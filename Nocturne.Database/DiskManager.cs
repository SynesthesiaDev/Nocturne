// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Buffers;
using Codon.Binary;
using DotNetty.Buffers;
using Serilog;

namespace Nocturne.Database;

public class DiskManager : IDisposable
{
    public readonly NocturneDatabase Database;

    private readonly FileStream databaseStream;
    private FileStream logStream;

    public int PageCount { get; private set; }

    public DiskManager(NocturneDatabase database)
    {
        Database = database;
        databaseStream = new FileStream(database.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, Page.SIZE, FileOptions.RandomAccess);
        logStream = new FileStream(database.LogPath, FileMode.Append, FileAccess.Write, FileShare.Read, Page.SIZE, FileOptions.SequentialScan);

        PageCount = (int)(databaseStream.Length / Page.SIZE) - 1;
        if (PageCount < 0) PageCount = 0;
    }

    public Page ReadPage(int id)
    {
        var array = ArrayPool<byte>.Shared.Rent(Page.SIZE);
        databaseStream.Seek((long)(id + 1) * Page.SIZE, SeekOrigin.Begin);
        databaseStream.ReadExactly(array, 0, Page.SIZE);

        var page = Page.CODEC.Read(Unpooled.CopiedBuffer(array, 0, Page.SIZE));
        ArrayPool<byte>.Shared.Return(array);

        return page;
    }

    public void WritePage(Page page)
    {
        databaseStream.Seek(page.Id * Page.SIZE, SeekOrigin.Begin);
        var buffer = Unpooled.Buffer();
        try
        {
            Page.CODEC.Write(buffer, page);
            databaseStream.Write(buffer.ToByteArraySafe());
        }
        finally
        {
            buffer.Release();
        }
    }

    public int AllocatePage()
    {
        var id = PageCount++;
        databaseStream.SetLength((long)PageCount * Page.SIZE);

        Log.Verbose("Allocated page {id} (stream now lenght {len})", id, databaseStream.Length);

        return id;
    }

    public byte[] ReadAllLogData()
    {
        FlushLog();

        using var readStream = new FileStream(Database.LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var bytes = new byte[readStream.Length];
        readStream.ReadExactly(bytes, 0, bytes.Length);
        return bytes;
    }

    public void Flush() => databaseStream.Flush(flushToDisk: true);

    public void AppendLog(ReadOnlySpan<byte> entry) => logStream.Write(entry);

    public void FlushLog() => logStream.Flush(flushToDisk: true);

    public void WriteHeaderBytes(byte[] headerBytes)
    {
        using var stream = new FileStream(Database.FilePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        stream.Seek(0, SeekOrigin.Begin);
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Flush(flushToDisk: true);
    }

    public void Compact()
    {
        logStream.Flush(flushToDisk: true);
        logStream.Close();

        File.WriteAllBytes(Database.LogPath, []);
        logStream = new FileStream(Database.LogPath, FileMode.Append, FileAccess.Write, FileShare.Read, Page.SIZE, FileOptions.SequentialScan);
    }

    public void Dispose()
    {
        databaseStream.Close();
        logStream.Close();
        GC.SuppressFinalize(this);
    }
}
