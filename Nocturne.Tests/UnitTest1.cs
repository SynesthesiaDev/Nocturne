using Nocturne.Database;

namespace Nocturne.Tests;

public class Tests
{
    public NocturneDatabase Nocturne;

    [SetUp]
    public void Setup()
    {
        Nocturne = new NocturneDatabase
        {
            FilePath = "./data/database.db",
            SchemaVersion = 0
        };

        Nocturne.Open();
    }

    [Test]
    public void Test1()
    {

    }

    [TearDown]
    public void Dispose()
    {
        Nocturne.Dispose();
    }
}
