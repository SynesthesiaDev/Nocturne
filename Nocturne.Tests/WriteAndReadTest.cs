using Codon.Binary;
using Nocturne.Database.API;

namespace Nocturne.Tests;

[TestFixture]
public class WriteAndReadTest : NocturneTestBase
{
    private NocturneCollection<string, Person> people =>
        Nocturne.For("people", 0, KeySerializers.STRING, Person.DATABASE_SERIALIZER);

    [Test]
    public void TestReadWriteRoundRobin()
    {
        var stelle = new Person("Stelle", 23, true, "gay /pos");
        var syn = new Person("Syn", 21, true, "nothing interesting");
        var zara = new Person("Zara", 19, true, "borger");

        people.Insert("stelle", stelle);
        people.Insert("synsyn", syn);
        people.Insert("zara", zara);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(stelle, Is.EqualTo(people.Find("stelle")));
            Assert.That(syn, Is.EqualTo(people.Find("synsyn")));
            Assert.That(zara, Is.EqualTo(people.Find("zara")));

            Assert.That(people.Values.ToList(), Has.Count.EqualTo(3));
            Assert.That(people.Keys.ToList(), Has.Count.EqualTo(3));
            Assert.That(people.FindAll().ToList(), Has.Count.EqualTo(3));
        }
    }

    [Test]
    public void TestUnknownKeys()
    {
        Assert.That(people.FindOrNull("skelly"), Is.Null);
        Assert.Throws<KeyNotFoundException>(() => people.Find("skelly"));

        people.FindOrAdd("skelly", _ => new Person("skelly", 22, true, "deer"));
        Assert.That(people.FindOrNull("skelly"), Is.Not.Null);
        Assert.DoesNotThrow(() => people.Find("skelly"));
    }

    [Test]
    public void TestPersistsAcrossRestarts()
    {
        people.Insert("stelle", new Person("Stelle", 23, true, "gay /pos"));

        SimulateRestart();

        Assert.That(people.Find("stelle").Name, Is.EqualTo("Stelle"));
    }

    [Test]
    public void RecoversFromCorruptedWrite()
    {
        people.Insert("stelle", new Person("Stelle", 23, true, "gay /pos"));
        people.Insert("syn", new Person("Syn", 21, true, "nothing interesting"));

        CorruptFileWithGarbage();

        Assert.That(people.Find("stelle").Name, Is.EqualTo("Stelle"));
        Assert.That(people.Find("syn").Name, Is.EqualTo("Syn"));
    }

    [Test]
    public void TestFindsTheLatestLog()
    {
        people.Insert("stelle", new Person("Stelle", 23, true, "testing value"));
        people.Insert("stelle", new Person("Stelle", 23, true, "new testing value"));

        Assert.That(people.Find("stelle").RandomFact, Is.EqualTo("new testing value"));
    }

    [Test]
    public void TestDeleteWorks()
    {
        people.Insert("stelle", new Person("Stelle", 23, true, "testing value"));
        people.Insert("stelle", new Person("Stelle", 23, true, "new testing value"));

        Assert.That(people.FindOrNull("stelle"), Is.Not.Null);
        Assert.That(people.ContainsKey("stelle"), Is.True);

        people.Delete("stelle");

        Assert.That(people.FindOrNull("stelle"), Is.Null);
        Assert.That(people.ContainsKey("stelle"), Is.False);
    }

    private record Person(string Name, int Age, bool IsCool, string RandomFact)
    {
        private static readonly IBinaryCodec<Person> codec = BinaryCodecs.For<Person>()
            .Field(BinaryCodecs.STRING, p => p.Name)
            .Field(BinaryCodecs.INT, p => p.Age)
            .Field(BinaryCodecs.BOOLEAN, p => p.IsCool)
            .Field(BinaryCodecs.STRING, p => p.RandomFact)
            .Build((name, age, cool, fact) => new Person(name, age, cool, fact));

        public static readonly INocturneSerializer<Person> DATABASE_SERIALIZER = NocturneSerializer.FromCodec(codec);
    }
}
