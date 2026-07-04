// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO.Hashing;
using System.Runtime.InteropServices;
using Codon.Binary;
using DotNetty.Buffers;

namespace Nocturne.Database.Cache;

[StructLayout(LayoutKind.Auto)]
public readonly struct KeyBytes : IEquatable<KeyBytes>
{
    public readonly byte[] Bytes;
    public readonly int Hash;

    public static readonly IBinaryCodec<KeyBytes> CODEC = BinaryCodecs.For<KeyBytes>()
        .Field(BinaryCodecs.ByteArray(), k => k.Bytes)
        .Field(BinaryCodecs.INT, k => k.Hash)
        .Build((bytes, hash) => new KeyBytes(bytes, hash));

    public IByteBuffer ToByteBuffer()
    {
        var buffer = Unpooled.Buffer();
        buffer.WriteBytes(Bytes);

        return buffer;
    }

    private KeyBytes(byte[] bytes, int hash)
    {
        Bytes = bytes;
        Hash = hash;
    }

    private KeyBytes(byte[] bytes)
    {
        this.Bytes = bytes;
        Hash = unchecked((int)XxHash32.HashToUInt32(bytes));
    }

    public static KeyBytes FromBuffer(IByteBuffer buffer)
    {
        var array = new byte[buffer.ReadableBytes];
        buffer.GetBytes(buffer.ReaderIndex, array);

        return new KeyBytes(array);
    }

    public bool Equals(KeyBytes other) => Bytes.AsSpan().SequenceEqual(other.Bytes);
    public override bool Equals(object? obj) => obj is KeyBytes kb && Equals(kb);
    public override int GetHashCode() => Hash;

    public static bool operator ==(KeyBytes left, KeyBytes right) => left.Equals(right);
    public static bool operator !=(KeyBytes left, KeyBytes right) => !(left == right);
}
