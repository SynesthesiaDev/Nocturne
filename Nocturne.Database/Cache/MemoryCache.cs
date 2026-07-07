// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using DotNetty.Buffers;

namespace Nocturne.Database.Cache;

public class MemoryCache : IDisposable
{
    private readonly Dictionary<string, Dictionary<KeyBytes, Entry>> collections;
    private readonly Lock lockObj = new();

    public static readonly IBinaryCodec<MemoryCache> CODEC = BinaryCodecs.For<MemoryCache>()
        .Field(BinaryCodecs.STRING.MapTo(KeyBytes.CODEC.MapTo(Entry.CODEC)), m => m.collections)
        .Build(collections => new MemoryCache(collections));

    public long DeadBytes { get; private set; }

    public int Size
    {
        get
        {
            int total = 0;
            lock (lockObj)
            {
                foreach (var outer in collections)
                    total += outer.Value.Count;
            }
            return total;
        }
    }

    public IEnumerable<KeyValuePair<KeyBytes, Entry>> AllEntries()
    {
        lock (lockObj)
        {
            var result = new List<KeyValuePair<KeyBytes, Entry>>(Size);
            result.AddRange(from outerEntry in collections from innerEntry in outerEntry.Value select new KeyValuePair<KeyBytes, Entry>(innerEntry.Key, innerEntry.Value));

            return result;
        }
    }

    public MemoryCache()
    {
        collections = new Dictionary<string, Dictionary<KeyBytes, Entry>>();
    }

    private MemoryCache(Dictionary<string, Dictionary<KeyBytes, Entry>> collections)
    {
        this.collections = collections;
    }

    public Entry? Get(string nestedKey, IByteBuffer key)
    {
        lock (lockObj)
        {
            if (!collections.ContainsKey(nestedKey)) return null;
            collections.TryGetValue(nestedKey, out var map);

            return map.TryGetValue(KeyBytes.FromBuffer(key), out var entry) ? entry : null;
        }
    }

    public int CountForCollection(string collectionKey)
    {
        lock (lockObj)
        {
            return collections.TryGetValue(collectionKey, out var map) ? map.Count : 0;
        }
    }

    public IDictionary<KeyBytes, Entry> GetAllForCollection(string collectionKey)
    {
        lock (lockObj)
        {
            var result = new Dictionary<KeyBytes, Entry>();
            if (!collections.TryGetValue(collectionKey, out var map)) return result;

            foreach (var innerEntry in map)
            {
                result[innerEntry.Key] = innerEntry.Value;
            }

            return result;
        }
    }


    public void Insert(string collectionKey, IByteBuffer key, Entry entry)
    {
        lock (lockObj)
        {
            if (!collections.TryGetValue(collectionKey, out var inner))
            {
                inner = new Dictionary<KeyBytes, Entry>();
                collections[collectionKey] = inner;
            }


            var keyBytes = KeyBytes.FromBuffer(key);
            if (inner.TryGetValue(keyBytes, out var old))
                DeadBytes += old.Length;

            inner[keyBytes] = entry;
        }
    }

    public void Remove(string collectionKey, IByteBuffer key)
    {
        lock (lockObj)
        {
            if (collections.TryGetValue(collectionKey, out var inner))
            {
                var keyBytes = KeyBytes.FromBuffer(key);
                if (inner.TryGetValue(keyBytes, out var old))
                    DeadBytes += old.Length;

                inner.Remove(keyBytes);
            }
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
        lock (lockObj)
        {
            collections.Clear();
            DeadBytes = 0;
        }
    }
}
