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
    };

    public static void Main(string[] args)
    {
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.SpectreConsole(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u4}] {Message:lj}{NewLine}{Exception}", minLevel: LogEventLevel.Verbose)
            .CreateLogger();

        Log.Logger = logger;

        NOCTURNE_DATABASE.Open();

        // previous run
        // var person = new Person("Stelle", 23, true, "cute");
        // Person.DB_COLLECTION.Insert("jackiepurplish", person);

        var readPerson = Person.DB_COLLECTION.Find("jackiepurplish");
        Log.Information("jackie - {person}", readPerson);
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
        "people",
        1,
        KeySerializers.STRING,
        DATABASE_SERIALIZER,
        IMigrationStrategy.Migrations()
            .Add(0, buffer =>
            {
                var name = BinaryCodecs.STRING.Read(buffer);
                var age = BinaryCodecs.INT.Read(buffer);
                var isCool = BinaryCodecs.BOOLEAN.Read(buffer);
                var _ = BinaryCodecs.STRING.Read(buffer);

                var newBuffer = Unpooled.Buffer();
                BinaryCodecs.STRING.Write(newBuffer, name);
                BinaryCodecs.INT.Write(newBuffer, age);
                BinaryCodecs.BOOLEAN.Write(newBuffer, isCool);

                return newBuffer;
            })
            .Build()
        );
}
