namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    // Temporary stand-in for FileViewerViewModel, which doesn't exist until Task 3.
    // Deleted once FileViewerViewModel takes over OpenFile in MainWindowViewModel.
    public class DemoDocumentViewModel : DocumentTabViewModel
    {
        public DemoDocumentViewModel(string path)
        {
            Title = System.IO.Path.GetFileName(path);
        }
    }
}
