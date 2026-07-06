// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DotNetty.Buffers;
using Nocturne.Database.Cache;
using Nocturne.Database.Exceptions;
using Nocturne.Database.Extensions;
using Nocturne.Database.Migrations;
using Nocturne.Database.Storage;
using Serilog;

namespace Nocturne.Database.API;

public class NocturneCollection<TKey, TValue>(
    string collectionKey,
    int schemaVersion,
    NocturneKeySerializer<TKey> keySerializer,
    INocturneSerializer<TValue> valueSerializer,
    NocturneDatabase databaseContext,
    IMigrationStrategy? migrationStrategy = null
) where TValue : class
{
    public readonly NocturneKeySerializer<TKey> KeySerializer = keySerializer;
    public readonly INocturneSerializer<TValue> ValueSerializer = valueSerializer;
    public readonly NocturneDatabase DatabaseContext = databaseContext;
    public MemoryCache MemoryCache => DatabaseContext.MemoryCache;
    public FileManager FileManager => DatabaseContext.FileManager;

    public readonly string CollectionKey = collectionKey;
    public readonly int SchemaVersion = schemaVersion;

    public void EnsureMigrated()
    {
        if (LatestCommitedSchemaVersion >= SchemaVersion) return;
        switch (migrationStrategy)
        {
            case IMigrationStrategy.DeleteIfRequired:
                Log.Warning("No migration provided for '{key}' (v{old} -> v{new}), wiping collection", CollectionKey, LatestCommitedSchemaVersion, SchemaVersion);
                FileManager.DeleteCollection(CollectionKey);
                DatabaseContext.MetaCollection.UpdateSchemaVersionFor(this, SchemaVersion);
                break;

            case IMigrationStrategy.Migration migration:
                var currentVersion = LatestCommitedSchemaVersion;
                var currentTransform = default(Func<IByteBuffer, IByteBuffer>);

                while (currentVersion < SchemaVersion)
                {
                    if (!migration.Steps.TryGetValue(currentVersion, out var step))
                        SchemaMigrationRequiredException.Throw(CollectionKey, currentVersion, SchemaVersion);

                    currentTransform = currentTransform == null
                        ? step
                        : compose(currentTransform, step!);

                    currentVersion++;
                }

                Log.Information("Performing migration on collection {key} from version {old} -> {new}", CollectionKey, LatestCommitedSchemaVersion, SchemaVersion);
                FileManager.MigrateCollection(CollectionKey, currentTransform!);
                DatabaseContext.MetaCollection.UpdateSchemaVersionFor(this, SchemaVersion);
                break;

            case null:
                SchemaMigrationRequiredException.Throw(CollectionKey, LatestCommitedSchemaVersion, SchemaVersion);
                break;
        }
    }

    private static Func<IByteBuffer, IByteBuffer> compose(Func<IByteBuffer, IByteBuffer> first, Func<IByteBuffer, IByteBuffer> second) => buffer => second(first(buffer));

    public int LatestCommitedSchemaVersion => DatabaseContext.Metadata.SchemaVersions.GetOrNullStruct(CollectionKey) ?? 0;

    public IEnumerable<TKey> Keys
    {
        get
        {
            var keys = MemoryCache.GetAllForCollection(CollectionKey).Keys;
            foreach (var keyBytes in keys)
            {
                var keyBuffer = keyBytes.ToByteBuffer();
                TKey key;

                try
                {
                    key = KeySerializer.Read(keyBuffer);
                }
                finally
                {
                    keyBuffer.Release();
                }

                yield return key;
            }
        }
    }

    public IEnumerable<TValue> Values
    {
        get
        {
            var keys = MemoryCache.GetAllForCollection(CollectionKey).Values.Select(s => s.Position);
            foreach (var chunk in FileManager.ReadChunks(keys))
            {
                TValue value;
                try
                {
                    value = ValueSerializer.Read(chunk.Value);
                }
                finally
                {
                    chunk.Release();
                }

                yield return value;
            }
        }
    }

    public int Count => DatabaseContext.MemoryCache.CountForCollection(CollectionKey);

    public void Insert(TKey key, TValue value)
    {
        var keyBuffer = PooledByteBufferAllocator.Default.Buffer();
        var valueBuffer = PooledByteBufferAllocator.Default.Buffer();
        try
        {
            KeySerializer.Write(keyBuffer, key);
            ValueSerializer.Write(valueBuffer, value);
            var chunk = new Chunk(ChunkType.Record, CollectionKey, keyBuffer, valueBuffer);
            FileManager.WriteChunk(chunk);
        }
        finally
        {
            keyBuffer.Release();
            valueBuffer.Release();
        }
    }

    public void Delete(TKey key)
    {
        var keyBuffer = PooledByteBufferAllocator.Default.Buffer();
        try
        {
            KeySerializer.Write(keyBuffer, key);
            var chunk = new Chunk(ChunkType.Delete, CollectionKey, keyBuffer, PooledByteBufferAllocator.Default.Buffer());
            FileManager.WriteChunk(chunk);
        }
        finally
        {
            keyBuffer.Release();
        }
    }

    public TValue? FindOrNull(TKey key)
    {
        var keyBuffer = PooledByteBufferAllocator.Default.Buffer();
        try
        {
            KeySerializer.Write(keyBuffer, key);
            var position = MemoryCache.Get(CollectionKey, keyBuffer);
            if (!position.HasValue) return null;

            var chunk = FileManager.ReadChunk(position.Value.Position);
            if (chunk == null)
            {
                Log.Warning("null chunk at cached stream offset {offset}", position);
                return null;
            }

            var valueBuffer = chunk.Value;

            try
            {
                return ValueSerializer.Read(valueBuffer);
            }
            finally
            {
                chunk.Release();
            }
        }
        finally
        {
            keyBuffer.Release();
        }
    }

    public TValue Find(TKey key) => FindOrNull(key) ?? throw new KeyNotFoundException();

    public TValue FindOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        var existing = FindOrNull(key);
        if (existing != null) return existing;

        var newValue = valueFactory.Invoke(key);
        Insert(key, newValue);
        return newValue;
    }

    public bool ContainsKey(TKey key)
    {
        var keyBuffer = PooledByteBufferAllocator.Default.Buffer();
        try
        {
            KeySerializer.Write(keyBuffer, key);
            return MemoryCache.GetAllForCollection(CollectionKey).ContainsKey(KeyBytes.FromBuffer(keyBuffer));
        }
        finally
        {
            keyBuffer.Release();
        }
    }

    public IEnumerable<TValue> FindAllWhere(Func<TValue, bool> predicate)
    {
        var positions = MemoryCache.GetAllForCollection(CollectionKey).Values;
        foreach (var chunk in FileManager.ReadChunks(positions.Select(s => s.Position)))
        {
            TValue entity;
            try
            {
                var value = ValueSerializer.Read(chunk.Value);
                entity = value;
            }
            finally
            {
                chunk.Release();
            }

            if (predicate.Invoke(entity))
            {
                yield return entity;
            }
        }
    }

    public IEnumerable<KeyValuePair<TKey, TValue>> FindAll()
    {
        foreach (var references in MemoryCache.GetAllForCollection(CollectionKey))
        {
            var chunk = FileManager.ReadChunk(references.Value.Position);
            var keyBuffer = references.Key.ToByteBuffer();

            KeyValuePair<TKey, TValue> entity;
            try
            {
                if (chunk == null)
                {
                    Log.Warning("null chunk at cached stream offset {offset}", references.Value);
                    continue;
                }

                var key = KeySerializer.Read(keyBuffer);
                var value = ValueSerializer.Read(chunk.Value);
                entity = new KeyValuePair<TKey, TValue>(key, value);
            }
            finally
            {
                chunk?.Release();
                keyBuffer.Release();
            }

            yield return entity;
        }
    }

    public void Update(TKey key, Action<TValue> action)
    {
        var value = Find(key);
        action.Invoke(value);
        Insert(key, value);
    }

    public void Nuke()
    {
        var keys = MemoryCache.GetAllForCollection(CollectionKey).Keys;
        foreach (var keyBytes in keys)
        {
            var buffer = keyBytes.ToByteBuffer();
            try
            {
                var key = KeySerializer.Read(buffer);
                Delete(key);
            }
            finally
            {
                buffer.Release();
            }
        }
    }
}
