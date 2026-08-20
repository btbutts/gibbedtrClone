using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using DRM = Gibbed.DeusEx3.FileFormats.DRM;

namespace Gibbed.DeusEx3.DRMEdit.ViewModels
{
    public partial class FileViewerViewModel : DocumentTabViewModel
    {
        private readonly MainWindowViewModel _owner;
        private readonly FileFormats.DRMFile _fileData;

        public ObservableCollection<SectionNode> Sections { get; } = new();
        public ObservableCollection<string> TypeFilterOptions { get; } = new();

        [ObservableProperty]
        public partial SectionNode? SelectedSection { get; set; }

        [ObservableProperty]
        public partial string SelectedTypeFilter { get; set; } = "All";

        // Persists the content-list TreeView's scroll position across tab switches --
        // MainWindow's ContentControl rebuilds this whole view from scratch each time the
        // tab is reselected, which would otherwise reset the TreeView's own internal
        // ScrollViewer.Offset. Saved/restored by FileViewerView's code-behind.
        [ObservableProperty]
        public partial Vector ScrollOffset { get; set; }

        public FileViewerViewModel(MainWindowViewModel owner, string path)
        {
            _owner = owner;
            Title = "DRM View: " + Path.GetFileName(path);

            using (var input = File.OpenRead(path))
            {
                var data = new FileFormats.DRMFile();
                data.Deserialize(input);
                _fileData = data;
            }

            TypeFilterOptions.Add("All");
            foreach (DRM.SectionType type in System.Enum.GetValues(typeof(DRM.SectionType)))
            {
                TypeFilterOptions.Add(type.ToString());
            }

            RebuildTree();
        }

        partial void OnSelectedTypeFilterChanged(string value) => RebuildTree();

        private void RebuildTree()
        {
            Sections.Clear();

            var showAll = SelectedTypeFilter == "All";
            var filterType = showAll
                ? default
                : (DRM.SectionType)System.Enum.Parse(typeof(DRM.SectionType), SelectedTypeFilter);

            foreach (var section in _fileData.Sections.OrderBy(s => s.Id))
            {
                if (showAll == false && section.Type != filterType)
                {
                    continue;
                }
                Sections.Add(new SectionNode(section));
            }
        }

        // No SaveDrmCommand: Gibbed.DeusEx3.FileFormats.DRMFile has no Serialize() method.
        // The original WinForms saveDRMButton exists but is permanently Enabled = false —
        // there is deliberately no equivalent command exposed here at all, rather than a
        // command that does nothing, since there's no toolbar button bound to it to explain
        // why (Deus Ex 3's FileViewerView keeps a disabled-looking button purely for visual
        // parity — see the View below).

        [RelayCommand]
        private Task ViewSection() => OpenSectionAsync(SelectedSection, false);

        [RelayCommand]
        private Task ViewSectionRaw() => OpenSectionAsync(SelectedSection, true);

        // A section whose deserializer chokes (e.g. any malformed RenderResource) used to
        // crash the whole process here, since TextureViewerViewModel's constructor
        // deserializes with no guard of its own.
        public async Task OpenSectionAsync(SectionNode? node, bool forceRaw)
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
            try
            {
                viewer = forceRaw == true
                    ? new RawViewerViewModel(section)
                    : (section.Type == DRM.SectionType.RenderResource
                        ? new TextureViewerViewModel(section)
                        : new RawViewerViewModel(section));
            }
            catch (System.Exception ex)
            {
                var box = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                {
                    ContentTitle = "Error",
                    ContentMessage = $"Could not open section {section.Id:X8}: {ex.Message}",
                    ButtonDefinitions = ButtonEnum.Ok,
                    Icon = Icon.Error,
                    Background = MessageBoxTheme.GetBackgroundBrush(),
                });
                await box.ShowAsync();
                return;
            }

            _owner.AddDocument(viewer);
        }
    }
}
