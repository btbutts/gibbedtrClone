using Avalonia.Controls;
using AvaloniaHex;
using Gibbed.TombRaider.DRMEdit.ViewModels;

namespace Gibbed.TombRaider.DRMEdit.Views
{
    public partial class RawViewerView : UserControl
    {
        public RawViewerView()
        {
            InitializeComponent();

            // FontFamily is set as a XAML attribute on HexEditor itself (see .axaml) --
            // HexEditor.HexView is a live existing instance, and its own FontFamily property
            // was being overwritten by HexEditor's control-theme TemplateBinding whenever it
            // was assigned directly here in code-behind instead.
            //
            // BytesPerLine left at its default (null) means the column count auto-fits to
            // available width; that auto-fit calculation is itself width-based off the same
            // per-character measurement the header/data columns use, so leaving it enabled
            // together with a non-monospace font is what caused header and data columns to
            // fall out of sync with each other as the window resized. Pinning BytesPerLine to
            // 16 (the standard hex-editor row width) removes that whole auto-fit code path;
            // it's a separate, deliberate choice from the font fix above, not a workaround for
            // the same bug.
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
