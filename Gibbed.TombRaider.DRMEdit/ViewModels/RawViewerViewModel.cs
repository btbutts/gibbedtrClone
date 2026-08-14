using DRM = Gibbed.TombRaider.FileFormats.DRM;

namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    // Placeholder — Task 4 replaces this with the real AvaloniaHex-backed implementation.
    public partial class RawViewerViewModel : DocumentTabViewModel
    {
        public RawViewerViewModel(DRM.Section section)
        {
            Title = "Raw View: " + section.Id.ToString("X8");
        }
    }
}
