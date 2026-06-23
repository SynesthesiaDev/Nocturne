// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using DotNetty.Buffers;

namespace Nocturne.Database;

public record Page(int PageVersion, int Id, Page.Type PageType, bool Reserved, short FreeOffset, int NextPage, uint Checksum, IByteBuffer Data)
{
    public const int SIZE = 4096;
    public const int HEADER_ALLOC = 64;
    public const int DATA_SIZE = SIZE - HEADER_ALLOC;

    private const int v1_header_bytes = 4 + 4 + 2 + 4 + 4;
    private const int v2_header_bytes = 4 + 4 + 4 + 1 + 2 + 4 + 4;

    public int PinCount { get; set; }
    public bool IsDirty { get; set; }

    public void WriteData(int offset, byte[] data) => Data.SetBytes(offset, data, 0, data.Length);

    public enum Type
    {
        Free,
        Header,
        Leaf,
        Internal,
        Overflow
    }

    private static readonly IBinaryCodec<Page> header_v1_codec = BinaryCodecs.For<Page>()
        .Field(BinaryCodecs.INT, p => p.PageVersion)
        .Field(BinaryCodecs.INT, p => p.Id)
        .Field(BinaryCodecs.SHORT, p => p.FreeOffset)
        .Field(BinaryCodecs.INT, p => p.NextPage)
        .Field(BinaryCodecs.UINT, p => p.Checksum)
        .Build((version, id, offset, next, checksum) => new Page(version, id, Type.Free, false, offset, next, checksum, Unpooled.Empty));

    private static readonly IBinaryCodec<Page> header_v2_codec = BinaryCodecs.For<Page>()
        .Field(BinaryCodecs.INT, p => p.PageVersion)
        .Field(BinaryCodecs.INT, p => p.Id)
        .Field(BinaryCodecs.Enum<Type>(), p => p.PageType)
        .Field(BinaryCodecs.BOOLEAN, p => p.Reserved)
        .Field(BinaryCodecs.SHORT, p => p.FreeOffset)
        .Field(BinaryCodecs.INT, p => p.NextPage)
        .Field(BinaryCodecs.UINT, p => p.Checksum)
        .Build((version, id, type, reserved, offset, next, checksum) => new Page(version, id, type, reserved, offset, next, checksum, Unpooled.Empty));

    public static readonly IBinaryCodec<Page> CODEC = BinaryCodecs.Custom<Page>(
        (buffer, page) =>
        {
            switch (page.PageVersion)
            {
                case 1: header_v1_codec.Write(buffer, page); break;
                case 2: header_v2_codec.Write(buffer, page); break;
                default: throw new InvalidOperationException("Unknown page version");
            }

            var headerBytes = page.PageVersion switch
            {
                1 => v1_header_bytes,
                2 => v2_header_bytes,
                _ => throw new InvalidOperationException("Unknown page version")
            };

            buffer.WriteZero(HEADER_ALLOC - headerBytes);
            buffer.WriteBytes(page.Data);
        },
        buffer =>
        {
            buffer.MarkReaderIndex();
            var version = buffer.ReadInt();
            buffer.ResetReaderIndex();

            var page = version switch
            {
                1 => header_v1_codec.Read(buffer),
                2 => header_v2_codec.Read(buffer),
                _ => throw new InvalidOperationException("Unknown page version")
            };

            buffer.SetReaderIndex(HEADER_ALLOC);
            var data = buffer.ReadBytes(DATA_SIZE);

            return page with { Data = data };
        });
}
