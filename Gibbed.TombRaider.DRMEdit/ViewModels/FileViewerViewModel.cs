using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gibbed.IO;
using DRM = Gibbed.TombRaider.FileFormats.DRM;

namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    public partial class FileViewerViewModel : DocumentTabViewModel
    {
        private readonly MainWindowViewModel _owner;
        private readonly string _path;
        private readonly FileFormats.DRMFile _fileData;

        public ObservableCollection<SectionNode> Sections { get; } = new();

        [ObservableProperty]
        public partial SectionNode? SelectedSection { get; set; }

        // Persists the content-list TreeView's scroll position across tab switches --
        // MainWindow's ContentControl rebuilds this whole view from scratch each time the
        // tab is reselected, which would otherwise reset the TreeView's own internal
        // ScrollViewer.Offset. Saved/restored by FileViewerView's code-behind.
        [ObservableProperty]
        public partial Vector ScrollOffset { get; set; }

        public FileViewerViewModel(MainWindowViewModel owner, string path)
        {
            _owner = owner;
            _path = path;
            Title = "DRM View: " + Path.GetFileName(path);

            using (var input = File.OpenRead(path))
            {
                var data = new FileFormats.DRMFile();
                data.Deserialize(input);
                _fileData = data;
            }

            foreach (var section in _fileData.Sections.OrderBy(s => s.Id))
            {
                Sections.Add(new SectionNode(section));
            }
        }

        [RelayCommand]
        private async Task SaveDrmAsync()
        {
            var fileTypes = new[]
            {
                new FilePickerFileType("DRM Files") { Patterns = new[] { "*.drm" } },
                FilePickerFileTypes.All,
            };

            var savePath = await App.PickerService.SaveFileAsync(
                GetTopLevel?.Invoke(), "Save DRM", fileTypes, Path.GetFileName(_path));
            if (savePath == null)
            {
                return;
            }

            using (var output = File.Create(savePath))
            {
                var data = _fileData.Serialize();
                output.WriteFromStream(data, data.Length);
            }
        }

        [RelayCommand]
        private void ViewSection()
        {
            OpenSection(SelectedSection, false);
        }

        [RelayCommand]
        private void ViewSectionRaw()
        {
            OpenSection(SelectedSection, true);
        }

        public void OpenSection(SectionNode? node, bool forceRaw)
        {
            if (node == null)
            {
                return;
            }

            var section = node.Section;
            if (section.Data != null)
            {
                section.Data.Seek(0, System.IO.SeekOrigin.Begin);
            }

            DocumentTabViewModel viewer;
            if (forceRaw == true)
            {
                viewer = new RawViewerViewModel(section);
            }
            else
            {
                viewer = section.Type == DRM.SectionType.RenderResource
                    ? new TextureViewerViewModel(section)
                    : new RawViewerViewModel(section);
            }

            _owner.AddDocument(viewer);
        }
    }
}
