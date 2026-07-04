// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using Faster.Map.Core;
using Nocturne.Database.Extensions;

namespace Nocturne.Database.Storage;

public record Metadata(int NocturneVersion, long FileCreatedUtc, long LastCompactedUtc, BlitzMap<string, int> SchemaVersions)
{
    public static readonly IBinaryCodec<Metadata> CODEC = BinaryCodecs.For<Metadata>()
        .Field(BinaryCodecs.VAR_INT, m => m.NocturneVersion)
        .Field(BinaryCodecs.LONG, m => m.FileCreatedUtc)
        .Field(BinaryCodecs.LONG, m => m.LastCompactedUtc)
        .Field(BinaryCodecs.STRING.BlitzMapTo(BinaryCodecs.VAR_INT), m => m.SchemaVersions)
        .Build((version, file, compact, update) => new Metadata(version, file, compact, update));
}
