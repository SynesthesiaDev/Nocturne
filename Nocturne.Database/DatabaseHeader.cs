// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;

namespace Nocturne.Database;

public record DatabaseHeader(int HeaderVersion, int NocturneVersion, int SchemaVersion, long Transactions, int RootPageId)
{
    public const int HEADER_SIZE = 4096;
    public const int MAGIC_SIGNATURE = 0x4E4F4354;

    // Changes:
    // v2 - Removed PageSize field
    // v3 - Added RootPageId

    private static readonly IBinaryCodec<DatabaseHeader> header_v1_codec = BinaryCodecs.For<DatabaseHeader>()
        .Field(BinaryCodecs.INT, d => d.HeaderVersion)
        .Field(BinaryCodecs.INT, d => d.NocturneVersion)
        .Field(BinaryCodecs.INT, d => d.SchemaVersion)
        .Field(BinaryCodecs.LONG, d => d.Transactions)
        .Field(BinaryCodecs.INT, _ => Page.SIZE)
        .Build((header, ver, schema, trans, _) => new DatabaseHeader(header, ver, schema, trans, 1));

    private static readonly IBinaryCodec<DatabaseHeader> header_v2_codec = BinaryCodecs.For<DatabaseHeader>()
        .Field(BinaryCodecs.INT, d => d.HeaderVersion)
        .Field(BinaryCodecs.INT, d => d.NocturneVersion)
        .Field(BinaryCodecs.INT, d => d.SchemaVersion)
        .Field(BinaryCodecs.LONG, d => d.Transactions)
        .Build((header, ver, schema, trans) => new DatabaseHeader(header, ver, schema, trans, 1));

    private static readonly IBinaryCodec<DatabaseHeader> header_v3_codec = BinaryCodecs.For<DatabaseHeader>()
        .Field(BinaryCodecs.INT, d => d.HeaderVersion)
        .Field(BinaryCodecs.INT, d => d.NocturneVersion)
        .Field(BinaryCodecs.INT, d => d.SchemaVersion)
        .Field(BinaryCodecs.LONG, d => d.Transactions)
        .Field(BinaryCodecs.INT, d => d.RootPageId)
        .Build((header, ver, schema, trans, root) => new DatabaseHeader(header, ver, schema, trans, root));


    public static readonly IBinaryCodec<DatabaseHeader> CODEC = BinaryCodecs.Custom<DatabaseHeader>
    (
        (buffer, header) =>
        {
            buffer.WriteInt(MAGIC_SIGNATURE);
            switch (header.HeaderVersion)
            {
                case 1: header_v1_codec.Write(buffer, header); break;
                case 2: header_v2_codec.Write(buffer, header); break;
                case 3: header_v3_codec.Write(buffer, header); break;
                default: throw new InvalidOperationException($"Unsupported header format: {header.HeaderVersion})");
            }

            int writtenBytes = buffer.WriterIndex;
            buffer.WriteZero(HEADER_SIZE - writtenBytes);
        },
        (buffer) =>
        {
            var magic = buffer.ReadInt();
            if (magic != MAGIC_SIGNATURE)
                throw new InvalidDataException("Invalid database file signature. Your database may be corrupt");

            buffer.MarkReaderIndex();
            var version = buffer.ReadInt();
            buffer.ResetReaderIndex();

            var header = version switch
            {
                1 => header_v1_codec.Read(buffer),
                2 => header_v2_codec.Read(buffer),
                3 => header_v3_codec.Read(buffer),
                _ => throw new InvalidOperationException($"Unknown database header version: {version}")
            };

            buffer.SetReaderIndex(HEADER_SIZE);
            return header;
        }
    );
}
