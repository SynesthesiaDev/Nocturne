using Nocturne.Database;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.SpectreConsole;

namespace Nocturne.Example;

class Program
{
    static void Main(string[] args)
    {
        using var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.SpectreConsole(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u4}] {Message:lj}{NewLine}{Exception}", minLevel: LogEventLevel.Verbose)
            .CreateLogger();

        Log.Logger = logger;
        
        var db = new NocturneDatabase
        {
            FilePath = "./data/database.nocturne",
            SchemaVersion = 0
        };
        
        db.Open();
    }
}