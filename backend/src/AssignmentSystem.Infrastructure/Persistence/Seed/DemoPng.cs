using System.IO.Compression;

namespace AssignmentSystem.Infrastructure.Persistence.Seed;

/// <summary>
/// A PNG encoder and the small amount of drawing the seeder needs from it: graph paper with a
/// plotted curve, which is what a mathematics or physics attachment would actually be a picture
/// of.
///
/// Hand-written for the same reason as <see cref="DemoPdf"/>, plus one of its own —
/// <c>System.Drawing.Common</c> is Windows-only from .NET 6 onwards, and this seeder runs inside
/// a Linux container. The format needed here is four chunks and a CRC, which is less code than
/// taking on a cross-platform imaging dependency for demo data.
///
/// The result is a real image: 8-bit truecolour, zlib-compressed as the specification requires,
/// so it decodes in a browser and previews in the app the same way an uploaded photograph would.
/// </summary>
internal static class DemoPng
{
    private const int Width = 760;
    private const int Height = 460;

    private const int GridStep = 38;
    private const int OriginX = 76;
    private const int OriginY = Height - 76;
    private const int PlotTop = 48;
    private const int PlotRight = Width - 38;

    /// <summary>One colour per curve, so two figures in the same course still look different.</summary>
    private static readonly (byte R, byte G, byte B)[] CurveColours =
    [
        (37, 99, 235),   // blue
        (219, 39, 119),  // pink
        (5, 150, 105),   // green
        (234, 88, 12),   // orange
    ];

    /// <summary>
    /// Graph paper with axes, ticks and one plotted curve. <paramref name="variant"/> selects the
    /// curve — a parabola, a wave, a straight line or an exponential decay — so a brief on
    /// projectile motion and one on differential equations do not attach the same picture.
    /// </summary>
    public static byte[] RenderChart(int variant)
    {
        var kind = ((variant % 4) + 4) % 4;
        var pixels = new byte[Width * Height * 3];

        Fill(pixels, 255, 255, 255);

        // Graph paper first, so the axes and the curve sit on top of it.
        for (var x = 0; x < Width; x += GridStep)
        {
            VerticalLine(pixels, x, 0, Height - 1, 226, 232, 240);
        }

        for (var y = Height - 1; y >= 0; y -= GridStep)
        {
            HorizontalLine(pixels, 0, Width - 1, y, 226, 232, 240);
        }

        // Axes, with ticks every second grid square.
        VerticalLine(pixels, OriginX, PlotTop - 10, OriginY + 20, 71, 85, 105);
        HorizontalLine(pixels, OriginX - 20, PlotRight + 10, OriginY, 71, 85, 105);

        for (var x = OriginX + (GridStep * 2); x <= PlotRight; x += GridStep * 2)
        {
            VerticalLine(pixels, x, OriginY - 5, OriginY + 5, 71, 85, 105);
        }

        for (var y = OriginY - (GridStep * 2); y >= PlotTop; y -= GridStep * 2)
        {
            HorizontalLine(pixels, OriginX - 5, OriginX + 5, y, 71, 85, 105);
        }

        PlotCurve(pixels, kind);

        return Encode(pixels);
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Samples the curve once per column and joins consecutive samples vertically, which is all
    /// the continuity a plot needs when the step is a single pixel.
    /// </summary>
    private static void PlotCurve(byte[] pixels, int kind)
    {
        var (r, g, b) = CurveColours[kind];
        var plotHeight = OriginY - PlotTop;
        var previousY = int.MinValue;

        for (var x = OriginX; x <= PlotRight; x++)
        {
            var t = (double)(x - OriginX) / (PlotRight - OriginX);
            var y = OriginY - (int)(Value(kind, t) * plotHeight);

            if (previousY != int.MinValue)
            {
                VerticalLine(pixels, x, Math.Min(previousY, y), Math.Max(previousY, y), r, g, b);
                VerticalLine(pixels, x + 1, Math.Min(previousY, y), Math.Max(previousY, y), r, g, b);
            }

            // Three pixels tall, so the line reads as a line rather than as a hairline.
            VerticalLine(pixels, x, y - 1, y + 1, r, g, b);
            previousY = y;
        }
    }

    /// <summary>The curve's height as a fraction of the plot area, for a position along it.</summary>
    private static double Value(int kind, double t) => kind switch
    {
        0 => t * t,                                        // parabola
        1 => 0.5 + (0.44 * Math.Sin(t * Math.PI * 3)),      // wave, one and a half cycles
        2 => 0.12 + (0.78 * t),                            // straight line
        _ => Math.Exp(-3.2 * t),                           // exponential decay
    };

    private static void Fill(byte[] pixels, byte r, byte g, byte b)
    {
        for (var i = 0; i < pixels.Length; i += 3)
        {
            pixels[i] = r;
            pixels[i + 1] = g;
            pixels[i + 2] = b;
        }
    }

    private static void HorizontalLine(byte[] pixels, int fromX, int toX, int y, byte r, byte g, byte b)
    {
        for (var x = Math.Max(0, fromX); x <= Math.Min(Width - 1, toX); x++)
        {
            SetPixel(pixels, x, y, r, g, b);
        }
    }

    private static void VerticalLine(byte[] pixels, int x, int fromY, int toY, byte r, byte g, byte b)
    {
        for (var y = Math.Max(0, fromY); y <= Math.Min(Height - 1, toY); y++)
        {
            SetPixel(pixels, x, y, r, g, b);
        }
    }

    /// <summary>Clipped rather than checked by the callers, so drawing near an edge is not a special case.</summary>
    private static void SetPixel(byte[] pixels, int x, int y, byte r, byte g, byte b)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            return;
        }

        var offset = ((y * Width) + x) * 3;
        pixels[offset] = r;
        pixels[offset + 1] = g;
        pixels[offset + 2] = b;
    }

    // ── Encoding ──────────────────────────────────────────────────────────────

    private static readonly byte[] Signature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    private static byte[] Encode(byte[] pixels)
    {
        using var file = new MemoryStream();
        file.Write(Signature);

        // Bit depth 8, colour type 2 (truecolour RGB), no interlacing.
        var header = new byte[13];
        WriteBigEndian(header, 0, (uint)Width);
        WriteBigEndian(header, 4, (uint)Height);
        header[8] = 8;
        header[9] = 2;
        WriteChunk(file, "IHDR", header);

        WriteChunk(file, "IDAT", Deflate(AddFilterBytes(pixels)));
        WriteChunk(file, "IEND", []);

        return file.ToArray();
    }

    /// <summary>
    /// Every scanline in a PNG is prefixed with the filter that was applied to it. Filter 0 means
    /// "none": these images are flat colour on a grid, which zlib already compresses to a few
    /// kilobytes, so a predictor would buy nothing worth the arithmetic.
    /// </summary>
    private static byte[] AddFilterBytes(byte[] pixels)
    {
        var stride = Width * 3;
        var raw = new byte[Height * (stride + 1)];

        for (var y = 0; y < Height; y++)
        {
            raw[y * (stride + 1)] = 0;
            Array.Copy(pixels, y * stride, raw, (y * (stride + 1)) + 1, stride);
        }

        return raw;
    }

    /// <summary>
    /// PNG's IDAT payload is a zlib stream (RFC 1950) — the header and Adler-32 checksum
    /// included, which is exactly what <see cref="ZLibStream"/> writes and what a bare
    /// <c>DeflateStream</c> would leave out.
    /// </summary>
    private static byte[] Deflate(byte[] raw)
    {
        using var compressed = new MemoryStream();
        using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(raw);
        }

        return compressed.ToArray();
    }

    private static void WriteChunk(Stream file, string type, byte[] data)
    {
        var length = new byte[4];
        WriteBigEndian(length, 0, (uint)data.Length);
        file.Write(length);

        var typeBytes = new[] { (byte)type[0], (byte)type[1], (byte)type[2], (byte)type[3] };
        file.Write(typeBytes);
        file.Write(data);

        // The CRC covers the type and the data, but not the length.
        var crc = Crc32(typeBytes, data);
        var crcBytes = new byte[4];
        WriteBigEndian(crcBytes, 0, crc);
        file.Write(crcBytes);
    }

    private static void WriteBigEndian(byte[] target, int offset, uint value)
    {
        target[offset] = (byte)(value >> 24);
        target[offset + 1] = (byte)(value >> 16);
        target[offset + 2] = (byte)(value >> 8);
        target[offset + 3] = (byte)value;
    }

    // ── CRC-32 ────────────────────────────────────────────────────────────────

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];

        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }

            table[n] = c;
        }

        return table;
    }

    private static uint Crc32(byte[] first, byte[] second)
    {
        var crc = 0xFFFFFFFFu;

        foreach (var b in first)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        foreach (var b in second)
        {
            crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }

        return crc ^ 0xFFFFFFFFu;
    }
}
