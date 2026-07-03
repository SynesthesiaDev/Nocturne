// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.


using Codon.Binary;
using DotNetty.Buffers;
using Nocturne.Database.Storage;
using Serilog;

namespace Nocturne.Database;

public class FileManager(NocturneDatabase database)
{
    public readonly NocturneDatabase Database = database;
    private readonly FileStream databaseStream = new(database.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 0, FileOptions.RandomAccess);

    public void PopulateCache()
    {
        Log.Information("Populating memory cache..");
        databaseStream.Seek(0, SeekOrigin.Begin);
        var memoryCache = Database.MemoryCache;
        var buffer = Unpooled.Buffer();

        try
        {
            while (databaseStream.Position < databaseStream.Length)
            {
                Log.Verbose(" ");
                var chunkStart = databaseStream.Position;
                Log.Verbose("Chunk start - {start}", chunkStart);

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

                if (chunk.ChunkType == ChunkType.Meta)
                {
                    //TODO update meta
                } else if (chunk.ChunkType == ChunkType.Delete)
                {
                    Log.Information("Remove chunk with collection key {ck}", chunk.CollectionKey);
                    memoryCache.Remove(chunk.CollectionKey, chunk.Key);
                }
                else
                {
                    Log.Information("Insert chunk with collection key {ck}", chunk.CollectionKey);
                    memoryCache.Insert(chunk.CollectionKey, chunk.Key, chunkStart);
                }
            }
        }
        finally
        {
            buffer.Release();
        }

        // ALWAYS In format of:
        // WrappedChunk, which is not length prefixed:

        //CRC32 Hash (Hash of chunk) (fixed size I think?) [uint]
        //Size (Size of Chunk data) [varint]
        //Chunk (actual chunk)[data]

        //memoryCache.Insert(string collectionKey, Chunk.Key, start of chunk in stream);
    }

    public void WriteChunk(Chunk chunk)
    {
        Log.Verbose("Trying to write chunk with collection key {ck}", chunk.CollectionKey);

        var wrapped = chunk.Wrapped.Value;
        var buffer = Unpooled.Buffer();
        try
        {
            WrappedChunk.CODEC.Write(buffer, wrapped);

            var position = databaseStream.Seek(0, SeekOrigin.End);

            databaseStream.Write(buffer.ToByteArraySafe());
            databaseStream.Flush();
            Database.MemoryCache.Insert(chunk.CollectionKey, chunk.Key, position);
        }
        finally
        {
            buffer.Release();
        }
    }
}
