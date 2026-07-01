// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace Nocturne.Database.Tree;

public interface INodeProvider
{
    ITreeNode GetNode(int pageId);
    void SaveNode(ITreeNode node);
    int AllocatePage();
}
