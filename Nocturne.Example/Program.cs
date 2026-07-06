using Codon.Binary;
using DotNetty.Buffers;
using Nocturne.Database;
using Nocturne.Database.API;
using Nocturne.Database.Migrations;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SpectreConsole;

namespace Nocturne.Example;

public class Program
{
    public static readonly NocturneDatabase NOCTURNE_DATABASE = new NocturneDatabase
    {
        FilePath = "./data/database.nocturne",
        AutomaticallyCompact = false,
    };

    public static void Main(string[] args)
    {
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.SpectreConsole(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u4}] {Message:lj}{NewLine}{Exception}", minLevel: LogEventLevel.Verbose)
            .CreateLogger();

        Log.Logger = logger;

        NOCTURNE_DATABASE.Open();

        var newPerson = new Person("John Person", 21, false);
        Person.DB_COLLECTION.Insert("john", newPerson);

        var john = Person.DB_COLLECTION.Find("john");

        Person.DB_COLLECTION.Delete("john");

        //fuck it, who needs prod anyway
        Person.DB_COLLECTION.Nuke();

        // NOCTURNE_DATABASE.Compact();

        // for (int i = 0; i < 100_000; i++)
        // {
        //     var person = new Person($"random_person_{i}", i, true);
        //     Person.DB_COLLECTION.Insert(i.ToString(), person);
        // }

        Console.WriteLine("Press Enter to exit...");
        Console.ReadLine();
    }
}

public record Person(string Name, int Age, bool IsCool)
{
    public static readonly IBinaryCodec<Person> CODEC = BinaryCodecs.For<Person>()
        .Field(BinaryCodecs.STRING, p => p.Name)
        .Field(BinaryCodecs.INT, p => p.Age)
        .Field(BinaryCodecs.BOOLEAN, p => p.IsCool)
        .Build((name, age, cool) => new Person(name, age, cool));

    public static readonly INocturneSerializer<Person> DATABASE_SERIALIZER = NocturneSerializer.FromCodec(CODEC);


    // Schema version changes:
    // 0 -> 1 - removed "FunFact" string field

    public static readonly NocturneCollection<string, Person> DB_COLLECTION = Program.NOCTURNE_DATABASE.For(
        collectionKey: "people",
        schemaVersion: 1,
        keySerializer: KeySerializers.STRING,
        valueSerializer: DATABASE_SERIALIZER
    );
}
