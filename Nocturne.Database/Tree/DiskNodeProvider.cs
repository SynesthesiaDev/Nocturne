// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace Nocturne.Database.Tree;

public class DiskNodeProvider(DiskManager diskManager, BufferPool bufferPool) : INodeProvider
{
    public ITreeNode GetNode(int pageId)
    {
        var page = bufferPool.Pin(pageId);
        try
        {
            if (page.PageType == Page.Type.Header)
                throw new InvalidOperationException("Trying to read database header as tree node");

            if (page.PageType == Page.Type.Leaf)
                return LeafTreeNode.CODEC.Read(page.Data);

            return InternalTreeNode.CODEC.Read(page.Data);
        }
        finally
        {
            bufferPool.Unpin(pageId, isDirty: false);
        }
    }

    public void SaveNode(ITreeNode node)
    {
        var page = bufferPool.Pin(node.PageId);
        try
        {
            page.Data.Clear();

            var updatedPage = page with
            {
                PageType = node is LeafTreeNode ? Page.Type.Leaf : Page.Type.Internal,
                PageVersion = SharedConstants.PAGE_VERSION,
            };

            if (node is LeafTreeNode leaf)
                LeafTreeNode.CODEC.Write(page.Data, leaf);
            else
            {
                InternalTreeNode.CODEC.Write(page.Data, (InternalTreeNode)node);
            }

            bufferPool.UpdatePageInFrame(page.Id, updatedPage);
        }
        finally
        {
            bufferPool.Unpin(node.PageId, isDirty: true);
        }
    }

    public int AllocatePage() => diskManager.AllocatePage();
}
