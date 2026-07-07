// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using Nocturne.Database.API;

namespace Nocturne.Tests;

[TestFixture]
public class OutlierTests : NocturneTestBase
{

    private NocturneCollection<string, Person> people =>
        Nocturne.For("people", 0, KeySerializers.STRING, Person.DATABASE_SERIALIZER);


    [Test]
    public void ReplicateDeleteCacheCrash()
    {
        for (int i = 0; i < 200; i++)
        {
            people.Insert($"target_person_{i}", new Person("Seed Data", i));
        }

        Assert.DoesNotThrow(() =>
        {
            for (int i = 0; i < 200; i++)
            {
                people.Insert($"target_person_{i}", new Person("Updated Data", i * 2));

                if (i % 2 == 0)
                {
                    people.Delete($"target_person_{i}");
                }
            }
        });
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
