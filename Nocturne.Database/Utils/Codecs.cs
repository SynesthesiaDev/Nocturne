// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using DotNetty.Buffers;
using Faster.Map.Core;

namespace Nocturne.Database.Utils;

public static class Codecs
{

    public class BlitzMapBinaryCodec<TKey, TValue>(IBinaryCodec<TKey> keyCodec, IBinaryCodec<TValue> valueCodec) : IBinaryCodec<BlitzMap<TKey, TValue>>
    {
        public void Write(IByteBuffer buffer, BlitzMap<TKey, TValue> value)
        {
            BinaryCodecs.VAR_INT.Write(buffer, value.Count);
            foreach (var map in value)
            {
                keyCodec.Write(buffer, map.Key);
                valueCodec.Write(buffer, map.Value);
            }
        }

        public BlitzMap<TKey, TValue> Read(IByteBuffer buffer)
        {
            var size = BinaryCodecs.VAR_INT.Read(buffer);
            var map = new BlitzMap<TKey, TValue>(size);

            for (int i = 0; i < size; i++) map.Insert(keyCodec.Read(buffer), valueCodec.Read(buffer));

            return map;
        }
    }
}
