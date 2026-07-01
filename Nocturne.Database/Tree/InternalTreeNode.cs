// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DotNetty.Buffers;
using Nocturne.Database.API;

namespace Nocturne.Database.Tree;

public class InternalTreeNode : ITreeNode
{
    private const int max_keys = BPlusTree.ORDER - 1;

    public required List<IByteBuffer> Keys { get; set; }

    public ISplitResult Insert<TKey>(IByteBuffer key, IByteBuffer value, NocturneKeySerializer<TKey> keySerializer)
    {
        int i = 0;
        while (i < Keys.Count && keySerializer.Compare(key, Keys[i]) >= 0) i++;

        var result = Children[i].Insert(key, value, keySerializer);
        if (result is ISplitResult.None) return ISplitResult.False();

        var splitResult = (result as ISplitResult.Split)!;
        Keys.Insert(i, splitResult.PromotedKey);
        Children.Insert(i + 1, splitResult.NewNode);

        if (Keys.Count <= max_keys) return ISplitResult.False();

        var mid = Keys.Count / 2;
        var sibling = new InternalTreeNode
        {
            Keys = [],
            Children = []
        };

        var promotedKey = Keys[mid];
        sibling.Keys.AddRange(Keys.GetRange(mid + 1, Keys.Count - (mid + 1)));
        sibling.Children.AddRange(Children.GetRange(mid + 1, Children.Count - (mid + 1)));

        Keys.RemoveRange(mid, Keys.Count - mid);
        Children.RemoveRange(mid + 1, Children.Count - (mid + 1));

        return ISplitResult.True(sibling, promotedKey);
    }

    public required List<ITreeNode> Children { get; set; }
}
