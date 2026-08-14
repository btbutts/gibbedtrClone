using System;
using BCnEncoder.Decoder;
using BCnEncoder.Encoder;
using BCnEncoder.Shared;

namespace Texture.BCnE.NET.Codec
{
    public sealed class TextureCodec
    {
        public enum Flags
        {
            None = 0,

            /// <summary>
            /// Use DXT1 compression.
            /// </summary>
            DXT1 = 1 << 0,

            /// <summary>
            /// Use DXT3 compression.
            /// </summary>
            DXT3 = 1 << 1,

            /// <summary>
            /// Use DXT5 compression.
            /// </summary>
            DXT5 = 1 << 2,

            /// <summary>
            /// Use a slow but high quality colour compressor (the default).
            /// No longer has any effect now that compression is backed by
            /// BCnEncoder.Net instead of libsquish — kept only so callers
            /// combining flags from before this migration still compile.
            /// </summary>
            ColourClusterFit = 1 << 3,

            /// <summary>
            /// Use a fast but low quality colour compressor.
            /// No longer has any effect — see <see cref="ColourClusterFit"/>.
            /// </summary>
            ColourRangeFit = 1 << 4,

            /// <summary>
            /// Use a perceptual metric for colour error (the default).
            /// No longer has any effect — see <see cref="ColourClusterFit"/>.
            /// </summary>
            ColourMetricPerceptual = 1 << 5,

            /// <summary>
            /// Use a uniform metric for colour error.
            /// No longer has any effect — see <see cref="ColourClusterFit"/>.
            /// </summary>
            ColourMetricUniform = 1 << 6,

            /// <summary>
            /// Weight the colour by alpha during cluster fit (disabled by default).
            /// No longer has any effect — see <see cref="ColourClusterFit"/>.
            /// </summary>
            WeightColourByAlpha = 1 << 7,

            /// <summary>
            /// Use a very slow but very high quality colour compressor.
            /// No longer has any effect — see <see cref="ColourClusterFit"/>.
            /// </summary>
            ColourIterativeClusterFit = 1 << 8,
        }

        private static CompressionFormat ToCompressionFormat(Flags flags)
        {
            if ((flags & Flags.DXT1) != 0)
            {
                return CompressionFormat.Bc1;
            }

            if ((flags & Flags.DXT3) != 0)
            {
                return CompressionFormat.Bc2;
            }

            if ((flags & Flags.DXT5) != 0)
            {
                return CompressionFormat.Bc3;
            }

            throw new ArgumentException("flags must specify DXT1, DXT3, or DXT5", nameof(flags));
        }

        public static byte[] CompressImage(byte[] pixels, int width, int height, Flags flags)
        {
            var encoder = new BcEncoder(ToCompressionFormat(flags));
            encoder.OutputOptions.GenerateMipMaps = false;
            encoder.OutputOptions.Quality = CompressionQuality.BestQuality;
            return encoder.EncodeToRawBytes(pixels, width, height, PixelFormat.Rgba32, 0, out _, out _);
        }

        public static byte[] DecompressImage(byte[] blocks, int width, int height, Flags flags)
        {
            var decoder = new BcDecoder();
            var colors = decoder.DecodeRaw(blocks, width, height, ToCompressionFormat(flags));

            var output = new byte[width * height * 4];
            for (var i = 0; i < colors.Length; i++)
            {
                output[i * 4 + 0] = colors[i].r;
                output[i * 4 + 1] = colors[i].g;
                output[i * 4 + 2] = colors[i].b;
                output[i * 4 + 3] = colors[i].a;
            }
            return output;
        }
    }
}
