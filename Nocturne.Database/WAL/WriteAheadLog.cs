// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Channels;
using Codon.Binary;
using DotNetty.Buffers;
using Serilog;

namespace Nocturne.Database.WAL;

public sealed class WriteAheadLog : IDisposable
{
    private readonly Channel<LogRecord> channel = Channel.CreateUnbounded<LogRecord>();
    private readonly Task drainTask;
    private readonly DiskManager diskManager;

    private long lsn;

    public WriteAheadLog(DiskManager disk)
    {
        diskManager = disk;
        drainTask = Task.Run(drainLoop);
    }

    public long Append(long transactionId, LogRecord.Type recordType, int pageId, int offset, byte[] oldValue, byte[] newValue)
    {
        var recordLsn = Interlocked.Increment(ref lsn);
        var record = new LogRecord(transactionId, recordLsn, recordType, pageId, offset, oldValue, newValue);

        channel.Writer.TryWrite(record);
        return recordLsn;
    }

    public long Append(long transactionId, LogRecord.Type recordType) => Append(transactionId, recordType, 0, 0, [], []);

    public void Replay(BufferPool bufferPool)
    {
        Log.Information("Replaying WAL..");

        var logData = diskManager.ReadAllLogData();
        if (logData.Length == 0)
        {
            Log.Information("WAL is empty, nothing to replay");
            return;
        }

        var buffer = Unpooled.WrappedBuffer(logData);
        var allRecords = new List<LogRecord>();

        try
        {
            while (buffer.IsReadable())
            {
                var record = LogRecord.CODEC.Read(buffer);
                allRecords.Add(record);
            }
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Error parsing WAL stream. The log might have ended abruptly due to an un-flushed crash segment");
        }
        finally
        {
            buffer.Release();
        }

        var committedTransactions = new HashSet<long>();

        foreach (var record in allRecords)
        {
            if (record.RecordType == LogRecord.Type.Commit)
            {
                committedTransactions.Add(record.TransactionId);
            }
        }

        Log.Information("Redoing {Count} committed log records...", committedTransactions.Count);
        foreach (var record in allRecords)
        {
            if (record.RecordType == LogRecord.Type.Update)
            {
                if (committedTransactions.Contains(record.TransactionId))
                {
                    var page = bufferPool.Pin(record.PageId);
                    try
                    {
                        page.WriteData(record.Offset, record.NewValue);
                    }
                    finally
                    {
                        bufferPool.Unpin(record.PageId, isDirty: true);
                    }

                }
                else
                {
                    Log.Debug("Skipping uncommitted record from Tx {TxId}", record.TransactionId);
                }
            }
        }

        bufferPool.FlushAll();
        Log.Information("WAL Replay completed successfully.");
    }

    private async Task drainLoop()
    {
        await foreach (var record in channel.Reader.ReadAllAsync())
        {
            var buffer = Unpooled.Buffer();
            LogRecord.CODEC.Write(buffer, record);
            diskManager.AppendLog(buffer.ToByteArraySafe());
            buffer.Release();

            // flush only when there are no other operations
            if (channel.Reader.Count == 0)
                diskManager.FlushLog();
        }

        diskManager.FlushLog();
    }

    public void CreateCheckpoint(BufferPool bufferPool)
    {
        Log.Information("Creating database checkpoint..");

        bufferPool.FlushAll();

        var checkpointLsn = Append(0, LogRecord.Type.Checkpoint);

        while (channel.Reader.Count > 0)
        {
            Thread.Sleep(1);
        }
        diskManager.FlushLog();
        diskManager.Compact();

        Log.Information("Checkpoint completed successfully. WAL compacted at LSN {Lsn}.", checkpointLsn);
    }

    public void Dispose()
    {
        channel.Writer.TryComplete();
        try
        {
            drainTask.Wait();
        }
        catch (AggregateException exception)
        {
            // suppress
        }

        drainTask.Dispose();
    }
}
