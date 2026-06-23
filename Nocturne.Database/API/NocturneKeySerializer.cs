// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DotNetty.Buffers;

namespace Nocturne.Database.API;

public abstract class NocturneKeySerializer<T> : INocturneSerializer<T>
{
    public abstract T Read(IByteBuffer buffer);
    public abstract void Write(IByteBuffer buffer, T value);

    public bool Compare(IByteBuffer left, IByteBuffer right)
    {
        left.MarkReaderIndex();
        right.MarkReaderIndex();

        try
        {
            var leftValue = Read(left);
            var rightValue = Read(left);

            return CompareValues(leftValue, rightValue);
        }
        finally
        {
            left.ResetReaderIndex();
            right.ResetReaderIndex();
        }
    }

    protected abstract bool CompareValues(T left, T right);
}
