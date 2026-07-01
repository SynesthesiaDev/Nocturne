// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Serilog;

namespace Nocturne.Database.WAL;

public sealed class Transaction
{
    private static long txIdCounter;

    public long Id { get; } = Interlocked.Increment(ref txIdCounter);
    public State TransactionState { get; private set; } = State.Active;

    private readonly LockManager lockManager;
    private readonly WriteAheadLog wal;
    private readonly BufferPool bufferPool;

    private readonly Stack<LocalUpdateRecord> localHistory = new();

    public Transaction(LockManager lockManager, WriteAheadLog wal, BufferPool bufferPool)
    {
        this.lockManager = lockManager;
        this.wal = wal;
        this.bufferPool = bufferPool;

        this.wal.Append(Id, LogRecord.Type.Begin);
    }

    public async Task WriteAsync(int pageId, int offset, byte[] newValue)
    {
        if (TransactionState != State.Active)
            throw new InvalidOperationException("Transaction is no longer active");

        Log.Information("Transaction ({id}): {pageId}, {offset}, {newValue}", Id, pageId, offset, newValue.Length);
        await lockManager.AcquireLockAsync(Id, pageId, PageLock.Mode.Exclusive);

        var page = bufferPool.Pin(pageId);

        try
        {
            byte[] oldValue = new byte[newValue.Length];
            page.Data.GetBytes(offset, oldValue, 0, newValue.Length);

            wal.Append(Id, LogRecord.Type.Update, pageId, offset, oldValue, newValue);

            page.WriteData(offset, newValue);

            localHistory.Push(new LocalUpdateRecord(pageId, offset, oldValue));
        }
        finally
        {
            bufferPool.Unpin(pageId, isDirty: true);
        }
    }

    public void Commit()
    {
        if (TransactionState != State.Active) return;
        Log.Information("Transaction ({id}): Commit", Id);

        wal.Append(Id, LogRecord.Type.Commit);
        TransactionState = State.Commited;

        lockManager.ReleaseAll(Id);
    }

    public void Abort()
    {
        if(TransactionState != State.Active) return;

        // undo!
        while(localHistory.Count > 0)
        {
            var update = localHistory.Pop();
            var page = bufferPool.Pin(update.PageId);
            try
            {
                page.WriteData(update.Offset, update.OldValue);
            }
            finally
            {
                bufferPool.Unpin(update.PageId, isDirty: true);
            }
        }

        Log.Information("Transaction ({id}): Abort", Id);
        wal.Append(Id, LogRecord.Type.Abort);
        TransactionState = State.Aborted;
        lockManager.ReleaseAll(Id);
    }

    public record LocalUpdateRecord(int PageId, int Offset, byte[] OldValue);

    public enum State
    {
        Active,
        Commited,
        Aborted
    }
}
