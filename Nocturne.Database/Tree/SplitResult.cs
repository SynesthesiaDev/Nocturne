// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DotNetty.Buffers;

namespace Nocturne.Database.Tree;

public interface ISplitResult
{
    private static readonly None none = new None();
    private static readonly Replacement replacement = new Replacement();

    static None False() => none;
    static Replacement Update() => replacement;
    static Split True(ITreeNode newNode, IByteBuffer promotedKey) => new Split(newNode, promotedKey);

    record Split(ITreeNode NewNode, IByteBuffer PromotedKey) : ISplitResult;

    record None : ISplitResult;

    record Replacement : ISplitResult;
}
