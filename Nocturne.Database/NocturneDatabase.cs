// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Faster.Map.Core;
using Nocturne.Database.API;
using Nocturne.Database.Cache;
using Nocturne.Database.Migrations;
using Nocturne.Database.Storage;
using Nocturne.Database.Utils;
using Serilog;

namespace Nocturne.Database;

public class NocturneDatabase : IDisposable
{
    public required string FilePath { get; init; }
    public bool CompactOnLaunch { get; init; } = true;
    public bool AutomaticallyCompact { get; init; } = true;

    public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? throw new InvalidOperationException("Invalid file path specified (cannot get directory name)");
    public string TempFilePath => Path.Combine(DirectoryPath, "compact.tmp") ?? throw new InvalidOperationException("Invalid file path specified (cannot get directory name)");

    public MetaNocturneCollection MetaCollection { get; private set; } = null!;

    public FileManager FileManager { get; private set; } = null!;

    public readonly MemoryCache MemoryCache = new MemoryCache(); //TODO Load memory cache from hint file

    public Metadata Metadata => MetaCollection.Get();

    private readonly List<Action> pendingMigrationChecks = [];

    public bool IsOpen { get; private set; }

    public int Compactions => FileManager.Compactions;

    public void Open()
    {
        if(IsOpen) return;

        Log.Information("Opening Nocturne database..");

        if (!Directory.Exists(DirectoryPath))
            Directory.CreateDirectory(DirectoryPath);

        var isNewDatabase = !File.Exists(FilePath);

        if (isNewDatabase)
        {
            Log.Information("Nocturne Database file doesn't exist or is empty, creating new one...");
            File.Create(FilePath).Close();
        }

        FileManager = new FileManager(this);

        MetaCollection = new MetaNocturneCollection(this);
        if (isNewDatabase)
        {
            var metadata = new Metadata(
                NocturneVersion: SharedConstants.NOCTURNE_VERSION,
                FileCreatedUtc: DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                LastCompactedUtc: SharedConstants.DEFAULT_COMPACT_TIMESTAMP,
                SchemaVersions: new BlitzMap<string, int>()
            );
            MetaCollection.Insert(metadata);
        }
        else
        {
            FileManager.PopulateCache();
        }

        foreach (var check in pendingMigrationChecks) check.Invoke();
        pendingMigrationChecks.Clear();

        if (CompactOnLaunch && FileManager.NeedsCompaction) Compact();

        IsOpen = true;
    }

    public void Compact() => FileManager.Compact();

    public NocturneCollection<TKey, TValue> For<TKey, TValue>(string collectionKey, int schemaVersion, NocturneKeySerializer<TKey> keySerializer, INocturneSerializer<TValue> valueSerializer, IMigrationStrategy? migrationStrategy = null) where TValue : class
    {
        var collection = new NocturneCollection<TKey, TValue>(collectionKey, schemaVersion, keySerializer, valueSerializer, this, migrationStrategy);
        if (IsOpen)
            collection.EnsureMigrated();
        else
            pendingMigrationChecks.Add(collection.EnsureMigrated);

        return collection;
    }

    public void Dispose()
    {
        FileManager.Dispose();
        MemoryCache.Dispose();
        Log.Information("Disposed Nocturne Database..");
    }
}
