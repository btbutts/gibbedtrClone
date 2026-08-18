using System;
using System.Buffers.Binary;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DRM = Gibbed.DeusEx3.FileFormats.DRM;
using SkiaSharp;
using Texture.BCnE.NET.Codec;

namespace Gibbed.DeusEx3.DRMEdit.ViewModels
{
    public partial class TextureViewerViewModel : DocumentTabViewModel
    {
        private readonly FileFormats.PCD9File _texture;

        [ObservableProperty]
        public partial WriteableBitmap? Preview { get; set; }

        [ObservableProperty]
        public partial bool IsZoomed { get; set; }

        // Magnification applied on top of IsZoomed's fit-to-view/actual-size toggle; driven
        // by TextureViewerView's Ctrl+scroll/pinch/Zoom In-Out handlers, clamped there to
        // [TextureViewerView.MinZoom, TextureViewerView.MaxZoom].
        [ObservableProperty]
        public partial double ZoomFactor { get; set; } = 1.0;

        // Persists the ScrollViewer's pan position across tab switches -- MainWindow's
        // ContentControl rebuilds this whole view from scratch each time the tab is
        // reselected, which would otherwise reset panning back to (0,0) every time even
        // though ZoomFactor/IsZoomed (being ViewModel-owned already) correctly survive it.
        [ObservableProperty]
        public partial Vector ScrollOffset { get; set; }

        // Set once TextureViewerView has applied the constructor's initial IsZoomed-driven
        // fit-to-view (see OnIsZoomedChanged/RequestZoomReset below). Not an
        // [ObservableProperty]: nothing binds to it, it's pure one-shot bookkeeping so a
        // fresh View instance recreated on every tab-switch-back doesn't re-run auto-fit
        // (which would recenter and discard the user's pan position) on every reattachment,
        // only on the texture's genuine first open.
        public bool HasAppliedInitialFit { get; set; }

        public const double ZoomStepFactor = 1.25;

        // The Zoom In/Out toolbar buttons live in TextureViewerToolbar, a sibling
        // UserControl to TextureViewerView -- neither has a direct reference to the other's
        // ScrollViewer, so these events (same pattern as DocumentTabViewModel's
        // RequestPopOut/RequestClose) let the ViewModel reach the View's actual
        // ScrollViewer-centering logic.
        public event EventHandler<double>? RequestZoom;
        public event EventHandler? RequestZoomReset;

        [RelayCommand]
        private void ZoomIn() => RequestZoom?.Invoke(this, ZoomStepFactor);

        [RelayCommand]
        private void ZoomOut() => RequestZoom?.Invoke(this, 1.0 / ZoomStepFactor);

        // Fit-to-view no longer means a fixed ZoomFactor==1.0: TextureViewerView computes
        // whatever factor actually fills the viewport (bounded by the shorter of width or
        // height) and applies it through ApplyFitZoom below, which must NOT be treated as
        // "the user manually zoomed" -- hence the suppression flag rather than comparing
        // against a hardcoded value.
        private bool _applyingFitZoom;

        // IsZoomed==true is the canonical "actually fit to view" state: any manual
        // magnification (buttons, Ctrl+scroll, pinch) means the display is no longer truly
        // fit-to-view, so the toggle drops to unchecked to reflect that -- and clicking it
        // again fully restores fit-to-view (below) rather than leaving the stale
        // magnification in place.
        partial void OnZoomFactorChanged(double value)
        {
            if (_applyingFitZoom)
            {
                return;
            }

            if (IsZoomed)
            {
                IsZoomed = false;
            }
        }

        partial void OnIsZoomedChanged(bool value)
        {
            if (value == false)
            {
                return;
            }

            RequestZoomReset?.Invoke(this, EventArgs.Empty);
        }

        // Called by TextureViewerView once it has computed the actual fit-to-view scale
        // from its own ScrollViewer bounds and the image's native pixel size (geometry the
        // ViewModel has no access to).
        public void ApplyFitZoom(double factor)
        {
            _applyingFitZoom = true;
            ZoomFactor = factor;
            _applyingFitZoom = false;

            // Setting this here, only once a fit has actually been computed and applied
            // (rather than pre-emptively in TextureViewerView right before calling
            // FitToView), covers every path that can trigger a fit: the constructor's
            // auto-fit for a large texture AND a manual "Zoom" toggle click. Previously only
            // the former set this flag, so after a manual toggle click the next
            // tab-switch-back's fresh View instance would still see HasAppliedInitialFit ==
            // false and re-run FitToView on a not-yet-laid-out ScrollViewer, computing a
            // near-zero (clamped-to-MinZoom) scale from a still-empty Bounds -- the
            // "reverts to zoomed way out" bug, reproducible exactly once per tab because
            // this method's re-entry via the erroneous refit finally set the flag correctly.
            HasAppliedInitialFit = true;
        }

        [ObservableProperty]
        public partial bool ShowAlpha { get; set; } = true;

        [ObservableProperty]
        public partial string InfoText { get; set; }

        public TextureViewerViewModel(DRM.Section section)
        {
            var texture = new FileFormats.PCD9File();
            texture.Deserialize(section.Data);
            _texture = texture;

            Title = "Texture: " + section.Id.ToString("X8");
            InfoText = string.Format("{0} : {1}x{2}", texture.Format, texture.Width, texture.Height);

            IsZoomed = texture.Width > 512 || texture.Height > 512;

            UpdatePreview();
        }

        partial void OnShowAlphaChanged(bool value) => UpdatePreview();

        // PCD9 A8R8G8B8 raw bytes and TextureCodec's DXT decode output are both RGBA-ordered
        // (the latter mirrors squish's original RGBA contract). Avalonia's WriteableBitmap
        // pixel data is BGRA (PixelFormat.Bgra8888), matching GDI+'s Format32bppArgb that the
        // original WinForms TextureViewer displayed into -- which is why the original always
        // swapped R/B in MakeBitmapFromTrueColor before blitting. Always returns a fresh
        // array so callers never mutate a live source buffer (mip.Data) in place.
        private static byte[] SwapRedBlue(byte[] source)
        {
            var output = new byte[source.Length];
            for (var i = 0; i + 3 < source.Length; i += 4)
            {
                output[i + 0] = source[i + 2];
                output[i + 1] = source[i + 1];
                output[i + 2] = source[i + 0];
                output[i + 3] = source[i + 3];
            }
            return output;
        }

        private void UpdatePreview()
        {
            if (_texture.Mipmaps.Count == 0)
            {
                Preview = null;
                return;
            }

            var mip = _texture.Mipmaps[0];
            var width = (int)mip.Width;
            var height = (int)mip.Height;

            byte[]? data = _texture.Format switch
            {
                FileFormats.PCD9.Format.A8R8G8B8 => mip.Data,
                FileFormats.PCD9.Format.DXT1 => TextureCodec.DecompressImage(mip.Data, width, height, TextureCodec.Flags.DXT1),
                FileFormats.PCD9.Format.DXT3 => TextureCodec.DecompressImage(mip.Data, width, height, TextureCodec.Flags.DXT3),
                FileFormats.PCD9.Format.DXT5 => TextureCodec.DecompressImage(mip.Data, width, height, TextureCodec.Flags.DXT5),
                _ => null,
            };

            if (data == null)
            {
                Preview = null;
                return;
            }

            data = SwapRedBlue(data);

            var bitmap = new WriteableBitmap(
                new PixelSize(width, height), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);

            var pixels = ShowAlpha == true ? data : (byte[])data.Clone();
            if (ShowAlpha == false)
            {
                for (var i = 3; i < pixels.Length; i += 4)
                {
                    pixels[i] = 0xFF;
                }
            }

            using (var buffer = bitmap.Lock())
            {
                System.Runtime.InteropServices.Marshal.Copy(pixels, 0, buffer.Address, pixels.Length);
            }

            Preview = bitmap;
        }

        [RelayCommand]
        private async Task SaveToFileAsync()
        {
            if (Preview == null)
            {
                return;
            }

            var fileTypes = new[]
            {
                new FilePickerFileType("PNG Files") { Patterns = new[] { "*.png" } },
                FilePickerFileTypes.All,
            };
            var savePath = await App.PickerService.SaveFileAsync(GetTopLevel?.Invoke(), "Save To File", fileTypes, null);
            if (savePath == null)
            {
                return;
            }

            var pngBytes = EncodePngLikeOriginal(Preview);
            System.IO.File.WriteAllBytes(savePath, pngBytes);
        }

        // Avalonia's Bitmap.Save(stream, PngBitmapEncoderOptions) only exposes
        // CompressionLevel; the SkiaSharp encoder underneath it still independently picks
        // a per-scanline filter (Sub/Up/Avg/Paeth) via SKPngEncoderOptions.FilterFlags,
        // which Avalonia's wrapper never surfaces. That adaptive-filter heuristic measurably
        // hurts LZ77 compressibility on textures with large flat/repeating regions: for a
        // real TR texture this produced a PNG ~1.7x the size of the original WinForms/GDI+
        // export for byte-identical decoded pixel data (confirmed by decompressing both
        // IDAT streams and diffing the per-row filter bytes: GDI+ always wrote filter type
        // 0/None; Skia's default wrote mostly Avg/Paeth). Bypassing Avalonia's wrapper and
        // calling SkiaSharp directly, pinned in the .csproj to the exact version
        // Avalonia.Skia 12.1.1 depends on so no second native Skia binary gets loaded, with
        // FilterFlags.None recovers GDI+-equivalent file sizes.
        //
        // SkiaSharp's PNG encoder has no DPI/resolution option at all, so the 192 DPI GDI+
        // used to write (inherited from the original dev machine's display scaling, not a
        // deliberate quality setting; DPI is a print/layout hint only and does not affect
        // the decoded pixel grid) is hand-spliced back in as a minimal pHYs chunk, purely
        // for byte-level parity with the original tool's output.
        private static byte[] EncodePngLikeOriginal(WriteableBitmap bitmap)
        {
            const double OriginalDpi = 192.0;

            var width = bitmap.PixelSize.Width;
            var height = bitmap.PixelSize.Height;
            var pixels = new byte[width * height * 4];
            using (var locked = bitmap.Lock())
            {
                System.Runtime.InteropServices.Marshal.Copy(locked.Address, pixels, 0, pixels.Length);
            }

            var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
            try
            {
                using var skBitmap = new SKBitmap();
                if (skBitmap.InstallPixels(info, handle.AddrOfPinnedObject(), info.RowBytes) == false)
                {
                    throw new InvalidOperationException("Failed to install pixel buffer into SKBitmap for PNG encoding.");
                }

                using var pixmap = skBitmap.PeekPixels()
                    ?? throw new InvalidOperationException("SKBitmap.PeekPixels() returned null after a successful InstallPixels call.");
                using var data = pixmap.Encode(new SKPngEncoderOptions(SKPngEncoderFilterFlags.None, 9))
                    ?? throw new InvalidOperationException("SkiaSharp failed to encode the texture preview as PNG.");
                return InsertPhysChunk(data.ToArray(), OriginalDpi);
            }
            finally
            {
                handle.Free();
            }
        }

        // PNG requires pHYs, if present, to appear before the first IDAT. Splices a minimal
        // one (pixels-per-meter, unit=meter, the same units GDI+ wrote) right after the
        // mandatory IHDR, which is always exactly 33 bytes in (8 signature + 8 chunk header
        // + 13 IHDR data + 4 CRC).
        private static byte[] InsertPhysChunk(byte[] png, double dpi)
        {
            var pixelsPerMeter = (uint)Math.Round(dpi / 0.0254);
            var chunkData = new byte[9];
            BinaryPrimitives.WriteUInt32BigEndian(chunkData.AsSpan(0, 4), pixelsPerMeter);
            BinaryPrimitives.WriteUInt32BigEndian(chunkData.AsSpan(4, 4), pixelsPerMeter);
            chunkData[8] = 1; // unit specifier: meter

            var chunk = BuildPngChunk("pHYs", chunkData);

            const int ihdrEnd = 8 + 8 + 13 + 4;
            var result = new byte[png.Length + chunk.Length];
            Array.Copy(png, 0, result, 0, ihdrEnd);
            Array.Copy(chunk, 0, result, ihdrEnd, chunk.Length);
            Array.Copy(png, ihdrEnd, result, ihdrEnd + chunk.Length, png.Length - ihdrEnd);
            return result;
        }

        private static byte[] BuildPngChunk(string type, byte[] chunkData)
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

        // Standard CRC-32 (ISO 3309 / zip / PNG Annex D), every PNG chunk is terminated
        // with this same algorithm over its type + data bytes.
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
