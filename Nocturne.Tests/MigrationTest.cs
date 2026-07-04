// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using DotNetty.Buffers;
using Nocturne.Database.API;
using Nocturne.Database.Exceptions;
using Nocturne.Database.Migrations;
using Nocturne.Database.Storage;

namespace Nocturne.Tests;

[TestFixture]
public class MigrationTest : NocturneTestBase
{
    private record PersonV1(string Name, int Age, bool IsCool)
    {
        private static readonly IBinaryCodec<PersonV1> codec = BinaryCodecs.For<PersonV1>()
            .Field(BinaryCodecs.STRING, p => p.Name)
            .Field(BinaryCodecs.INT, p => p.Age)
            .Field(BinaryCodecs.BOOLEAN, p => p.IsCool)
            .Build((name, age, cool) => new PersonV1(name, age, cool));

        public static readonly INocturneSerializer<PersonV1> DATABASE_SERIALIZER = NocturneSerializer.FromCodec(codec);
    }

    // old shape used only to make legacy bytes for the test
    private record PersonV0(string Name, int Age, bool IsCool, string FunFact)
    {
        public static readonly IBinaryCodec<PersonV0> CODEC = BinaryCodecs.For<PersonV0>()
            .Field(BinaryCodecs.STRING, p => p.Name)
            .Field(BinaryCodecs.INT, p => p.Age)
            .Field(BinaryCodecs.BOOLEAN, p => p.IsCool)
            .Field(BinaryCodecs.STRING, p => p.FunFact)
            .Build((name, age, cool, fact) => new PersonV0(name, age, cool, fact));
    }

    private static IMigrationStrategy getMigrationStrategy() =>
        IMigrationStrategy.Migrations()
            .Add(0, buffer =>
            {
                var name = BinaryCodecs.STRING.Read(buffer);
                var age = BinaryCodecs.INT.Read(buffer);
                var isCool = BinaryCodecs.BOOLEAN.Read(buffer);
                BinaryCodecs.STRING.Read(buffer); // discard FunFact

                var newBuffer = Unpooled.Buffer();
                BinaryCodecs.STRING.Write(newBuffer, name);
                BinaryCodecs.INT.Write(newBuffer, age);
                BinaryCodecs.BOOLEAN.Write(newBuffer, isCool);
                return newBuffer;
            })
            .Build();

    // writes v0 record directly, bypassing the current codec to simulate data written by old version of the app before schema changed
    private void writeLegacyData(string key, PersonV0 person)
    {
        var keyBuffer = Unpooled.Buffer();
        var valueBuffer = Unpooled.Buffer();
        try
        {
            KeySerializers.STRING.Write(keyBuffer, key);
            PersonV0.CODEC.Write(valueBuffer, person);
            var chunk = new Chunk(ChunkType.Record, "people", keyBuffer, valueBuffer);
            Nocturne.FileManager.WriteChunk(chunk);
        }
        finally
        {
            keyBuffer.Release();
            valueBuffer.Release();
        }
    }

    [Test]
    public void MigrationTransformsLegacyDataCorrectly()
    {
        writeLegacyData("stelle", new PersonV0("Stelle", 23, true, "gay /pos"));

        var people = Nocturne.For("people", 1, KeySerializers.STRING, PersonV1.DATABASE_SERIALIZER, migrationStrategy: getMigrationStrategy());

        var result = people.Find("stelle");
        Assert.That(result.Name, Is.EqualTo("Stelle"));
        Assert.That(result.Age, Is.EqualTo(23));
        Assert.That(result.IsCool, Is.True);
    }

    [Test]
    public void MigrationDoesNotRerunOnSecondOpen()
    {
        writeLegacyData("stelle", new PersonV0("Stelle", 23, true, "gay /pos"));

        var people = Nocturne.For("people", 1, KeySerializers.STRING, PersonV1.DATABASE_SERIALIZER, migrationStrategy: getMigrationStrategy());
        Assert.That(people.Find("stelle").Name, Is.EqualTo("Stelle"));

        SimulateRestart();

        // passing null so if a migration were attempted, this would throw
        var reopened = Nocturne.For("people", 1, KeySerializers.STRING, PersonV1.DATABASE_SERIALIZER, migrationStrategy: null);
        Assert.That(reopened.Find("stelle").Name, Is.EqualTo("Stelle"));
    }

    [Test]
    public void MissingMigrationStrategyThrows()
    {
        writeLegacyData("stelle", new PersonV0("Stelle", 23, true, "gay /pos"));
        Assert.Throws<SchemaMigrationRequiredException>(() => Nocturne.For("people", 1, KeySerializers.STRING, PersonV1.DATABASE_SERIALIZER, migrationStrategy: null));
    }

    [Test]
    public void MissingMigrationStepInChainThrows()
    {
        writeLegacyData("stelle", new PersonV0("Stelle", 23, true, "gay /pos"));

        // requesting version 2, but only a 0 -> 1 step is registered, 1 -> 2 is missing
        var incompleteStrategy = IMigrationStrategy.Migrations()
            .Add(0, buffer => buffer)
            .Build();

        Assert.Throws<SchemaMigrationRequiredException>(() => Nocturne.For("people", 2, KeySerializers.STRING, PersonV1.DATABASE_SERIALIZER, migrationStrategy: incompleteStrategy));
    }

    [Test]
    public void DeleteIfMigrationRequiredWipesCollection()
    {
        writeLegacyData("stelle", new PersonV0("Stelle", 23, true, "gay /pos"));
        writeLegacyData("syn", new PersonV0("Syn", 21, true, "nothing interesting"));

        var people = Nocturne.For("people", 1, KeySerializers.STRING, PersonV1.DATABASE_SERIALIZER, migrationStrategy: IMigrationStrategy.DeleteIfMigrationRequired());

        Assert.That(people.FindAll().ToList(), Is.Empty);
    }

    [Test]
    public void DeleteStrategyDoesNotWipeAgainOnNextOpen()
    {
        writeLegacyData("stelle", new PersonV0("Stelle", 23, true, "gay /pos"));

        Nocturne.For("people", 1, KeySerializers.STRING, PersonV1.DATABASE_SERIALIZER, migrationStrategy: IMigrationStrategy.DeleteIfMigrationRequired());

        SimulateRestart();

        var reopened = Nocturne.For("people", 1, KeySerializers.STRING, PersonV1.DATABASE_SERIALIZER, migrationStrategy: null);
        reopened.Insert("zara", new PersonV1("Zara", 19, true));

        Assert.That(reopened.Find("zara").Name, Is.EqualTo("Zara"));
    }
}
