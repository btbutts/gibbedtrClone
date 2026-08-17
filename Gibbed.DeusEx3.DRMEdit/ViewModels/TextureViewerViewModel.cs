using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DRM = Gibbed.DeusEx3.FileFormats.DRM;
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

            using (var output = System.IO.File.Create(savePath))
            {
                Preview.Save(output, new PngBitmapEncoderOptions());
            }
        }
    }
}
