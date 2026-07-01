// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DotNetty.Buffers;
using Nocturne.Database.API;

namespace Nocturne.Database.Tree;

public interface ITreeNode
{
    List<IByteBuffer> Keys { get; set; }

    ISplitResult Insert<TKey>(IByteBuffer key, IByteBuffer value, NocturneKeySerializer<TKey> keySerializer);
}
