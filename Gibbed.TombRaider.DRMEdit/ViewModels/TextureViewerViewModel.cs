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
using MsBox.Avalonia.Enums;
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
                var box = MessageBoxManager.GetMessageBoxStandard("Error", ex.Message, ButtonEnum.Ok, Icon.Error);
                await box.ShowAsync();
            }
        }

        private void ReplaceImage(string path)
        {
            using var bitmap = new Bitmap(path);

            if (bitmap.PixelSize.Width != _texture.Width || bitmap.PixelSize.Height != _texture.Height)
            {
                throw new FormatException("New texture must have the same size as the old one");
            }

            if (_texture.Mipmaps.Count > 1)
            {
                throw new NotSupportedException("Texture with multiple mipmaps not supported");
            }

            var width = bitmap.PixelSize.Width;
            var height = bitmap.PixelSize.Height;
            var mip = new byte[width * height * 4];

            using (var locked = new WriteableBitmap(bitmap.PixelSize, bitmap.Dpi, PixelFormat.Bgra8888, AlphaFormat.Unpremul).Lock())
            {
                bitmap.CopyPixels(new PixelRect(0, 0, width, height), locked.Address, mip.Length, width * 4);
                System.Runtime.InteropServices.Marshal.Copy(locked.Address, mip, 0, mip.Length);
            }

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
