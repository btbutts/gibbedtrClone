using System;
using System.Buffers.Binary;
using System.Text;

namespace Gibbed.TombRaider.DRMEdit
{
    // Builds a PNG file directly rather than delegating to a general-purpose image library's
    // writer. GDI+'s original PNG encoder always writes filter type 0/None for every
    // scanline, colortype 6 (truecolor + alpha) regardless of content, and always includes
    // gAMA (2.2, i.e. 1/2.2 stored) and sRGB (Perceptual) chunks. Both SkiaSharp's and later
    // Magick.NET's high-level writers proved unable to reliably reproduce this. Magick.NET
    // specifically silently downgraded a fully grayscale-looking texture to a narrower color
    // type, ignored an explicit "no filter" setting in favor of its own adaptive heuristic (up
    // to ~1.7x the file size on some textures), and introduced a +1 rounding drift on the
    // alpha channel for textures with continuously-varying alpha, all despite its
    // strongly-typed PngWriteDefines API appearing to offer direct control over each of those.
    // Building the file directly removes all three: colortype/filter are fixed values written
    // by hand rather than chosen by any analysis pass, and the only processing ever applied to
    // the pixel bytes themselves is the raw zlib compression.
    internal static class PNGEncoder
    {
        private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        // Encodes 8-bit RGBA pixel data (row-major, no padding) as a PNG matching GDI+'s
        // encoder byte-for-byte on every structural/metadata property: colortype 6, filter
        // type 0 for every scanline, gAMA 45455 (1/2.2), sRGB rendering intent 0 (Perceptual),
        // and a pHYs chunk derived from dpi.
        public static byte[] Encode(byte[] rgbaPixels, int width, int height, double dpi)
        {
            var ihdr = new byte[13];
            BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), (uint)width);
            BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), (uint)height);
            ihdr[8] = 8; // bit depth
            ihdr[9] = 6; // color type: truecolor + alpha
            ihdr[10] = 0; // compression method
            ihdr[11] = 0; // filter method
            ihdr[12] = 0; // interlace method

            var pixelsPerMeter = (uint)Math.Round(dpi / 0.0254);
            var physData = new byte[9];
            BinaryPrimitives.WriteUInt32BigEndian(physData.AsSpan(0, 4), pixelsPerMeter);
            BinaryPrimitives.WriteUInt32BigEndian(physData.AsSpan(4, 4), pixelsPerMeter);
            physData[8] = 1; // unit specifier: meter

            var gamaData = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(gamaData, 45455);

            var stride = width * 4;
            var filtered = new byte[height * (stride + 1)];
            for (var row = 0; row < height; row++)
            {
                var dstOffset = row * (stride + 1);
                filtered[dstOffset] = 0; // filter type: None
                Array.Copy(rgbaPixels, row * stride, filtered, dstOffset + 1, stride);
            }

            byte[] compressed;
            using (var compressedStream = new System.IO.MemoryStream())
            {
                using (var zlib = new System.IO.Compression.ZLibStream(
                    compressedStream, System.IO.Compression.CompressionLevel.SmallestSize, leaveOpen: true))
                {
                    zlib.Write(filtered, 0, filtered.Length);
                }
                compressed = compressedStream.ToArray();
            }

            using var output = new System.IO.MemoryStream();
            output.Write(Signature);
            output.Write(BuildChunk("IHDR", ihdr));
            output.Write(BuildChunk("sRGB", new byte[] { 0 }));
            output.Write(BuildChunk("gAMA", gamaData));
            output.Write(BuildChunk("pHYs", physData));
            output.Write(BuildChunk("IDAT", compressed));
            output.Write(BuildChunk("IEND", Array.Empty<byte>()));
            return output.ToArray();
        }

        private static byte[] BuildChunk(string type, byte[] chunkData)
        {
            var typeBytes = Encoding.ASCII.GetBytes(type);
            var chunk = new byte[4 + 4 + chunkData.Length + 4];
            BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(0, 4), (uint)chunkData.Length);
            typeBytes.CopyTo(chunk, 4);
            chunkData.CopyTo(chunk, 8);
            var crc = Crc32(typeBytes, chunkData);
            BinaryPrimitives.WriteUInt32BigEndian(chunk.AsSpan(8 + chunkData.Length, 4), crc);
            return chunk;
        }

        private static readonly uint[] _crc32Table = BuildCrc32Table();

        private static uint[] BuildCrc32Table()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                var c = n;
                for (var k = 0; k < 8; k++)
                {
                    c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
                }
                table[n] = c;
            }
            return table;
        }

        // Standard CRC-32 (ISO 3309 / zip / PNG Annex D), every PNG chunk is terminated with
        // this same algorithm over its type + data bytes.
        private static uint Crc32(byte[] type, byte[] data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in type)
            {
                crc = _crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }
            foreach (var b in data)
            {
                crc = _crc32Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
            }
            return crc ^ 0xFFFFFFFF;
        }
    }
}
