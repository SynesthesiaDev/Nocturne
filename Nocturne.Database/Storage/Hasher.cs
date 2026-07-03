// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DotNetty.Buffers;

namespace Nocturne.Database.Storage;

public sealed class Hasher
{
    public static uint Crc32(IByteBuffer buffer)
    {
        buffer.MarkReaderIndex();

        var bytes = new byte[buffer.ReadableBytes];
        buffer.GetBytes(buffer.ReaderIndex, bytes);

        var crc32 = System.IO.Hashing.Crc32.HashToUInt32(bytes);

        buffer.ResetReaderIndex();
        return crc32;
    }
}
