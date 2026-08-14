using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using AvaloniaHex.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DRM = Gibbed.DeusEx3.FileFormats.DRM;

namespace Gibbed.DeusEx3.DRMEdit.ViewModels
{
    public partial class RawViewerViewModel : DocumentTabViewModel
    {
        [ObservableProperty]
        public partial IBinaryDocument HexDocument { get; set; }

        [ObservableProperty]
        public partial string InfoText { get; set; }

        private readonly byte[] _data;

        public RawViewerViewModel(DRM.Section section)
        {
            Title = "Raw View: " + section.Id.ToString("X8");

            _data = new byte[section.Data.Length];
            section.Data.Read(_data, 0, _data.Length);

            InfoText = string.Format(
                "ID:\t{0:X8}\nType:\t{1}\nFilesize:\t{2}", section.Id, section.Type, section.Data.Length);

            HexDocument = new MemoryBinaryDocument(_data, isReadOnly: true);
        }

        [RelayCommand(CanExecute = nameof(CanLoadFromFile))]
        private void LoadFromFile()
        {
            // Permanently disabled — matches the original WinForms RawViewer, whose
            // loadFromFileButton was Enabled = false and never enabled anywhere.
        }

        private bool CanLoadFromFile() => false;

        [RelayCommand]
        private async Task SaveToFileAsync()
        {
            var fileTypes = new[] { FilePickerFileTypes.All };
            var savePath = await App.PickerService.SaveFileAsync(GetTopLevel?.Invoke(), "Save To File", fileTypes, null);
            if (savePath == null)
            {
                return;
            }

            await System.IO.File.WriteAllBytesAsync(savePath, _data);
        }
    }
}
