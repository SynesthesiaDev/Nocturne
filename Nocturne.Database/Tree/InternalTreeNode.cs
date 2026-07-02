// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using DotNetty.Buffers;
using Nocturne.Database.API;

namespace Nocturne.Database.Tree;

public class InternalTreeNode : ITreeNode
{
    private const int max_keys = BPlusTree.ORDER - 1;

    public int PageId { get; set; }

    public required List<int> ChildPageIds { get; set; }

    public required List<IByteBuffer> Keys { get; set; }

    public static readonly IBinaryCodec<InternalTreeNode> CODEC = BinaryCodecs.For<InternalTreeNode>()
        .Field(BinaryCodecs.INT, n => n.PageId)
        .Field(BinaryCodecs.INT.List(), n => n.ChildPageIds)
        .Field(BinaryCodecs.BYTE_BUFFER.List(), n => n.Keys)
        .Build((pageId, childs, keys) => new InternalTreeNode
        {
            PageId = pageId,
            ChildPageIds = childs,
            Keys = keys
        });

    public ISplitResult Insert<TKey>(IByteBuffer key, IByteBuffer value, NocturneKeySerializer<TKey> keySerializer, INodeProvider provider)
    {
        int index = 0;
        while (index < Keys.Count && keySerializer.Compare(key, Keys[index]) >= 0) index++;

        var childPageId = ChildPageIds[index];
        var childNode = provider.GetNode(childPageId);

        var result = childNode.Insert(key, value, keySerializer, provider);

        if (result is ISplitResult.Split splitResult)
        {
            Keys.Insert(index, splitResult.PromotedKey);
            ChildPageIds.Insert(index + 1, splitResult.NewNode.PageId);

            provider.SaveNode(splitResult.NewNode);
        }
        provider.SaveNode(this);

        if (Keys.Count <= max_keys) return ISplitResult.False();

        var sibling = new InternalTreeNode
        {
            PageId = provider.AllocatePage(),
            Keys = [],
            ChildPageIds = []
        };

        var mid = Keys.Count / 2;

        var promotedKey = Keys[mid];

        var keysToMove = Keys.GetRange(mid + 1, Keys.Count - (mid + 1));
        foreach (var k in keysToMove) k.Retain();
        sibling.Keys.AddRange(keysToMove);

        sibling.ChildPageIds.AddRange(ChildPageIds.GetRange(mid + 1, ChildPageIds.Count - (mid + 1)));

        foreach (var k in keysToMove) k.Release();

        Keys.RemoveRange(mid, Keys.Count - mid);
        ChildPageIds.RemoveRange(mid + 1, ChildPageIds.Count - (mid + 1));

        provider.SaveNode(sibling);
        provider.SaveNode(this);
        return ISplitResult.True(sibling, promotedKey);
    }

}
