// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using DotNetty.Buffers;

namespace Nocturne.Database.API;

public interface INocturneSerializer<T>
{
    T Read(IByteBuffer buffer);
    void Write(IByteBuffer buffer, T value);
}
