// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DotNetty.Buffers;
using Nocturne.Database.API;

namespace Nocturne.Database;

public class BPlusTree
{
    public IByteBuffer Search<TKey>(IByteBuffer keyBuffer, NocturneKeySerializer<TKey> keySerializer)
    {
        throw new NotImplementedException();
    }

    public IEnumerable<IByteBuffer> IterateAllValues()
    {
        throw new NotImplementedException();
    }
}
