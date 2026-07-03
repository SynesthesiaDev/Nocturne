// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Buffers.Binary;
using Codon.Binary;
using DotNetty.Buffers;
using Nocturne.Database.Exceptions;
using Nocturne.Database.Utils;
using Serilog;

namespace Nocturne.Database.Storage;

public record WrappedChunk(uint Crc32Hash, IByteBuffer ChunkData)
{
    public static readonly IBinaryCodec<WrappedChunk> CODEC = BinaryCodecs.For<WrappedChunk>()
        .Field(BinaryCodecs.UINT, w => w.Crc32Hash)
        .Field(BinaryCodecs.BYTE_BUFFER, w => w.ChunkData)
        .Build((hash, data) => new WrappedChunk(hash, data));

    public readonly Lazy<Chunk> Resolved = new Lazy<Chunk>(() =>
    {
        var actualHash = Hasher.Crc32(ChunkData);
        if (actualHash != Crc32Hash)
            throw new CorruptedChunkException($"Hash doesn't match (read {Crc32Hash} != actual {actualHash})");

        var chunk = Chunk.CODEC.Read(ChunkData);
        return chunk;
    });

    public static Chunk? ReadChunkFromStream(Stream stream)
    {
        var crcBytes = new byte[4];
        if (stream.Read(crcBytes, 0, 4) < 4)
        {
            Log.Warning("crc bytes are less than 4");
            return null;
        }

        uint expectedHash = BinaryPrimitives.ReadUInt32BigEndian(crcBytes);

        var length = StreamUtils.ReadVarInt(stream);
        var chunkBytes = new byte[length];

        if (stream.Read(chunkBytes, 0, length) < length)
        {
            Log.Warning("read payloadLength from stream is less than payloadLength");
            return null;
        }

        var chunkBuffer = Unpooled.CopiedBuffer(chunkBytes);
        var actualHash = Hasher.Crc32(chunkBuffer);

        if (expectedHash != actualHash)
        {
            Log.Warning("expected hash isnt actual hash (expected {expected} != actual {actual})", expectedHash, actualHash);
            return null;
        }

        var chunk = Chunk.CODEC.Read(chunkBuffer);

        chunkBuffer.Release();

        return chunk;
    }
}
