// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DotNetty.Buffers;
using Nocturne.Database.API;

namespace Nocturne.Database.Tree;

public class BPlusTree
{
    public const int ORDER = 10;

    private ITreeNode root = new LeafTreeNode
    {
        Next = null,
        Keys = [],
        Values = []
    };

    public IByteBuffer? Search<TKey>(IByteBuffer keyBuffer, NocturneKeySerializer<TKey> keySerializer)
    {
        ITreeNode current = root;

        while (current is InternalTreeNode internalNode)
        {
            int i = findChildIndex(internalNode, keyBuffer, keySerializer);
            current = internalNode.Children[i];
        }

        var (index, equals) = keySerializer.FindEquals(current.Keys, keyBuffer);
        return equals ? (current as LeafTreeNode)!.Values[index] : null;
    }

    public void Insert<TKey>(IByteBuffer keyBuffer, IByteBuffer valueBuffer, NocturneKeySerializer<TKey> keySerializer)
    {
        var result = root.Insert(keyBuffer, valueBuffer, keySerializer);

        if (result is ISplitResult.Split splitResult)
        {
            var newRoot = new InternalTreeNode
            {
                Keys = [splitResult.PromotedKey],
                Children = [root, splitResult.NewNode]
            };

            root = newRoot;
        }
    }

    public IEnumerable<IByteBuffer> IterateAllValues()
    {
        throw new NotImplementedException();
    }

    private static int findChildIndex<TKey>(InternalTreeNode node, IByteBuffer target, NocturneKeySerializer<TKey> keySerializer)
    {
        var i = 0;
        while (i < node.Keys.Count && keySerializer.Compare(target, node.Keys[i]) >= 0)
            i++;

        return i;
    }
}
