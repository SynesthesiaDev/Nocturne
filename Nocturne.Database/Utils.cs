// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Serilog;

namespace Nocturne.Database;

public static class Utils
{
    public static void DumpDatabaseStructure(NocturneDatabase db)
    {
        Log.Information("=== RAW DATABASE FILE DUMP ===");
        Log.Information("Header Root Page ID: {RootPageId}", db.Header.RootPageId);
        Log.Information("Disk Manager Page Count: {PageCount}", db.DiskManager.PageCount);

        // Read directly from the disk file manager to see what's actually persisted
        for (int i = 1; i <= db.DiskManager.PageCount; i++)
        {
            try
            {
                var page = db.DiskManager.ReadPage(i);
                Log.Information("--- Page #{Id} | Type: {Type} ---", page.Id, page.PageType);

                // Peek at the raw bytes inside the page data buffer
                byte[] rawBytes = page.Data.Array;
                int lengthToPrint = Math.Min(128, page.Data.ReadableBytes);

                // Simple hex + ASCII preview helper
                var hexString = BitConverter.ToString(rawBytes, page.Data.ReaderIndex, lengthToPrint);
                Log.Debug("Raw Hex (First {Len} bytes):\n{Hex}", lengthToPrint, hexString);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to read Page #{Id}: {Message}", i, ex.Message);
            }
        }
        Log.Information("==============================");
    }
}
