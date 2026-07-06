// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using DotNetty.Buffers;
using Faster.Map.Core;
using Nocturne.Database.Extensions;

namespace Nocturne.Database.Cache;

public class MemoryCache : IDisposable
{
    private readonly BlitzMap<string, BlitzMap<KeyBytes, Entry>> collections;

    public static readonly IBinaryCodec<MemoryCache> CODEC = BinaryCodecs.For<MemoryCache>()
        .Field(BinaryCodecs.STRING.BlitzMapTo(KeyBytes.CODEC.BlitzMapTo(Entry.CODEC)), m => m.collections)
        .Build(collections => new MemoryCache(collections));

    public long DeadBytes { get; private set; }

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

    public IEnumerable<KeyValuePair<KeyBytes, Entry>> AllEntries()
    {
        var result = new List<KeyValuePair<KeyBytes, Entry>>(Size);

        foreach (var outerEntry in collections)
        {
            foreach (var innerEntry in outerEntry.Value)
            {
                result.Add(new KeyValuePair<KeyBytes, Entry>(innerEntry.Key, innerEntry.Value));
            }
        }

        return result;
    }

    public MemoryCache()
    {
        collections = new BlitzMap<string, BlitzMap<KeyBytes, Entry>>();

    }

    private MemoryCache(BlitzMap<string, BlitzMap<KeyBytes, Entry>> collections)
    {
        this.collections = collections;
    }

    public Entry? Get(string nestedKey, IByteBuffer key)
    {
        if (!collections.Contains(nestedKey)) return null;
        collections.Get(nestedKey, out var map);

        return map.Get(KeyBytes.FromBuffer(key), out var entry) ? entry : null;
    }

    public int CountForCollection(string collectionKey) =>
        collections.Get(collectionKey, out var map) ? map.Count : 0;

    public IDictionary<KeyBytes, Entry> GetAllForCollection(string collectionKey)
    {
        var result = new Dictionary<KeyBytes, Entry>();
        if (!collections.Get(collectionKey, out var map)) return result;

        foreach (var innerEntry in map)
        {
            result[innerEntry.Key] = innerEntry.Value;
        }

        return result;
    }


    public void Insert(string collectionKey, IByteBuffer key, Entry entry)
    {
        if (!collections.Get(collectionKey, out var inner))
        {
            inner = new BlitzMap<KeyBytes, Entry>();
            collections.Insert(collectionKey, inner);
        }


        var keyBytes = KeyBytes.FromBuffer(key);
        if (inner.Get(keyBytes, out var old))
            DeadBytes += old.Length;

        inner.InsertOrUpdate(keyBytes, entry);

    }

    public void Remove(string collectionKey, IByteBuffer key)
    {
        if (collections.Get(collectionKey, out var inner))
        {
            var keyBytes = KeyBytes.FromBuffer(key);
            if (inner.Get(keyBytes, out var old))
                DeadBytes += old.Length;

            inner.Remove(keyBytes);
        }
    }

    public readonly struct Entry(long position, int length)
    {
        public readonly long Position = position;
        public readonly int Length = length;

        public static readonly IBinaryCodec<Entry> CODEC = BinaryCodecs.For<Entry>()
            .Field(BinaryCodecs.LONG, e => e.Position)
            .Field(BinaryCodecs.VAR_INT, e => e.Length)
            .Build((pos, len) => new Entry(pos, len));
    }


    public void Dispose()
    {
        Clear();
    }

    public void Clear()
    {
        collections.Clear();
        DeadBytes = 0;
    }
}
