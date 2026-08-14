using DRM = Gibbed.TombRaider.FileFormats.DRM;

namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    // Placeholder — Task 6 replaces this with the real WriteableBitmap-backed implementation.
    public partial class TextureViewerViewModel : DocumentTabViewModel
    {
        public TextureViewerViewModel(DRM.Section section)
        {
            Title = "Texture: " + section.Id.ToString("X8");
        }
    }
}
