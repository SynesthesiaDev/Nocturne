// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DotNetty.Buffers;
using Nocturne.Database.API;

namespace Nocturne.Database.Tree;

public class LeafTreeNode : ITreeNode
{
    private const int max_keys = BPlusTree.ORDER - 1;

    public required List<IByteBuffer> Keys { get; set; }

    public ISplitResult Insert<TKey>(IByteBuffer key, IByteBuffer value, NocturneKeySerializer<TKey> keySerializer)
    {
        int index = 0;
        while (index < Keys.Count && keySerializer.Compare(Keys[index], key) < 0)
        {
            index++;
        }

        if (index < Keys.Count && keySerializer.Compare(Keys[index], key) == 0)
        {
            Values[index].Release();
            Values[index] = value.RetainedDuplicate();

            return ISplitResult.Update();
        }

        Keys.Insert(index, key.RetainedDuplicate());
        Values.Insert(index, value.RetainedDuplicate());

        if (Keys.Count <= max_keys)
            return ISplitResult.False();

        var mid = Keys.Count / 2;
        var sibling = new LeafTreeNode
        {
            Keys = [],
            Values = [],
            Next = null
        };

        int remainder = Keys.Count - mid;
        var keysToMove = Keys.GetRange(mid, remainder);
        var valuesToMove = Values.GetRange(mid, remainder);

        foreach (var k in keysToMove) k.Retain();
        foreach (var v in valuesToMove) v.Retain();

        sibling.Keys.AddRange(keysToMove);
        sibling.Values.AddRange(valuesToMove);

        foreach (var k in keysToMove) k.Release();
        foreach (var v in valuesToMove) v.Release();

        Keys.RemoveRange(mid, remainder);
        Values.RemoveRange(mid, remainder);

        sibling.Next = Next;
        Next = sibling;

        return ISplitResult.True(sibling, sibling.Keys[0]);
    }

    public required List<IByteBuffer> Values { get; set; }
    public required LeafTreeNode? Next { get; set; }
}
