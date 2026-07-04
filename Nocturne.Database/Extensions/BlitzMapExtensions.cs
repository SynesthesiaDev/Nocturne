// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Faster.Map.Core;

namespace Nocturne.Database.Extensions;

public static class BlitzMapExtensions
{
    public static TValue? GetOrNull<TKey, TValue>(this BlitzMap<TKey, TValue> map, TKey key) where TValue : class
    {
        return map.Get(key, out var value) ? value : null;
    }

    public static TValue? GetOrNullStruct<TKey, TValue>(this BlitzMap<TKey, TValue> map, TKey key) where TValue : struct
    {
        return map.Get(key, out var value) ? value : null;
    }
}
