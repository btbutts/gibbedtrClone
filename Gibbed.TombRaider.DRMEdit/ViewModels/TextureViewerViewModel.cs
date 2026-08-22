using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DRM = Gibbed.TombRaider.FileFormats.DRM;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using ImageMagick;
using Texture.BCnE.NET.Codec;

namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    public partial class TextureViewerViewModel : DocumentTabViewModel
    {
        private readonly DRM.Section _section;
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

        // Persists the ScrollViewer's pan position across tab switches, MainWindow's
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
        // UserControl to TextureViewerView, neither has a direct reference to the other's
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
            // near-zero (clamped-to-MinZoom) scale from a still-empty Bounds, the
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
            _section = section;

            var texture = new FileFormats.PCD9File();
            texture.Deserialize(section.Data);
            _texture = texture;

            Title = "Texture: " + section.Id.ToString("X8");
            InfoText = string.Format(
                "{0} : {1}x{2} | Filesize: {3} Bytes",
                texture.Format, texture.Width, texture.Height, section.Data.Length);

            IsZoomed = texture.Width > 512 || texture.Height > 512;

            UpdatePreview();
        }

        partial void OnShowAlphaChanged(bool value) => UpdatePreview();

        // PCD9 A8R8G8B8 raw bytes and TextureCodec's DXT decode output are both RGBA-ordered
        // (the latter mirrors squish's original RGBA contract). Avalonia's WriteableBitmap
        // pixel data is BGRA (PixelFormat.Bgra8888), matching GDI+'s Format32bppArgb that the
        // original WinForms TextureViewer displayed into, which is why the original always
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
        private void Save()
        {
            _section.Data = (System.IO.MemoryStream)_texture.Serialize();
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
                new FilePickerFileType("TIFF Files") { Patterns = new[] { "*.tif", "*.tiff" } },
                FilePickerFileTypes.All,
            };
            var savePath = await App.PickerService.SaveFileAsync(GetTopLevel?.Invoke(), "Save To File", fileTypes, null);
            if (savePath == null)
            {
                return;
            }

            var format = savePath.EndsWith(".tif", StringComparison.OrdinalIgnoreCase) ||
                         savePath.EndsWith(".tiff", StringComparison.OrdinalIgnoreCase)
                ? MagickFormat.Tiff
                : MagickFormat.Png;

            var imageBytes = EncodeImage(Preview, format);
            System.IO.File.WriteAllBytes(savePath, imageBytes);
        }

        // 192 DPI matches what the original WinForms/GDI+ tool wrote (inherited from the
        // original dev machine's display scaling, not a deliberate quality setting; DPI is
        // a print/layout hint only and does not affect the decoded pixel grid), preserved
        // here for parity with that tool's PNG output.
        private const double OriginalDpi = 192.0;

        private static byte[] EncodeImage(WriteableBitmap bitmap, MagickFormat format)
        {
            var width = bitmap.PixelSize.Width;
            var height = bitmap.PixelSize.Height;
            var pixels = new byte[width * height * 4];
            using (var locked = bitmap.Lock())
            {
                System.Runtime.InteropServices.Marshal.Copy(locked.Address, pixels, 0, pixels.Length);
            }

            // Preview's native pixel format is PixelFormat.Bgra8888 (see UpdatePreview).
            if (format == MagickFormat.Png)
            {
                return PNGEncoder.Encode(SwapRedBlue(pixels), width, height, OriginalDpi);
            }

            var settings = new PixelReadSettings((uint)width, (uint)height, StorageType.Char, PixelMapping.BGRA);
            using var image = new MagickImage(pixels, settings);
            image.Density = new Density(OriginalDpi, OriginalDpi, DensityUnit.PixelsPerInch);
            using var stream = new System.IO.MemoryStream();
            image.Write(stream, format);
            return stream.ToArray();
        }

        [RelayCommand]
        private async Task LoadFromFileAsync()
        {
            var fileTypes = new[]
            {
                new FilePickerFileType("Image")
                {
                    Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.tif", "*.tiff", "*.bmp" },
                },
                FilePickerFileTypes.All,
            };
            var paths = await App.PickerService.OpenFilesAsync(GetTopLevel?.Invoke(), "Load From File", fileTypes, false);
            var path = paths.Count > 0 ? paths[0] : null;
            if (path == null)
            {
                return;
            }

            try
            {
                ReplaceImage(path);
            }
            catch (Exception ex)
            {
                var box = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                {
                    ContentTitle = "Error",
                    ContentMessage = ex.Message,
                    ButtonDefinitions = ButtonEnum.Ok,
                    Icon = Icon.Error,
                    ContentBackground = MessageBoxTheme.GetContentBackgroundBrush(),
                    ContentBodyForeground = MessageBoxTheme.GetContentBodyForegroundBrush(),
                    ContentBodyFontSize = MessageBoxTheme.GetContentBodyFontSize(),
                    ContentHeaderFontSize = MessageBoxTheme.GetContentHeaderFontSize(),
                });
                await box.ShowAsync();
            }
        }

        private void ReplaceImage(string path)
        {
            using var image = new MagickImage(path);

            if (image.Width != (uint)_texture.Width || image.Height != (uint)_texture.Height)
            {
                throw new FormatException("New texture must have the same size as the old one");
            }

            if (_texture.Mipmaps.Count > 1)
            {
                throw new NotSupportedException("Texture with multiple mipmaps not supported");
            }

            if (image.HasAlpha == false)
            {
                image.Alpha(AlphaOption.Opaque);
            }

            var width = (int)image.Width;
            var height = (int)image.Height;

            // Requesting RGBA directly (rather than Magick.NET's native-order export) matches
            // what PCD9/TextureCodec already expect (see UpdatePreview's comment on channel
            // order), so no manual swap is needed here, unlike the BGRA read on the encode
            // side above.
            var mip = image.GetPixelsUnsafe().ToByteArray(PixelMapping.RGBA)
                ?? throw new InvalidOperationException("Magick.NET failed to export pixel data from the replacement image.");

            switch (_texture.Format)
            {
                case FileFormats.PCD9.Format.A8R8G8B8:
                    _texture.Mipmaps[0].Data = mip;
                    break;
                case FileFormats.PCD9.Format.DXT1:
                    _texture.Mipmaps[0].Data = TextureCodec.CompressImage(mip, width, height, TextureCodec.Flags.DXT1);
                    break;
                case FileFormats.PCD9.Format.DXT3:
                    _texture.Mipmaps[0].Data = TextureCodec.CompressImage(mip, width, height, TextureCodec.Flags.DXT3);
                    break;
                case FileFormats.PCD9.Format.DXT5:
                    _texture.Mipmaps[0].Data = TextureCodec.CompressImage(mip, width, height, TextureCodec.Flags.DXT5);
                    break;
            }

            UpdatePreview();
        }
    }
}
