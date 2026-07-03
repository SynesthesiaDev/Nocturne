// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using Faster.Map.Core;
using Nocturne.Database.Utils;

namespace Nocturne.Database.Extensions;

public static class BinaryCodecExtensions
{
    public static IBinaryCodec<BlitzMap<TKey, TValue>> BlitzMapTo<TKey, TValue>(this IBinaryCodec<TKey> keyCodec, IBinaryCodec<TValue> valueCodec)
    {
        return new Codecs.BlitzMapBinaryCodec<TKey, TValue>(keyCodec, valueCodec);
    }
}
