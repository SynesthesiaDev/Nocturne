// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Faster.Map.Core;
using Nocturne.Database.API;
using Nocturne.Database.Storage;

namespace Nocturne.Database.Utils;

public class MetaNocturneCollection(NocturneDatabase databaseContext) : NocturneCollection<string, Metadata>(meta_collection_name, meta_collection_schema_version, KEY_SERIALIZER, SERIALIZER, databaseContext)
{
    private const string meta_collection_name = "__meta";
    private const int meta_collection_schema_version = 0;
    private const string meta_key_name = "__meta.key.latest";
    public static readonly NocturneKeySerializer<string> KEY_SERIALIZER = KeySerializers.STRING;
    public static readonly INocturneSerializer<Metadata> SERIALIZER = NocturneSerializer.FromCodec(Metadata.CODEC);

    public void UpdateSchemaVersionFor<TKey, TValue>(NocturneCollection<TKey, TValue> collection, int newSchemaVersion) where TValue : class
    {
        var localCopy = new BlitzMap<string, int>();
        DatabaseContext.Metadata.SchemaVersions.Copy(localCopy);
        localCopy.InsertOrUpdate(collection.CollectionKey, newSchemaVersion);

        var meta = DatabaseContext.Metadata with { SchemaVersions = localCopy };
        Insert(meta);
    }

    public void Insert(Metadata metadata) => Insert(meta_key_name, metadata);

    public Metadata Get() => Find(meta_key_name);
}
