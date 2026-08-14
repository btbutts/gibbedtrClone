using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gibbed.TombRaider.DRMEdit.Services;

namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IFilePickerService _filePickerService;
        private readonly IPopOutWindowService _popOutWindowService;

        public ObservableCollection<DocumentTabViewModel> OpenDocuments { get; } = new();

        [ObservableProperty]
        public partial DocumentTabViewModel? SelectedDocument { get; set; }

        public Func<TopLevel?>? GetTopLevel { get; set; }

        public MainWindowViewModel(IFilePickerService filePickerService, IPopOutWindowService popOutWindowService)
        {
            _filePickerService = filePickerService;
            _popOutWindowService = popOutWindowService;
        }

        public void AddDocument(DocumentTabViewModel document)
        {
            document.RequestPopOut += OnRequestPopOut;
            document.RequestPopIn += OnRequestPopIn;
            document.RequestClose += OnRequestClose;
            document.GetTopLevel = GetTopLevel;
            OpenDocuments.Add(document);
            SelectedDocument = document;
        }

        private void RemoveDocument(DocumentTabViewModel document)
        {
            document.RequestPopOut -= OnRequestPopOut;
            document.RequestPopIn -= OnRequestPopIn;
            document.RequestClose -= OnRequestClose;
            OpenDocuments.Remove(document);
        }

        private void OnRequestPopOut(object? sender, EventArgs e)
        {
            var document = (DocumentTabViewModel)sender!;
            OpenDocuments.Remove(document);
            document.IsPoppedOut = true;
            _popOutWindowService.PopOut(document, () => RemoveDocument(document));
        }

        private void OnRequestPopIn(object? sender, EventArgs e)
        {
            var document = (DocumentTabViewModel)sender!;
            _popOutWindowService.Close(document);
            document.IsPoppedOut = false;
            document.GetTopLevel = GetTopLevel;
            OpenDocuments.Add(document);
            SelectedDocument = document;
        }

        private void OnRequestClose(object? sender, EventArgs e)
        {
            var document = (DocumentTabViewModel)sender!;
            if (document.IsPoppedOut == true)
            {
                _popOutWindowService.Close(document);
            }
            RemoveDocument(document);
        }

        [RelayCommand]
        private async Task OpenAsync()
        {
            var fileTypes = new[]
            {
                new FilePickerFileType("TR DRM Files") { Patterns = new[] { "*.drm" } },
                FilePickerFileTypes.All,
            };

            var paths = await _filePickerService.OpenFilesAsync(GetTopLevel?.Invoke(), "Open DRM", fileTypes, true);
            foreach (var path in paths)
            {
                OpenFile(path);
            }
        }

        public void OpenFile(string path)
        {
            AddDocument(new FileViewerViewModel(this, path));
        }

        [RelayCommand]
        private void CloseAll()
        {
            foreach (var document in OpenDocuments.ToList())
            {
                document.Close();
            }
        }
    }
}
