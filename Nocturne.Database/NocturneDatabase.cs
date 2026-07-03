// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Nocturne.Database.Cache;
using Serilog;

namespace Nocturne.Database;

public class NocturneDatabase : IDisposable
{
    public required string FilePath { get; init; }
    public required int SchemaVersion { get; init; }
    public bool DeleteIfMigrationNeeded { get; init; } = false;
    public bool CompactOnLaunch { get; init; } = false;

    public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? throw new InvalidOperationException("Invalid file path specified (cannot get directory name)");
    public string FileName => Path.GetFileNameWithoutExtension(FilePath) ?? throw new InvalidOperationException("Invalid file path specified (cannot get file name)");

    public FileManager FileManager { get; private set; } = null!;

    //TODO Load memory cache from hint file
    public readonly MemoryCache MemoryCache = new MemoryCache();

    public void Open()
    {
        Log.Verbose("Opening database..");

        var directoryPath = Path.GetDirectoryName(FilePath)!;
        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        var isNewDatabase = !File.Exists(FilePath);

        if (isNewDatabase)
        {
            Log.Debug("Database file doesn't exist or is empty, creating new one...");
            File.Create(FilePath).Close();
        }

        FileManager = new FileManager(this);
        FileManager.PopulateCache();

        Log.Debug("MemoryCache size: {size}", MemoryCache.Size);
        foreach (var (key, value) in MemoryCache.AllEntries())
        {
            Log.Verbose("{key} - {value}", key.GetHashCode(), value);
        }
    }

    public void Dispose()
    {
    }
}
