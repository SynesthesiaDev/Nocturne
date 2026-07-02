using Codon.Binary;
using Nocturne.Database;
using Nocturne.Database.API;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SpectreConsole;

namespace Nocturne.Example;

public class Program
{
    public static readonly NocturneDatabase NOCTURNE_DATABASE = new NocturneDatabase
    {
        FilePath = "./data/database.nocturne",
        SchemaVersion = 0
    };

    public static void Main(string[] args)
    {
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.SpectreConsole(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u4}] {Message:lj}{NewLine}{Exception}", minLevel: LogEventLevel.Verbose)
            .CreateLogger();

        Log.Logger = logger;

        NOCTURNE_DATABASE.Open();

        var stelle = new Person("Stelle", 23, true, "faggot");

        Person.DB_COLLECTION.Insert(0, stelle);
    }
}

public record Person(string Name, int Age, bool IsCool, string RandomFact)
{
    public static readonly IBinaryCodec<Person> CODEC = BinaryCodecs.For<Person>()
        .Field(BinaryCodecs.STRING, p => p.Name)
        .Field(BinaryCodecs.INT, p => p.Age)
        .Field(BinaryCodecs.BOOLEAN, p => p.IsCool)
        .Field(BinaryCodecs.STRING, p => p.RandomFact)
        .Build((name, age, cool, fact) => new Person(name, age, cool, fact));

    public static readonly INocturneSerializer<Person> DATABASE_SERIALIZER = NocturneSerializer.FromCodec(CODEC);
    public static readonly NocturneCollection<int, Person> DB_COLLECTION = Program.NOCTURNE_DATABASE.For(KeySerializers.INT, DATABASE_SERIALIZER);
}
