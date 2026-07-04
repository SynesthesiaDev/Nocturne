// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Nocturne.Database;

namespace Nocturne.Tests;

public abstract class NocturneTestBase
{
    protected NocturneDatabase Nocturne { get; private set; } = null!;
    private string dbPath = null!;

    protected virtual NocturneDatabase CreateDatabase(string filePath) =>
        new() { FilePath = filePath };

    [SetUp]
    public void BaseSetup()
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"nocturne-test-{Guid.NewGuid()}.db");
        Nocturne = CreateDatabase(dbPath);
        Nocturne.Open();
    }

    [TearDown]
    public void BaseTeardown()
    {
        Nocturne.Dispose();

        if (File.Exists(dbPath))
            File.Delete(dbPath);

        var tempCompactPath = dbPath + ".compact-tmp";
        if (File.Exists(tempCompactPath))
            File.Delete(tempCompactPath);
    }

    protected void SimulateRestart()
    {
        Nocturne.Dispose();
        Nocturne = CreateDatabase(dbPath);
        Nocturne.Open();
    }

    protected void CorruptFileWithGarbage(int garbageByteCount = 20)
    {
        Nocturne.Dispose();

        using (var stream = new FileStream(dbPath, FileMode.Append, FileAccess.Write))
        {
            var garbage = new byte[garbageByteCount];
            new Random().NextBytes(garbage);
            stream.Write(garbage);
        }

        Nocturne = CreateDatabase(dbPath);
        Nocturne.Open();
    }
}
