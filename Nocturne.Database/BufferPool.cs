// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Serilog;

namespace Nocturne.Database;

public class BufferPool(DiskManager disk, int capacity = 256) : IDisposable
{
    //TODO Optimize to zero alloc BlitzMap with custom struct CacheFrame(Page, NextIndex, PrevIndex). Do this after all the things is functional and all
    private readonly Dictionary<int, (Page Page, LinkedListNode<int> Node)> frames = new();

    private readonly LinkedList<int> lru = [];

    /// <summary>
    /// Fetch page into memory. MUST unpin when done
    /// </summary>
    public Page Pin(int id)
    {
        Log.Verbose("pin page with id {id}", id);
        if (frames.TryGetValue(id, out var entry))
        {
            Log.Verbose("cache hit");
            // move to front (most recently used)
            lru.Remove(entry.Node);
            lru.AddFirst(entry.Node);

            entry.Page.PinCount++;
            return entry.Page;
        }

        Log.Verbose("cache miss, reading from disk");
        if (frames.Count >= capacity)
        {
            Log.Warning("capacity reached - evicting first");
            Evict();
        }

        var page = disk.ReadPage(id);
        page.PinCount = 1;
        page.IsDirty = false;

        var node = lru.AddFirst(id);
        frames[id] = (page, node);

        return page;
    }

    public void UpdatePageInFrame(int pageId, Page updatedPage)
    {
        if (frames.TryGetValue(pageId, out var entry))
        {
            frames[pageId] = (updatedPage, entry.Node);
        }
        else
        {
            var node = lru.AddFirst(pageId);
            frames[pageId] = (updatedPage, node);
        }
    }

    public void Unpin(int id, bool isDirty = false)
    {
        if (!frames.TryGetValue(id, out var entry)) return;

        var newPinCount = Math.Max(0, entry.Page.PinCount - 1);
        var newIsDirty = entry.Page.IsDirty || isDirty;

        var updatedPage = entry.Page with
        {
            PinCount = newPinCount,
            IsDirty = newIsDirty
        };

        frames[id] = (updatedPage, entry.Node);
    }

    public void FlushAll()
    {
        foreach (var (page, _) in frames.Values)
        {
            if (!page.IsDirty) continue;
            disk.WritePage(page);
            page.IsDirty = false;
        }

        disk.Flush();
    }

    public void Evict()
    {
        var node = lru.Last;

        while (node is not null)
        {
            var (page, _) = frames[node.Value];
            if (page.PinCount == 0) break;
            node = node.Previous;
        }

        if (node is null)
            throw new InvalidOperationException("BufferPool is full and all pages are pinned. You may need to increase capacity (are you unpinning properly?)");

        var id = node.Value;
        var (evicted, _) = frames[id];

        if (evicted.IsDirty)
            disk.WritePage(evicted);

        frames.Remove(id);
        lru.Remove(node);

        evicted.Data.Release();
    }

    public void Dispose()
    {
        FlushAll();

        foreach (var entry in frames.Values)
        {
            entry.Page.Data.Release();
        }

        frames.Clear();
        lru.Clear();
    }
}
