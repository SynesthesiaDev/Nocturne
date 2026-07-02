// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DotNetty.Buffers;
using Nocturne.Database.Tree;

namespace Nocturne.Database.API;

public class NocturneCollection<TKey, TValue>(NocturneKeySerializer<TKey> keySerializer, INocturneSerializer<TValue> valueSerializer, NocturneDatabase databaseContext) where TValue : class
{
    private BPlusTree binaryTree = new BPlusTree(databaseContext.Header.RootPageId, new DiskNodeProvider(databaseContext.DiskManager, databaseContext.BufferPool), databaseContext);
    public readonly NocturneKeySerializer<TKey> KeySerializer = keySerializer;
    public readonly INocturneSerializer<TValue> ValueSerializer = valueSerializer;
    public readonly NocturneDatabase DatabaseContext = databaseContext;

    public IEnumerable<TKey> Keys
    {
        get
        {
            foreach (var keyBuffer in binaryTree.IterateAllKeys())
            {
                TKey key;

                try
                {
                    key = KeySerializer.Read(keyBuffer);
                }
                finally
                {
                    keyBuffer.Release();
                }

                yield return key;
            }
        }
    }

    public IEnumerable<TValue> Values
    {
        get
        {
            foreach (var kvp in binaryTree.IterateAllValues())
            {
                TValue value;

                try
                {
                    value = ValueSerializer.Read(kvp);
                }
                finally
                {
                    kvp.Release();
                }

                yield return value;
            }
        }
    }

    public IEnumerable<KeyValuePair<TKey, TValue>> KeyValuePairs
    {
        get
        {
            foreach (var kvp in binaryTree.IterateKeyAndValuePairs())
            {
                TKey key;
                TValue value;

                try
                {
                    key = KeySerializer.Read(kvp.Key);
                    value = ValueSerializer.Read(kvp.Value);
                }
                finally
                {
                    kvp.Key.Release();
                    kvp.Value.Release();
                }

                yield return new KeyValuePair<TKey, TValue>(key, value);
            }
        }
    }

    public long Count => binaryTree.Count;

    public void Insert(TKey key, TValue value)
    {
        var keyBuffer = Unpooled.Buffer();
        var valueBuffer = Unpooled.Buffer();
        try
        {
            KeySerializer.Write(keyBuffer, key);
            ValueSerializer.Write(valueBuffer, value);
            binaryTree.Insert(keyBuffer, valueBuffer, KeySerializer);
        }
        finally
        {
            keyBuffer.Release();
            valueBuffer.Release();
        }
    }

    public void Delete(TKey key)
    {
        var keyBuffer = Unpooled.Buffer();
        try
        {
            KeySerializer.Write(keyBuffer, key);
            binaryTree.Delete(keyBuffer, KeySerializer);
        }
        finally
        {
            keyBuffer.Release();
        }
    }

    public TValue? FindOrNull(TKey key)
    {
        var keyBuffer = Unpooled.Buffer();
        try
        {
            KeySerializer.Write(keyBuffer, key);
            var valueBuffer = binaryTree.Search(keyBuffer, KeySerializer);
            if (valueBuffer == null) return null;

            try
            {
                return ValueSerializer.Read(valueBuffer);
            }
            finally
            {
                valueBuffer.Release();
            }
        }
        finally
        {
            keyBuffer.Release();
        }
    }

    public TValue Find(TKey key) => FindOrNull(key) ?? throw new KeyNotFoundException();

    public TValue FindOrAdd(TKey key, Func<TKey, TValue> valueFactory)
    {
        var existing = FindOrNull(key);
        if (existing != null) return existing;

        var newValue = valueFactory.Invoke(key);
        Insert(key, newValue);
        return newValue;
    }

    public bool ContainsKey(TKey key)
    {
        var keyBuffer = Unpooled.Buffer();
        try
        {
            KeySerializer.Write(keyBuffer, key);
            var valueBuffer = binaryTree.Search(keyBuffer, KeySerializer);
            if (valueBuffer == null) return false;

            valueBuffer.Release();
            return true;
        }
        finally
        {
            keyBuffer.Release();
        }
    }

    public IEnumerable<TValue> FindAllWhere(Func<TValue, bool> predicate)
    {
        foreach (var valueBuffer in binaryTree.IterateAllValues())
        {
            TValue entity;
            try
            {
                var value = ValueSerializer.Read(valueBuffer);
                entity = value;
            }
            finally
            {
                valueBuffer.Release();
            }

            if (predicate.Invoke(entity))
            {
                yield return entity;
            }
        }
    }

    public void Compact()
    {
        Transaction(_ =>
        {
            var newTree = new BPlusTree(DatabaseContext.Header.RootPageId, new DiskNodeProvider(DatabaseContext.DiskManager, DatabaseContext.BufferPool), DatabaseContext);

            foreach (var kvp in binaryTree.IterateKeyAndValuePairs())
            {
                newTree.Insert(kvp.Key, kvp.Value, KeySerializer);

                kvp.Key.Release();
                kvp.Value.Release();
            }

            binaryTree.Dispose();
            binaryTree = newTree;
        });
    }

    public IEnumerable<TValue> FindAll()
    {
        foreach (var valueBuffer in binaryTree.IterateAllValues())
        {
            TValue entity;
            try
            {
                var value = ValueSerializer.Read(valueBuffer);
                entity = value;
            }
            finally
            {
                valueBuffer.Release();
            }

            yield return entity;
        }
    }

    public void Update(TKey key, Action<TValue> action)
    {
        var value = Find(key);
        action.Invoke(value);
        Insert(key, value);
    }

    public void Nuke() => Transaction(_ => binaryTree.Clear());

    public void Transaction(Action<NocturneCollection<TKey, TValue>> action)
    {
        var tx = DatabaseContext.BeginTransaction();
        try
        {
            action.Invoke(this);
            tx.Commit();
        }
        catch (Exception)
        {
            tx.Abort();
            throw;
        }
        finally
        {
            DatabaseContext.EndTransaction();
        }
    }
}
