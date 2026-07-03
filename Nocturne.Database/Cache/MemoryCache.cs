// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using DotNetty.Buffers;
using Faster.Map.Core;
using Nocturne.Database.Extensions;
using Serilog;

namespace Nocturne.Database.Cache;

public class MemoryCache
{
    private readonly BlitzMap<string, BlitzMap<KeyBytes, long>> collections;

    public static readonly IBinaryCodec<MemoryCache> CODEC = BinaryCodecs.For<MemoryCache>()
        .Field(BinaryCodecs.STRING.BlitzMapTo(KeyBytes.CODEC.BlitzMapTo(BinaryCodecs.LONG)), m => m.collections)
        .Build(collections => new MemoryCache(collections));

    public int Size
    {
        get
        {
            int total = 0;
            foreach (var outer in collections)
                total += outer.Value.Count;
            return total;
        }
    }
    public IEnumerable<KeyValuePair<KeyBytes, long>> AllEntries()
    {
        var result = new List<KeyValuePair<KeyBytes, long>>(Size);

        foreach (var outerEntry in collections)
        {
            foreach (var innerEntry in outerEntry.Value)
            {
                result.Add(new KeyValuePair<KeyBytes, long>(innerEntry.Key, innerEntry.Value));
            }
        }

        return result;
    }

    public MemoryCache()
    {
        collections = new BlitzMap<string, BlitzMap<KeyBytes, long>>();
    }

    private MemoryCache(BlitzMap<string, BlitzMap<KeyBytes, long>> collections)
    {
        this.collections = collections;
    }

    public long? Get(string nestedKey, IByteBuffer key)
    {
        if (!collections.Contains(nestedKey)) return null;
        collections.Get(nestedKey, out var map);

        return map.Get(KeyBytes.FromBuffer(key), out var entry) ? entry : null;
    }

    public void Insert(string collectionKey, IByteBuffer key, long valuePosition)
    {
        if (!collections.Get(collectionKey, out var inner))
        {
            inner = new BlitzMap<KeyBytes, long>();
            collections.Insert(collectionKey, inner);
            Log.Verbose("(MemoryCache) inner map for collection key {key} does not exist, creating one", collectionKey);
        }

        var keyBytes = KeyBytes.FromBuffer(key);
        inner.InsertOrUpdate(keyBytes, valuePosition);
        Log.Verbose("(MemoryCache) Updated memory cache for collection {key} with key hash {keyhash} bytes and stream position of {pos}", collectionKey, keyBytes.Hash, valuePosition);
    }

    public void Remove(string collectionKey, IByteBuffer key)
    {
        if (collections.Get(collectionKey, out var inner))
        {
            var keyBytes = KeyBytes.FromBuffer(key);
            inner.Remove(keyBytes);
            Log.Verbose("(MemoryCache) Removed key hash {hash} from memory cache for collection {key}", keyBytes.Hash, collectionKey);
        }
    }
}
