// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using Nocturne.Database.API;

namespace Nocturne.Tests;

[TestFixture]
public class CompactTests : NocturneTestBase
{
    private NocturneCollection<string, Person> people =>
        Nocturne.For("people", 0, KeySerializers.STRING, Person.DATABASE_SERIALIZER);

    [Test]
    public void CompactionPreservesLiveData()
    {
        people.Insert("stelle", new Person("Stelle", 23));
        people.Insert("syn", new Person("Syn", 21));
        people.Insert("zara", new Person("Zara", 19));

        Nocturne.FileManager.Compact();

        Assert.That(people.Find("stelle").Age, Is.EqualTo(23));
        Assert.That(people.Find("syn").Age, Is.EqualTo(21));
        Assert.That(people.Find("zara").Age, Is.EqualTo(19));
        Assert.That(people.FindAll().ToList(), Has.Count.EqualTo(3));
    }

    [Test]
    public void CompactDropsDeadRecords()
    {
        // make some garbage stinky dead records
        people.Insert("stelle", new Person("Stelle", 1));
        people.Insert("stelle", new Person("Stelle", 18));
        people.Insert("stelle", new Person("Stelle", 19));
        people.Insert("stelle", new Person("Stelle", 20));
        people.Insert("stelle", new Person("Stelle", 21));
        people.Insert("stelle", new Person("Stelle", 22));
        people.Insert("stelle", new Person("Stelle", 23));
        people.Insert("syn", new Person("Syn", 21));
        people.Insert("syn", new Person("Syn", 22));
        people.Insert("syn", new Person("Syn", 23));
        people.Delete("syn");

        var sizeBefore = new FileInfo(Nocturne.FilePath).Length;
        Nocturne.FileManager.Compact();
        var sizeAfter = new FileInfo(Nocturne.FilePath).Length;

        Assert.That(sizeAfter, Is.LessThan(sizeBefore));
        Assert.That(people.Find("stelle").Age, Is.EqualTo(23));
        Assert.That(people.FindOrNull("syn"), Is.Null);
        Assert.That(people.FindAll().ToList(), Has.Count.EqualTo(1));
    }

    [Test]
    public void OffsetsAreCorrect()
    {
        for (int i = 0; i < 20; i++)
            people.Insert($"person{i}", new Person($"Person{i}", i));

        for (int i = 0; i < 10; i++)
            people.Insert($"person{i}", new Person($"Person{i}", i + 100));

        Nocturne.FileManager.Compact();

        for (int i = 0; i < 20; i++)
        {
            var expectedAge = i < 10 ? i + 100 : i;
            Assert.That(people.Find($"person{i}").Age, Is.EqualTo(expectedAge), $"person{i} != new after compaction");
        }
    }

    [Test]
    public void DataSurvivesCompactionAndRestart()
    {
        people.Insert("stelle", new Person("Stelle", 23));
        people.Insert("syn", new Person("Syn", 21));

        Nocturne.FileManager.Compact();
        SimulateRestart();

        Assert.That(people.Find("stelle").Age, Is.EqualTo(23));
        Assert.That(people.Find("syn").Age, Is.EqualTo(21));
    }

    [Test]
    public void OldCompactTempFileIsCleanedUpOnNextCompact()
    {
        people.Insert("stelle", new Person("Stelle", 23));

        // simulate crash mid-compaction
        File.WriteAllBytes(Nocturne.TempFilePath, [1, 2, 3, 4, 5]);

        Assert.DoesNotThrow(() => Nocturne.FileManager.Compact());
        Assert.That(people.Find("stelle").Age, Is.EqualTo(23)); // db untouched
        Assert.That(File.Exists(Nocturne.TempFilePath), Is.False); // file removed
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
