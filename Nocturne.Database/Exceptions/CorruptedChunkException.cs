// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace Nocturne.Database.Exceptions;

public class CorruptedChunkException(string message) : Exception
{
    public override string Message => message;
}
