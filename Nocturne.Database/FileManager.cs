// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.


using Codon.Binary;
using DotNetty.Buffers;
using Nocturne.Database.Cache;
using Nocturne.Database.Exceptions;
using Nocturne.Database.Storage;
using Serilog;
using Synesthesia.Utils.Profiler;

namespace Nocturne.Database;

public class FileManager(NocturneDatabase database) : IDisposable
{
    public readonly NocturneDatabase Database = database;
    private FileStream databaseStream = new(database.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 0, FileOptions.RandomAccess);

    public bool NeedsCompaction => Database.MemoryCache.DeadBytes >= Database.MemoryCache.Size;

    public int Compactions { get; private set; }

    public void PopulateCache()
    {
        var profiler = Timings.RentAndPush();
        databaseStream.Seek(0, SeekOrigin.Begin);
        var memoryCache = Database.MemoryCache;
        var buffer = PooledByteBufferAllocator.Default.Buffer();

        try
        {
            while (databaseStream.Position < databaseStream.Length)
            {
                var chunkStart = databaseStream.Position;

                Chunk chunk;
                try
                {
                    var readChunk = WrappedChunk.ReadChunkFromStream(databaseStream);

                    if (readChunk == null)
                    {
                        Log.Error("read chunk is null");
                        databaseStream.SetLength(chunkStart);
                        break;
                    }

                    chunk = readChunk;
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to read chunk form database file");
                    databaseStream.SetLength(chunkStart);
                    break;
                }

                if (chunk.ChunkType == ChunkType.Delete)
                {
                    memoryCache.Remove(chunk.CollectionKey, chunk.Key);
                }
                else
                {
                    memoryCache.Insert(chunk.CollectionKey, chunk.Key, new MemoryCache.Entry(chunkStart, chunk.Value.ReadableBytes));
                }
            }
        }
        finally
        {
            buffer.Release();
        }

        Log.Information("Populated {entries} entries into memory cache in {time}ms", memoryCache.Size, profiler.PopAndReturn());
        CheckForAutoCompact();
    }

    public List<Chunk> ReadChunks(IEnumerable<long> positions, bool throwIfNull = false)
    {
        var list = new List<Chunk>();
        foreach (var position in positions)
        {
            var chunk = ReadChunk(position);
            if (chunk == null)
            {
                if (throwIfNull)
                    throw new CorruptedChunkException($"Chunk at position {position} was read as null");

                continue;
            }

            list.Add(chunk);
        }

        return list;
    }

    public Chunk? ReadChunk(long position)
    {
        try
        {
            databaseStream.Seek(position, SeekOrigin.Begin);

            var readChunk = WrappedChunk.ReadChunkFromStream(databaseStream);
            if (readChunk != null) return readChunk;

            Log.Warning("read chunk at positon {position} is null (where did you get this position from?)", position);
            return null;
        }

        catch (Exception e)
        {
            Log.Error(e, "Failed to read chunk form database file");
            return null;
        }
    }

    public void WriteChunk(Chunk chunk, bool updateCache = true) => WriteChunk(chunk, databaseStream, updateCache);

    public void WriteChunk(Chunk chunk, Stream stream, bool updateCache = true)
    {
        var wrapped = chunk.ToWrapped();
        var buffer = PooledByteBufferAllocator.Default.Buffer();
        try
        {
            WrappedChunk.CODEC.Write(buffer, wrapped);

            var position = stream.Seek(0, SeekOrigin.End);
            stream.Write(buffer.ToByteArraySafe());
            stream.Flush();

            if (!updateCache) return;

            if (chunk.ChunkType == ChunkType.Delete)
                Database.MemoryCache.Remove(chunk.CollectionKey, chunk.Key);
            else
                Database.MemoryCache.Insert(chunk.CollectionKey, chunk.Key, new MemoryCache.Entry(position, chunk.Value.ReadableBytes));
        }
        finally
        {
            buffer.Release();
        }
        CheckForAutoCompact();
    }

    public void Compact()
    {
        var profiler = Timings.RentAndPush();

        if (File.Exists(Database.TempFilePath))
        {
            Log.Warning("Temporary compact file was found, your last compact may have not finished");
            File.Delete(Database.TempFilePath);
        }

        var sizeBefore = databaseStream.Length;

        try
        {
            var positions = Database.MemoryCache.AllEntries().Select(s => s.Value.Position);
            var latest = ReadChunks(positions, throwIfNull: true).ToList();

            using var tempStream = new FileStream(Database.TempFilePath, FileMode.Create, FileAccess.Write);
            foreach (var chunk in latest) WriteChunk(chunk, tempStream, updateCache: false);
            tempStream.Flush(true);
        }
        catch (Exception e)
        {
            Log.Error(e, "Compact failed during prepare phase, database untouched");
            if (File.Exists(Database.TempFilePath))
                File.Delete(Database.TempFilePath);
            return;
        }

        databaseStream.Dispose();
        File.Replace(Database.TempFilePath, Database.FilePath, null);
        databaseStream = new FileStream(Database.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 0, FileOptions.RandomAccess);
        Database.MemoryCache.Clear();
        PopulateCache();
        Compactions++;

        var ms = profiler.PopAndReturn();
        Log.Information("Nocturne Database compacted in {time}ms from {before} -> {now} bytes", ms, sizeBefore, databaseStream.Length);
    }

    public void DeleteCollection(string collectionKey)
    {
        foreach (var (key, value) in Database.MemoryCache.GetAllForCollection(collectionKey))
        {
            var keyBuffer = key.ToByteBuffer();
            try
            {
                WriteChunk(new Chunk(ChunkType.Delete, collectionKey, keyBuffer, Unpooled.Empty));
            }
            finally
            {
                keyBuffer.Release();
            }
        }

        CheckForAutoCompact();
    }

    public void MigrateCollection(string collectionKey, Func<IByteBuffer, IByteBuffer> transform)
    {
        var positions = Database.MemoryCache.GetAllForCollection(collectionKey).Values.Select(e => e.Position);
        var oldChunks = ReadChunks(positions, throwIfNull: true);

        foreach (var old in oldChunks)
        {
            var migratedValue = transform(old.Value);
            var newChunk = new Chunk(ChunkType.Record, collectionKey, old.Key, migratedValue);
            WriteChunk(newChunk);
        }
    }


    public void CheckForAutoCompact()
    {
        if (!NeedsCompaction || !Database.AutomaticallyCompact) return;

        Log.Information("Automatically compacting due to size of dead bytes being more than actual size");
        Compact();

    }

    public void Dispose()
    {
        databaseStream.Flush();
        databaseStream.Dispose();
    }
}
