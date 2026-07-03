// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using DotNetty.Buffers;

namespace Nocturne.Database.Storage;

public class Chunk
{
    public const string META_CHUNK_KEY = "__meta";

    public readonly ChunkType ChunkType;
    public readonly string CollectionKey;
    public readonly IByteBuffer Key;
    public readonly IByteBuffer Value;
    public readonly Lazy<WrappedChunk> Wrapped;

    public static readonly IBinaryCodec<Chunk> CODEC = BinaryCodecs.For<Chunk>()
        .Field(BinaryCodecs.Enum<ChunkType>(), c => c.ChunkType)
        .Field(BinaryCodecs.STRING, c => c.CollectionKey)
        .Field(BinaryCodecs.BYTE_BUFFER, c => c.Key)
        .Field(BinaryCodecs.BYTE_BUFFER, c => c.Value)
        .Build((type, prefix, key, value) => new Chunk(type, prefix, key, value));

    public Chunk(ChunkType type, string collectionKey, IByteBuffer key, IByteBuffer value)
    {
        ChunkType = type;
        CollectionKey = collectionKey;
        Key = key;
        Value = value;

        Wrapped = new Lazy<WrappedChunk>(() =>
        {
            var buffer = Unpooled.Buffer();
            CODEC.Write(buffer, this);
            var hash = Hasher.Crc32(buffer);
            return new WrappedChunk(hash, buffer);
        });
    }

    // (obviously byte buffers/arraays and strings are length prefixed)

    // Example: Meta type
    // [chunk size][ChunkType.Meta][__meta][empty][empty][9048093]

    // Example: Record
    //[chunk size][ChunkType.Record][person][{byte data}][{ byte data}][4890345]

    // Example: Delete
    //[chunk size][ChunkType.Delete][person][{byte data}][empty][4890345]
}
