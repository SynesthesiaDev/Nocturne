// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DotNetty.Buffers;

namespace Nocturne.Database.API;

public class NocturneCollection<TKey, TEntity>(NocturneKeySerializer<TKey> keySerializer, NocturneKeySerializer<TEntity> valueSerializer, NocturneDatabase databaseContext) where TEntity : class
{
    private readonly BPlusTree binaryTree = new BPlusTree();
    public readonly NocturneKeySerializer<TKey> KeySerializer = keySerializer;
    public readonly NocturneKeySerializer<TEntity> ValueSerializer = valueSerializer;
    public readonly NocturneDatabase DatabaseContext = databaseContext;

    public TEntity FindOrNull(TKey id)
    {
        var keyBuffer = Unpooled.Buffer();
        try
        {
            KeySerializer.Write(keyBuffer, id);
            var valueBuffer = binaryTree.Search(keyBuffer, KeySerializer);
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

    public IEnumerable<TEntity> FindAllWhere(Func<TEntity, bool> predicate)
    {
        foreach (var valueBuffer in binaryTree.IterateAllValues())
        {
            TEntity entity;
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

    public IEnumerable<TEntity> FindAll()
    {
        foreach (var valueBuffer in binaryTree.IterateAllValues())
        {
            TEntity entity;
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

    public void Transaction(Action<NocturneCollection<TKey, TEntity>> action)
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
