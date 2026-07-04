// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Serilog;

namespace Nocturne.Database.Exceptions;

public class SchemaMigrationRequiredException(string collectionKey, int oldSchemaVersion, int newSchemaVersion) : Exception
{
    public static void Throw(string collectionKey, int oldSchemaVersion, int newSchemaVersion)
    {
        Log.Error(" ");
        Log.Error("Migration required for collection {collectionKey} (old: {oldSchemaVersion} < {newSchemaVersion})", collectionKey, oldSchemaVersion, newSchemaVersion);
        Log.Error(" ");
        throw new SchemaMigrationRequiredException(collectionKey, oldSchemaVersion, newSchemaVersion);
    }

    public override string Message => $"Migration required for collection {collectionKey} (old: {oldSchemaVersion}, new: {newSchemaVersion})";
}
