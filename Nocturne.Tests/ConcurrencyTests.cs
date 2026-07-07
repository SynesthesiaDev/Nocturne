// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using Nocturne.Database.API;
using Synesthesia.Utils.Randomness;

namespace Nocturne.Tests;

[TestFixture]
public class ConcurrencyTests : NocturneTestBase
{
    private NocturneCollection<string, Person> people =>
        Nocturne.For("people", 0, KeySerializers.STRING, Person.DATABASE_SERIALIZER);


    [Test]
    public void TestConcurrentReadWriteDelete_ShouldNotCorruptStream()
    {
        const int task_count = 40;
        const int operations_per_task = 100;

        var tasks = new List<Task>();

        for (int i = 0; i < 50; i++)
        {
            people.Insert($"init_{i}", new Person($"Name_{i}", i));
        }

        for (int t = 0; t < task_count; t++)
        {
            int taskId = t;
            tasks.Add(Task.Run(() =>
            {
                for (int i = 0; i < operations_per_task; i++)
                {
                    string key = $"user_{taskId}_key_{i}";

                    if (taskId % 4 == 0)
                    {
                        people.Insert(key, new Person("Stelle", i));
                    }
                    else if (taskId % 4 == 1)
                    {
                        people.Insert(key, new Person("March", i * 2));
                        people.Delete(key);
                    }
                    else
                    {
                        var existing = people.FindOrNull($"init_{Rng.RandomInt(0, 50)}");
                        if (existing != null)
                        {
                            Assert.That(existing.Name, Does.StartWith("Name_"));
                        }
                    }
                }
            }));
        }

        Assert.DoesNotThrowAsync(async () => await Task.WhenAll(tasks));
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
