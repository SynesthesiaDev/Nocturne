// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DotNetty.Buffers;
using Nocturne.Database.API;

namespace Nocturne.Database.Tree;

public class BPlusTree : IDisposable
{
    public const int ORDER = 10;

    public bool IsDisposed { get; private set; }

    public long Count { get; private set; }

    private ITreeNode root;

    private readonly INodeProvider provider;
    private readonly NocturneDatabase databaseContext;

    public BPlusTree(int rootPageId, INodeProvider provider, NocturneDatabase databaseContext)
    {
        this.provider = provider;
        this.databaseContext = databaseContext;

        if (databaseContext.DiskManager.PageCount == 0)
        {
            root = new LeafTreeNode
            {
                PageId = rootPageId,
                Keys = [],
                Values = [],
                NextPageId = 0
            };

            provider.SaveNode(root);
        }
        else
        {
            root = provider.GetNode(rootPageId);
        }
    }

    public IByteBuffer? Search<TKey>(IByteBuffer keyBuffer, NocturneKeySerializer<TKey> keySerializer)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, "Tree is already disposed");
        ITreeNode current = root;

        while (current is InternalTreeNode internalNode)
        {
            int i = findChildIndex(internalNode, keyBuffer, keySerializer);
            current = provider.GetNode(internalNode.ChildPageIds[i]);
        }

        var (index, equals) = keySerializer.FindEquals(current.Keys, keyBuffer);
        return equals ? (current as LeafTreeNode)!.Values[index] : null;
    }

    public void Insert<TKey>(IByteBuffer keyBuffer, IByteBuffer valueBuffer, NocturneKeySerializer<TKey> keySerializer)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, "Tree is already disposed");
        var result = root.Insert(keyBuffer, valueBuffer, keySerializer, provider);

        if (result is ISplitResult.Split splitResult)
        {
            provider.SaveNode(root);
            provider.SaveNode(splitResult.NewNode);

            var newRoot = new InternalTreeNode
            {
                PageId = provider.AllocatePage(),
                Keys = [splitResult.PromotedKey],
                ChildPageIds = [root.PageId, splitResult.NewNode.PageId]
            };

            root = newRoot;
            provider.SaveNode(root);
            databaseContext.UpdateRootPageId(newRoot.PageId);

            if (result is not ISplitResult.Replacement) Count++;
        }
        else
        {
            provider.SaveNode(root);
        }
    }

    public bool Delete<TKey>(IByteBuffer key, NocturneKeySerializer<TKey> keySerializer)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, "Tree is already disposed");
        ITreeNode current = root;
        while (current is InternalTreeNode internalNode)
        {
            int i = findChildIndex(internalNode, key, keySerializer);
            current = provider.GetNode(internalNode.ChildPageIds[i]);
        }

        var leaf = (LeafTreeNode)current;
        var (index, equals) = keySerializer.FindEquals(leaf.Keys, key);

        if (!equals) return false;

        Count--;
        leaf.Keys[index].Release();
        leaf.Values[index].Release();

        leaf.Keys.RemoveAt(index);
        leaf.Values.RemoveAt(index);
        return true;
    }

    public IEnumerable<IByteBuffer> IterateAllValues()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, "Tree is already disposed");
        var current = root;

        while (current is InternalTreeNode internalNode)
        {
            current = provider.GetNode(internalNode.ChildPageIds[0]);
        }

        LeafTreeNode? leaf = current as LeafTreeNode;

        while (leaf != null)
        {
            foreach (var value in leaf.Values)
            {
                yield return value.RetainedDuplicate();
            }

            leaf = leaf.GetNext(provider);
        }
    }

    public IEnumerable<IByteBuffer> IterateAllKeys()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, "Tree is already disposed");
        var current = root;

        while (current is InternalTreeNode internalNode)
        {
            current = provider.GetNode(internalNode.ChildPageIds[0]);
        }

        LeafTreeNode? leaf = current as LeafTreeNode;

        while (leaf != null)
        {
            foreach (var value in leaf.Keys)
            {
                yield return value.RetainedDuplicate();
            }

            leaf = leaf.GetNext(provider);
        }
    }

    public IEnumerable<KeyValuePair<IByteBuffer, IByteBuffer>> IterateKeyAndValuePairs()
    {
        ObjectDisposedException.ThrowIf(IsDisposed, "Tree is already disposed");
        var current = root;

        while (current is InternalTreeNode internalNode)
        {
            current = (InternalTreeNode)provider.GetNode(internalNode.ChildPageIds[0]);
        }

        LeafTreeNode? leaf = current as LeafTreeNode;

        while (leaf != null)
        {
            for (var i = 0; i < leaf.Values.Count; i++)
            {
                var keyBuffer = leaf.Keys[i];
                var valueBuffer = leaf.Values[i];

                yield return new KeyValuePair<IByteBuffer, IByteBuffer>(keyBuffer.RetainedDuplicate(), valueBuffer.RetainedDuplicate());
            }

            leaf = leaf.GetNext(provider);
        }
    }

    private static int findChildIndex<TKey>(InternalTreeNode node, IByteBuffer target, NocturneKeySerializer<TKey> keySerializer)
    {
        var i = 0;
        while (i < node.Keys.Count && keySerializer.Compare(target, node.Keys[i]) >= 0)
            i++;

        return i;
    }

    public void Clear()
    {
        Count = 0;
        ITreeNode current = root;
        while (current is InternalTreeNode internalNode)
        {
            current = provider.GetNode(internalNode.ChildPageIds[0]);
        }

        LeafTreeNode? leaf = current as LeafTreeNode;
        while (leaf != null)
        {
            foreach (var k in leaf.Keys) k.Release();
            foreach (var v in leaf.Values) v.Release();

            leaf.Keys.Clear();
            leaf.Values.Clear();
            leaf = leaf.GetNext(provider);
        }

        root = new LeafTreeNode
        {
            Keys = [],
            Values = []
        };
    }

    public void Dispose()
    {
        if (IsDisposed) return;
        IsDisposed = true;

        Count = 0;
        ITreeNode current = root;
        while (current is InternalTreeNode internalNode)
        {
            current = provider.GetNode(internalNode.ChildPageIds[0]);
        }

        LeafTreeNode? leaf = current as LeafTreeNode;
        while (leaf != null)
        {
            foreach (var k in leaf.Keys) k.Release();
            foreach (var v in leaf.Values) v.Release();

            leaf.Keys.Clear();
            leaf.Values.Clear();
            leaf = leaf.GetNext(provider);
        }
    }
}
