// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;

namespace Nocturne.Database.WAL;

public sealed record LogRecord(long Lsn, long TransactionId, LogRecord.Type RecordType, int PageId, int Offset, byte[] OldValue, byte[] NewValue)
{
    public int Size => 8 + 4 + 4 + 4 + (4 + OldValue.Length) + (4 + NewValue.Length);

    public static readonly IBinaryCodec<LogRecord> CODEC = BinaryCodecs.For<LogRecord>()
        .Field(BinaryCodecs.LONG, l => l.Lsn)
        .Field(BinaryCodecs.LONG, l => l.TransactionId)
        .Field(BinaryCodecs.Enum<Type>(), l => l.RecordType)
        .Field(BinaryCodecs.INT, l => l.PageId)
        .Field(BinaryCodecs.INT, l => l.Offset)
        .Field(BinaryCodecs.BYTE_ARRAY, l => l.OldValue)
        .Field(BinaryCodecs.BYTE_ARRAY, l => l.NewValue)
        .Build((lsn, trans, type, id, offset, oldV, newV) => new LogRecord(lsn, trans, type, id, offset, oldV, newV));

    public enum Type
    {
        Begin,
        Commit,
        Abort,
        Update,
        Checkpoint
    }
}
