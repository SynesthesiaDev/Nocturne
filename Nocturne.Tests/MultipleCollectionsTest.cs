// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using Nocturne.Database.API;

namespace Nocturne.Tests;

[TestFixture]
public class MultipleCollectionsTest : NocturneTestBase
{
    private NocturneCollection<string, Person> people =>
        Nocturne.For("people", 0, KeySerializers.STRING, Person.DATABASE_SERIALIZER);

    private NocturneCollection<string, Order> orders =>
        Nocturne.For("orders", 0, KeySerializers.STRING, Order.DATABASE_SERIALIZER);


    [Test]
    public void KeyNotCollide()
    {
        people.Insert("1", new Person("Stelle", 23));
        orders.Insert("1", new Order("Widget", 5));

        Assert.That(people.Find("1").Name, Is.EqualTo("Stelle"));
        Assert.That(orders.Find("1").Item, Is.EqualTo("Widget"));
    }

    [Test]
    public void KeyNotCollideInDelete()
    {
        people.Insert("1", new Person("Stelle", 23));
        orders.Insert("1", new Order("Widget", 5));

        people.Delete("1");

        Assert.That(people.FindOrNull("1"), Is.Null);
        Assert.That(orders.FindOrNull("1"), Is.Not.Null);
    }

    [Test]
    public void CollectionsSurviveRestart()
    {
        people.Insert("stelle", new Person("Stelle", 23));
        orders.Insert("order1", new Order("Widget", 5));

        SimulateRestart();

        Assert.That(people.Find("stelle").Name, Is.EqualTo("Stelle"));
        Assert.That(orders.Find("order1").Item, Is.EqualTo("Widget"));
        Assert.That(people.FindAll().ToList(), Has.Count.EqualTo(1));
        Assert.That(orders.FindAll().ToList(), Has.Count.EqualTo(1));
    }

    [Test]
    public void CompactionKeepsCollectionsSeparate()
    {
        people.Insert("stelle", new Person("Stelle", 1));
        people.Insert("stelle", new Person("Stelle", 23));
        orders.Insert("order1", new Order("Widget", 5));
        orders.Delete("order1");
        orders.Insert("order2", new Order("Gadget", 2));

        Nocturne.FileManager.Compact();

        Assert.That(people.Find("stelle").Age, Is.EqualTo(23));
        Assert.That(orders.FindOrNull("order1"), Is.Null);
        Assert.That(orders.Find("order2").Item, Is.EqualTo("Gadget"));
        Assert.That(people.FindAll().ToList(), Has.Count.EqualTo(1));
        Assert.That(orders.FindAll().ToList(), Has.Count.EqualTo(1));
    }

    [Test]
    public void InterleavedStayIsolated()
    {
        for (int i = 0; i < 10; i++)
        {
            people.Insert($"p{i}", new Person($"Person{i}", i));
            orders.Insert($"o{i}", new Order($"Item{i}", i));
        }

        for (int i = 0; i < 10; i++)
        {
            Assert.That(people.Find($"p{i}").Age, Is.EqualTo(i));
            Assert.That(orders.Find($"o{i}").Quantity, Is.EqualTo(i));
        }

        Assert.That(people.FindAll().ToList(), Has.Count.EqualTo(10));
        Assert.That(orders.FindAll().ToList(), Has.Count.EqualTo(10));
    }

    private record Person(string Name, int Age)
    {
        private static readonly IBinaryCodec<Person> codec = BinaryCodecs.For<Person>()
            .Field(BinaryCodecs.STRING, p => p.Name)
            .Field(BinaryCodecs.INT, p => p.Age)
            .Build((name, age) => new Person(name, age));

        public static readonly INocturneSerializer<Person> DATABASE_SERIALIZER = NocturneSerializer.FromCodec(codec);
    }

    private record Order(string Item, int Quantity)
    {
        private static readonly IBinaryCodec<Order> codec = BinaryCodecs.For<Order>()
            .Field(BinaryCodecs.STRING, o => o.Item)
            .Field(BinaryCodecs.INT, o => o.Quantity)
            .Build((item, qty) => new Order(item, qty));

        public static readonly INocturneSerializer<Order> DATABASE_SERIALIZER = NocturneSerializer.FromCodec(codec);
    }
}
