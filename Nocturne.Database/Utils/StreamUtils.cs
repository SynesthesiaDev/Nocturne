// Copyright (c) 2026 SynesthesiaDev <synesthesiadev@proton.me>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace Nocturne.Database.Utils;

public static class StreamUtils
{
    private const int segment_bits = 127 /*0x7F*/;
    private const int continue_bit = 128 /*0x80*/;

    public static int ReadVarInt(Stream stream)
    {
        int num1 = 0;
        for (int index = 0; index < 35; index += 7)
        {
            int num2 = stream.ReadByte();
            int num3 = num2 & sbyte.MaxValue;
            num1 |= num3 << index;
            if ((num2 & 128 /*0x80*/) == 0)
                return num1;
        }

        throw new InvalidDataException("VarInt is too long");
    }
}
