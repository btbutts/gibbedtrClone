using Avalonia.Controls;
using AvaloniaHex;
using Gibbed.DeusEx3.DRMEdit.ViewModels;

namespace Gibbed.DeusEx3.DRMEdit.Views
{
    public partial class RawViewerView : UserControl
    {
        public RawViewerView()
        {
            InitializeComponent();

            // BytesPerLine left at its default (null) means the column count auto-fits to
            // available width; that auto-fit calculation is itself width-based off the same
            // per-character measurement the header/data columns use, so leaving it enabled
            // together with a non-monospace font is what caused header and data columns to
            // fall out of sync with each other as the window resized. Pinning BytesPerLine to
            // 16 (the standard hex-editor row width) removes that whole auto-fit code path;
            // it's a separate, deliberate choice from the FontFamily fix in the .axaml, not a
            // workaround for the same bug.
            var hexEditor = this.FindControl<HexEditor>("TheHexEditor")!;
            hexEditor.HexView.BytesPerLine = 16;

            AttachedToVisualTree += (_, _) =>
            {
                if (DataContext is RawViewerViewModel viewModel)
                {
                    viewModel.GetTopLevel = () => TopLevel.GetTopLevel(this);
                }
            };
        }
    }
}
