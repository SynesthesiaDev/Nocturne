// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Faster.Map.Core;

namespace Nocturne.Database.WAL;

public sealed class LockManager
{
    private readonly BlitzMap<long, PageLock> pageLocks = new();
    private readonly BlitzMap<long, HashSet<long>> txOwnership = new();

    public async Task AcquireLockAsync(long txId, int pageId, PageLock.Mode mode)
    {
        TaskCompletionSource<bool> tsc = null!;
        PageLock.Request request = null!;

        lock (pageLocks)
        {
            if (!pageLocks.Get(pageId, out var pageLock))
            {
                pageLock = new PageLock();
                pageLocks[pageId] = pageLock;
            }

            if (canGrantLock(pageLock, txId, mode))
            {
                pageLock.HoldingTransactions.Add(txId);
                registerOwnership(txId, pageId);
                return;
            }

            tsc = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            request = new PageLock.Request(txId, mode, tsc);

            pageLock.WaitingQueue.Enqueue(new PageLock.Request(txId, mode, tsc));
        }

        using var cts = new CancellationTokenSource(500);

        await using(cts.Token.Register(() => tsc.TrySetCanceled()))
        {
            try
            {
                await tsc.Task;
            }
            catch (TaskCanceledException)
            {
                lock (pageLocks)
                {
                    if (pageLocks.Get(pageId, out var pageLock))
                    {
                        var filteredQueue = new Queue<PageLock.Request>(pageLock.WaitingQueue.Where(r => r != request));
                        pageLock.WaitingQueue.Clear();
                        foreach (var req in filteredQueue) pageLock.WaitingQueue.Enqueue(req);
                    }
                }
            }
        }

        throw new TimeoutException($"(Deadlock) Transaction {txId} timed out waiting for a {mode} lock on Page {pageId}");
    }

    private void processWaitingQueue(PageLock pageLock)
    {
        while (pageLock.WaitingQueue.Count > 0)
        {
            var nextRequest = pageLock.WaitingQueue.Peek();
            if (canGrantLock(pageLock, nextRequest.TransactionId, nextRequest.Mode))
            {
                pageLock.WaitingQueue.Dequeue();
                pageLock.HoldingTransactions.Add(nextRequest.TransactionId);
                registerOwnership(nextRequest.TransactionId, nextRequest.TransactionId);

                nextRequest.Tcs.SetResult(true);
            }
            else
            {
                break;
            }
        }
    }

    public void ReleaseAll(long txId)
    {
        lock (pageLocks)
        {
            if (!txOwnership.Get(txId, out var pages)) return;

            foreach (var pageId in pages)
            {
                if (!pageLocks.Get(pageId, out var pageLock)) continue;

                pageLock.HoldingTransactions.Remove(txId);

                processWaitingQueue(pageLock);
            }

            txOwnership.Remove(txId);
        }
    }

    private bool canGrantLock(PageLock pageLock, long txId, PageLock.Mode mode)
    {
        if (pageLock.HoldingTransactions.Count == 0) return true;
        if (pageLock.HoldingTransactions.Contains(txId) && pageLock.HoldingTransactions.Count == 1) return true;

        if (mode == PageLock.Mode.Shared)
            return true;

        return false;
    }

    private void registerOwnership(long txId, long pageId)
    {
        if (!txOwnership.Get(txId, out var pages))
        {
            pages = new HashSet<long>();
            txOwnership[txId] = pages;
        }

        pages.Add(pageId);
    }
}
