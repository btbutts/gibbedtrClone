using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Gibbed.DeusEx3.DRMEdit.Services;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;

namespace Gibbed.DeusEx3.DRMEdit.ViewModels
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
                new FilePickerFileType("Deus Ex 3 DRM Files") { Patterns = new[] { "*.drm" } },
                FilePickerFileTypes.All,
            };

            var paths = await _filePickerService.OpenFilesAsync(GetTopLevel?.Invoke(), "Open DRM", fileTypes, true);
            foreach (var path in paths)
            {
                await OpenFileAsync(path);
            }
        }

        // A missing/corrupt/unsupported-version DRM (or one hit via a startup command line
        // arg, see MainWindow's Opened handler) used to crash the whole process here, since
        // FileViewerViewModel's constructor deserializes with no guard of its own. This
        // includes the known version 19 DRM gap (Gibbed.DeusEx3.FileFormats.DRMFile only
        // supports version 21).
        public async System.Threading.Tasks.Task OpenFileAsync(string path)
        {
            FileViewerViewModel viewer;
            try
            {
                viewer = new FileViewerViewModel(this, path);
            }
            catch (System.Exception ex)
            {
                var box = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
                {
                    ContentTitle = "Error",
                    ContentMessage = $"Could not open '{System.IO.Path.GetFileName(path)}':\n{ex.Message}",
                    ButtonDefinitions = ButtonEnum.Ok,
                    Icon = Icon.Error,
                    ContentBackground = MessageBoxTheme.GetContentBackgroundBrush(),
                    ContentBodyForeground = MessageBoxTheme.GetContentBodyForegroundBrush(),
                    ContentBodyFontSize = MessageBoxTheme.GetContentBodyFontSize(),
                    ContentHeaderFontSize = MessageBoxTheme.GetContentHeaderFontSize(),
                });
                await box.ShowAsync();
                return;
            }

            AddDocument(viewer);
        }

        [RelayCommand]
        private void CloseAll()
        {
            foreach (var document in OpenDocuments.ToList())
            {
                document.Close();
            }

            // OnRequestClose already knows how to close a popped-out document's window
            // (IsPoppedOut path below), so route through the same document.Close() call
            // used for docked tabs rather than closing PopOutWindows directly here.
            foreach (var document in _popOutWindowService.GetOpenDocuments())
            {
                document.Close();
            }
        }
    }
}
