// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using Nocturne.Database;
using Nocturne.Database.API;
using Synesthesia.Utils.Randomness;

namespace Nocturne.Tests;

public class AutoCompactTest : NocturneTestBase
{
    protected override NocturneDatabase CreateDatabase(string filePath)
    {
        return new NocturneDatabase
        {
            FilePath = filePath,
            AutomaticallyCompact = true,
        };
    }

    private NocturneCollection<string, Person> people =>
        Nocturne.For("people", 0, KeySerializers.STRING, Person.DATABASE_SERIALIZER);

    [Test]
    public void AutomaticallyCompact()
    {
        var start = Nocturne.Compactions;
        for (int i = 0; i < 10; i++)
        {
            people.Insert("stelle", new Person("Stelle", Rng.RandomInt(0, 100)));
            people.Insert("syn", new Person("Syn", Rng.RandomInt(0, 100)));
            people.Insert("zara", new Person("Zara", Rng.RandomInt(0, 100)));
        }

        Assert.That(Nocturne.Compactions, Is.GreaterThan(start));
    }


    private record Person(string Name, int Age)
    {
        private static readonly IBinaryCodec<Person> codec = BinaryCodecs.For<Person>()
            .Field(BinaryCodecs.STRING, p => p.Name)
            .Field(BinaryCodecs.INT, p => p.Age)
            .Build((name, age) => new Person(name, age));

        public static readonly INocturneSerializer<Person> DATABASE_SERIALIZER = NocturneSerializer.FromCodec(codec);
    }

}
