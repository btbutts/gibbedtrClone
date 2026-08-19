using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform.Storage;
using AvaloniaHex.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DRM = Gibbed.TombRaider.FileFormats.DRM;

namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    public partial class RawViewerViewModel : DocumentTabViewModel
    {
        [ObservableProperty]
        public partial IBinaryDocument HexDocument { get; set; }

        [ObservableProperty]
        public partial string InfoText { get; set; }

        // Toggled by the "Span window width" toolbar button. False (default) = a fixed
        // 16 bytes per row. True = RawViewerView dynamically computes bytes-per-row from
        // the draggable splitter's position instead, handled entirely in the View since it
        // needs the HexEditor's real column measurements.
        [ObservableProperty]
        public partial bool SpanWindowWidth { get; set; }

        // The three pieces of RawViewerView's own layout state that need to survive
        // MainWindow's ContentControl rebuilding this whole view from scratch on every tab
        // switch (same reason FileViewerViewModel.ScrollOffset exists): the hex/ascii
        // split's fraction while in span mode, the hex output's own scroll position, and
        // the horizontal GridSplitter's row height (hex area vs. the File Info/tabPage2
        // tabs below it). All three are saved/restored by RawViewerView's code-behind.
        [ObservableProperty]
        public partial double SpanFraction { get; set; } = 0.75;

        [ObservableProperty]
        public partial Vector HexScrollOffset { get; set; }

        [ObservableProperty]
        public partial double HexRowHeight { get; set; } = 200;

        private readonly DRM.Section _section;
        private byte[] _data = System.Array.Empty<byte>();

        public RawViewerViewModel(DRM.Section section)
        {
            _section = section;
            Title = "Raw View: " + section.Id.ToString("X8");
            LoadFromSection();
        }

        [RelayCommand(CanExecute = nameof(CanLoadFromFile))]
        private void LoadFromFile()
        {
            // Permanently disabled — matches the original WinForms RawViewer, whose
            // loadFromFileButton was Enabled = false and never enabled anywhere.
        }

        private bool CanLoadFromFile() => false;

        // This tab snapshots section.Data once at construction (matches the original
        // WinForms RawViewer/DynamicByteProvider, which had the same one-shot snapshot with
        // no refresh path). If a Texture Save on this same section happens while its Raw tab
        // is already open, that snapshot goes stale silently. Rather than add cross-viewmodel
        // observer wiring to auto-refresh (a bigger, more invasive change), this gives the
        // user an explicit, opt-in way to re-snapshot on demand.
        [RelayCommand]
        private void Refresh()
        {
            LoadFromSection();
        }

        private void LoadFromSection()
        {
            _section.Data.Position = 0;
            _data = new byte[_section.Data.Length];
            _section.Data.Read(_data, 0, _data.Length);

            InfoText = string.Format(
                "ID:\t{0:X8}\nType:\t{1}\nFilesize:\t{2}", _section.Id, _section.Type, _section.Data.Length);

            HexDocument = new MemoryBinaryDocument(_data, isReadOnly: true);
        }

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
