// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DotNetty.Buffers;

namespace Nocturne.Database.Tree;

public interface ISplitResult
{
    private static readonly None none = new None();

    static None False() => none;
    static Split True(ITreeNode newNode, IByteBuffer promotedKey) => new Split(newNode, promotedKey);

    record Split(ITreeNode NewNode, IByteBuffer PromotedKey) : ISplitResult;

    record None : ISplitResult;
}
