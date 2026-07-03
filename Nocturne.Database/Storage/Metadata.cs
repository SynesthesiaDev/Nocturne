// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;

namespace Nocturne.Database.Storage;

public record Metadata(int NocturneVersion, int SchemaVersion, long FileCreatedUtc, long LastCompactedUtc)
{
    public const int MAGIC_SIGNATURE = 0x4E4F4354;
    // public readonly IByteBuffer

    // public static readonly int CRC32Hash =

    public static readonly IBinaryCodec<Metadata> CODEC = BinaryCodecs.For<Metadata>()
        .Field(BinaryCodecs.VAR_INT, m => m.NocturneVersion)
        .Field(BinaryCodecs.VAR_INT, m => m.SchemaVersion)
        .Field(BinaryCodecs.LONG, m => m.FileCreatedUtc)
        .Field(BinaryCodecs.LONG, m => m.LastCompactedUtc)
        .Build((version, schema, file, compact) => new Metadata(version, schema, file, compact));

}
