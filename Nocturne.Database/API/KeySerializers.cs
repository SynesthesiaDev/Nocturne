// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using DotNetty.Buffers;

namespace Nocturne.Database.API;

public sealed class KeySerializers
{
    public static readonly NocturneKeySerializer<string> STRING = new InlineKeySerializer<string>
    (
        (buffer, str) => BinaryCodecs.STRING.Write(buffer, str),
        (buffer) => BinaryCodecs.STRING.Read(buffer),
        string.CompareOrdinal
    );

    public static readonly NocturneKeySerializer<int> INT = new InlineKeySerializer<int>
    (
        (buffer, str) => BinaryCodecs.INT.Write(buffer, str),
        (buffer) => BinaryCodecs.INT.Read(buffer),
        (left, right) => left.CompareTo(right)
    );

    public static readonly NocturneKeySerializer<long> LONG = new InlineKeySerializer<long>
    (
        (buffer, str) => BinaryCodecs.LONG.Write(buffer, str),
        (buffer) => BinaryCodecs.LONG.Read(buffer),
        (left, right) => left.CompareTo(right)
    );

    public static readonly NocturneKeySerializer<Guid> GUID = new InlineKeySerializer<Guid>
    (
        (buffer, str) => BinaryCodecs.GUID.Write(buffer, str),
        (buffer) => BinaryCodecs.GUID.Read(buffer),
        (left, right) => left.CompareTo(right)
    );

    internal class InlineKeySerializer<T>(Action<IByteBuffer, T> read, Func<IByteBuffer, T> write, Func<T, T, int> compare) : NocturneKeySerializer<T>
    {
        public override T Read(IByteBuffer buffer) => write.Invoke(buffer);

        public override void Write(IByteBuffer buffer, T value) => read.Invoke(buffer, value);

        protected override int CompareValues(T left, T right) => compare.Invoke(left, right);
    }
}
