// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Codon.Binary;
using DotNetty.Buffers;

namespace Nocturne.Database.API;

public interface INocturneSerializer<T>
{
    T Read(IByteBuffer buffer);
    void Write(IByteBuffer buffer, T value);

}

public static class NocturneSerializer
{
    public static INocturneSerializer<T> For<T>(Func<IByteBuffer, T> read, Action<IByteBuffer, T> write) => new InlineNocturneSerializer<T>(read, write);
    public static INocturneSerializer<T> FromCodec<T>(IBinaryCodec<T> codec) => new CodecNocturneSerializer<T>(codec);

}

public class CodecNocturneSerializer<T>(IBinaryCodec<T> codec) : INocturneSerializer<T>
{
    public T Read(IByteBuffer buffer) => codec.Read(buffer);

    public void Write(IByteBuffer buffer, T value) => codec.Write(buffer, value);
}

public class InlineNocturneSerializer<T>(Func<IByteBuffer, T> read, Action<IByteBuffer, T> write) : INocturneSerializer<T>
{
    public T Read(IByteBuffer buffer) => read.Invoke(buffer);

    public void Write(IByteBuffer buffer, T value) => write.Invoke(buffer, value);
}
