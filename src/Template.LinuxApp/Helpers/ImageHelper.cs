namespace Template.LinuxApp.Helpers;

public static class ImageHelper
{
    public static unsafe void ConvertYuyvToRgba(ReadOnlySpan<byte> yuyv, Span<byte> rgba)
    {
        var count = Math.Min(yuyv.Length / 4, rgba.Length / 8);
        fixed (byte* yuyvPointer = yuyv)
        {
            fixed (byte* rgbaPointer = rgba)
            {
                var src = yuyvPointer;
                var dst = rgbaPointer;
                for (var i = 0; i < count; i++)
                {
                    var c0 = 298 * (src[0] - 16);
                    var c1 = 298 * (src[2] - 16);
                    var d = src[1] - 128;
                    var e = src[3] - 128;

                    var r = (409 * e) + 128;
                    var g = -(100 * d) - (208 * e) + 128;
                    var b = (516 * d) + 128;

                    dst[0] = Clamp((c0 + r) >> 8);
                    dst[1] = Clamp((c0 + g) >> 8);
                    dst[2] = Clamp((c0 + b) >> 8);
                    dst[3] = 255;

                    dst[4] = Clamp((c1 + r) >> 8);
                    dst[5] = Clamp((c1 + g) >> 8);
                    dst[6] = Clamp((c1 + b) >> 8);
                    dst[7] = 255;

                    src += 4;
                    dst += 8;
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Clamp(int value) => (byte)Math.Clamp(value, 0, 255);
}
