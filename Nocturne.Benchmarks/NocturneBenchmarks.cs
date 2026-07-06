using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Codon.Binary;
using Nocturne.Database;
using Nocturne.Database.API;
using Synesthesia.Utils.Randomness;

namespace Nocturne.Benchmarks;

[MemoryDiagnoser] // tracks allocations per op — you care about this given IByteBuffer lifecycle
[EventPipeProfiler(EventPipeProfile.GcVerbose)]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class NocturneBenchmarks
{
    private NocturneDatabase db = null!;
    private NocturneCollection<string, Person> people = null!;
    private string dbPath = null!;

    [GlobalSetup]
    public void Setup()
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"bench-{Guid.NewGuid()}.db");
        db = new NocturneDatabase { FilePath = dbPath, AutomaticallyCompact = false, CompactOnLaunch = false };
        db.Open();
        people = db.For("people", 0, KeySerializers.STRING, Person.DATABASE_SERIALIZER);

        for (int i = 0; i < 10_000; i++)
        {
            people.Insert($"key{i}", new Person($"Person{i}", i, true));
            people.Insert($"key{i}", new Person($"Person{i}", i + Rng.RandomInt(0, 100), true));
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        db.Dispose();
        File.Delete(dbPath);
    }

    [Benchmark]
    public void Insert() => people.Insert(Guid.NewGuid().ToString(), new Person("Test", 1, true));

    [Benchmark]
    public void Compact() => db.Compact();

    [Benchmark]
    public Person Find() => people.Find("key5000");

    [Benchmark]
    public int FindAllCount() => people.FindAll().Count();

    [Benchmark]
    public int Count() => people.Count;

    [Benchmark]
    public void Delete() => people.Delete("key5000");

    [Benchmark]
    public void Nuke() => people.Nuke();

    public record Person(string Name, int Age, bool IsCool)
    {
        private static readonly IBinaryCodec<Person> codec = BinaryCodecs.For<Person>()
            .Field(BinaryCodecs.STRING, p => p.Name)
            .Field(BinaryCodecs.INT, p => p.Age)
            .Field(BinaryCodecs.BOOLEAN, p => p.IsCool)
            .Build((name, age, cool) => new Person(name, age, cool));

        public static readonly INocturneSerializer<Person> DATABASE_SERIALIZER = NocturneSerializer.FromCodec(codec);
    }
}
