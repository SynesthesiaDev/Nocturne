using Codon.Binary;
using DotNetty.Buffers;
using Nocturne.Database.API;
using Nocturne.Database.WAL;
using Serilog;

namespace Nocturne.Database;

public class NocturneDatabase : IDisposable
{
    public required string FilePath { get; init; }
    public required int SchemaVersion { get; init; }
    public bool DeleteIfMigrationNeeded { get; init; } = false;
    public bool CompactOnLaunch { get; init; } = false;
    public int BufferPoolSize { get; init; } = 256;

    public string DirectoryPath => Path.GetDirectoryName(FilePath) ?? throw new InvalidOperationException("Invalid file path specified (cannot get directory name)");
    public string FileName => Path.GetFileNameWithoutExtension(FilePath) ?? throw new InvalidOperationException("Invalid file path specified (cannot get file name)");

    public string LogPath => Path.Join(DirectoryPath, $"{FileName}.log");

    public string LockPath => Path.Join(DirectoryPath, $"{FileName}.lock");

    public LockManager LockManager { get; private set; } = new LockManager();
    public WriteAheadLog WriteAheadLog { get; private set; } = null!;
    public DiskManager DiskManager { get; private set; } = null!;
    public BufferPool BufferPool { get; private set; } = null!;

    private readonly Lock txLock = new();

    private int activeTransactionCount;

    public DatabaseHeader Header { get; private set; } = null!;

    public void Open()
    {
        var directoryPath = Path.GetDirectoryName(FilePath)!;
        if (!Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        var isNewDatabase = !File.Exists(FilePath) || new FileInfo(FilePath).Length == 0;

        if (isNewDatabase)
        {
            Log.Debug("Database file doesn't exist or is empty, creating new one...");
            File.Create(FilePath).Close();
            File.Create(LogPath).Close();
        }

        if (isNewDatabase)
        {
            Header = new DatabaseHeader
            (
                HeaderVersion: SharedConstants.HEADER_VERSION,
                NocturneVersion: SharedConstants.DATABASE_VERSION,
                SchemaVersion: SchemaVersion, Transactions: 0,
                RootPageId: 1
            );

            initializeNewDatabaseFile();
        }
        else
        {
            readHeader();
        }

        DiskManager = new DiskManager(this);
        WriteAheadLog = new WriteAheadLog(DiskManager);
        BufferPool = new BufferPool(DiskManager, BufferPoolSize);

        Log.Verbose("Database Header: {header}", Header);
        if (CompactOnLaunch) Compact();
    }

    public void SaveHeader(DatabaseHeader newHeader)
    {
        lock (txLock)
        {
            if (activeTransactionCount > 0)
                throw new InvalidOperationException("Transactions are actively running, cannot update header");

            Header = newHeader;
            var buffer = Unpooled.Buffer(DatabaseHeader.HEADER_SIZE);

            try
            {
                DatabaseHeader.CODEC.Write(buffer, Header);
                DiskManager.WriteHeaderBytes(buffer.ToByteArraySafe());
                Log.Debug("Written new database header (Schema: {Schema}, TxCount: {Tx})", Header.SchemaVersion, Header.Transactions);
            }
            finally
            {
                buffer.Release();
            }
        }
    }

    private void initializeNewDatabaseFile()
    {
        var buffer = Unpooled.Buffer(DatabaseHeader.HEADER_SIZE);
        try
        {
            DatabaseHeader.CODEC.Write(buffer, Header);

            using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Write, FileShare.Read);
            stream.Write(buffer.ToByteArraySafe());
            stream.Flush(flushToDisk: true);
        }
        finally
        {
            buffer.Release();
        }
    }

    private void readHeader()
    {
        var data = new byte[DatabaseHeader.HEADER_SIZE];
        using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.ReadExactly(data, 0, data.Length);

        var buffer = Unpooled.WrappedBuffer(data);
        try
        {
            Header = DatabaseHeader.CODEC.Read(buffer);
        }
        catch (Exception exception)
        {
            Log.Error(exception, "Failed to read database header. Your database may be corrupt");
        }
        finally
        {
            buffer.Release();
        }

        if (Header.SchemaVersion == SchemaVersion) return;

        Log.Warning("Schema version mismatch (file {old} != current {new})", Header.SchemaVersion, SchemaVersion);
        if (DeleteIfMigrationNeeded)
        {
            DiskManager.Dispose();
            WriteAheadLog.Dispose();
            BufferPool.Dispose();

            File.Delete(FilePath);
            File.Delete(LogPath);

            Open();
        }
        else if (Header.SchemaVersion < SchemaVersion)
        {
            //TODO migrations
            throw new InvalidOperationException($"Database schema mismatch! File version is {Header.SchemaVersion}, but runtime requires {SchemaVersion} and no migrations were found");

            var migratedHeader = Header with { SchemaVersion = this.SchemaVersion };
            SaveHeader(migratedHeader);
        }
        else
        {
            throw new InvalidOperationException($"Cannot downgrade schema version");
        }
    }

    public Transaction BeginTransaction()
    {
        lock (txLock)
        {
            activeTransactionCount++;
            Log.Verbose("Begin Transaction (active: {active})", activeTransactionCount);

            return new Transaction(LockManager, WriteAheadLog, BufferPool);
        }
    }

    public void EndTransaction()
    {
        lock (txLock)
        {
            activeTransactionCount--;
            Log.Verbose("End Transaction (active: {active})", activeTransactionCount);
        }
    }

    public void Compact()
    {
        Log.Information("Stalling for compact...");

        lock (txLock)
        {
            while (activeTransactionCount > 0)
            {
                Thread.Sleep(5);
            }

            WriteAheadLog.CreateCheckpoint(BufferPool);
        }
    }

    public NocturneCollection<TKey, TValue> For<TKey, TValue>(NocturneKeySerializer<TKey> keySerializer, INocturneSerializer<TValue> valueSerializer) where TValue : class
    {
        return new NocturneCollection<TKey, TValue>(keySerializer, valueSerializer, this);
    }

    public void UpdateRootPageId(int newRootPageId)
    {
        Header = Header with { RootPageId = newRootPageId };
        SaveHeader(Header);
    }

    public void Dispose()
    {
        Log.Information("Closing database gracefully..");
        lock (txLock)
        {
            try
            {
                if (WriteAheadLog != null && BufferPool != null)
                    WriteAheadLog.CreateCheckpoint(BufferPool);

                if (Header != null && DiskManager != null)
                {
                    var finalHeader = Header with { Transactions = Header.Transactions };
                    SaveHeader(finalHeader);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during database checkpoint/header save during disposal");
            }
            finally
            {
                WriteAheadLog?.Dispose();
                BufferPool?.Dispose();
                DiskManager?.Dispose();
            }
        }

        Log.Information("Database closed safely.");
    }
}
