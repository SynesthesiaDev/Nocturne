using Codon.Binary;
using Nocturne.Database;
using Nocturne.Database.API;

namespace Nocturne.Tests;

[TestFixture]
public class ReproductionTests
{
    private NocturneDatabase db;
    private const string db_path = "./repro.nocturne";

    [SetUp]
    public void Setup()
    {
        if (File.Exists(db_path)) File.Delete(db_path);
        if (File.Exists(db_path + ".log")) File.Delete(db_path + ".log");

        db = new NocturneDatabase
        {
            FilePath = db_path,
            SchemaVersion = 0
        };
        db.Open();
    }

    [TearDown]
    public void TearDown()
    {
        db.Dispose();
        if (File.Exists(db_path)) File.Delete(db_path);
        if (File.Exists(db_path + ".log")) File.Delete(db_path + ".log");
    }

    [Test]
    public void TestInsertAndFind()
    {
        var personCodec = BinaryCodecs.For<PersonRepro>()
            .Field(BinaryCodecs.STRING, p => p.Name)
            .Field(BinaryCodecs.INT, p => p.Age)
            .Field(BinaryCodecs.BOOLEAN, p => p.IsCool)
            .Field(BinaryCodecs.STRING, p => p.RandomFact)
            .Build((name, age, cool, fact) => new PersonRepro(name, age, cool, fact));

        var serializer = NocturneSerializer.FromCodec(personCodec);
        var collection = db.For(KeySerializers.INT, serializer);

        var stelle = new PersonRepro("Stelle", 23, true, "faggot");

        collection.Transaction(_ =>
        {
            collection.Insert(0, stelle);
        });

        var found = collection.FindOrNull(0);
        Assert.That(found, Is.Not.Null, "Should find the person after insert");
        Assert.That(found.Name, Is.EqualTo("Stelle"));
    }

    [Test]
    public void TestInsertCloseOpenFind()
    {
        var personCodec = BinaryCodecs.For<PersonRepro>()
            .Field(BinaryCodecs.STRING, p => p.Name)
            .Field(BinaryCodecs.INT, p => p.Age)
            .Field(BinaryCodecs.BOOLEAN, p => p.IsCool)
            .Field(BinaryCodecs.STRING, p => p.RandomFact)
            .Build((name, age, cool, fact) => new PersonRepro(name, age, cool, fact));

        var serializer = NocturneSerializer.FromCodec(personCodec);

        {
            using var db1 = new NocturneDatabase { FilePath = db_path, SchemaVersion = 0 };
            db1.Open();
            var collection1 = db1.For(KeySerializers.INT, serializer);
            collection1.Transaction(_ => {
                collection1.Insert(0, new PersonRepro("Stelle", 23, true, "faggot"));
            });
        }

        {
            using var db2 = new NocturneDatabase { FilePath = db_path, SchemaVersion = 0 };
            db2.Open();
            var collection2 = db2.For(KeySerializers.INT, serializer);
            var found = collection2.FindOrNull(0);
            Assert.That(found, Is.Not.Null, "Should find the person after reopening");
            Assert.That(found.Name, Is.EqualTo("Stelle"));
        }
    }
}

public record PersonRepro(string Name, int Age, bool IsCool, string RandomFact);
