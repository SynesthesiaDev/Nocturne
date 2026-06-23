// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace Nocturne.Database.WAL;

public class PageLock
{
    public HashSet<long> HoldingTransactions { get; } = [];
    public Queue<Request> WaitingQueue { get; } = new();
    public record Request(long TransactionId, Mode Mode, TaskCompletionSource<bool> Tcs);

    public enum Mode
    {
        Shared,
        Exclusive
    }
}
