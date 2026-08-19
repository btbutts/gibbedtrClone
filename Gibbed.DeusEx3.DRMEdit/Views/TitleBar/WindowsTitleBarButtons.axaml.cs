using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;

namespace Gibbed.DeusEx3.DRMEdit.Views.TitleBar
{
    public partial class WindowsTitleBarButtons : UserControl
    {
        private readonly Rectangle _maximizeGlyph;
        private readonly Canvas _restoreGlyph;
        private readonly Button _maximizeButton;

        public WindowsTitleBarButtons()
        {
            InitializeComponent();

            _maximizeGlyph = this.FindControl<Rectangle>("MaximizeGlyph")!;
            _restoreGlyph = this.FindControl<Canvas>("RestoreGlyph")!;
            _maximizeButton = this.FindControl<Button>("MaximizeButton")!;

            this.FindControl<Button>("MinimizeButton")!.Click += (_, _) => OwnerWindow()!.WindowState = WindowState.Minimized;
            _maximizeButton.Click += (_, _) => OwnerWindow()!.WindowState =
                OwnerWindow()!.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            this.FindControl<Button>("CloseButton")!.Click += (_, _) => OwnerWindow()!.Close();

            // Deferred to AttachedToVisualTree, not the constructor: OwnerWindow() resolves
            // via TopLevel.GetTopLevel(this), which is only non-null once this control is
            // actually attached to a Window's visual tree.
            AttachedToVisualTree += (_, _) =>
            {
                var window = OwnerWindow();
                if (window == null)
                {
                    return;
                }

                UpdateMaximizeGlyph(window.WindowState);
                window.PropertyChanged += (_, e) =>
                {
                    if (e.Property == Window.WindowStateProperty)
                    {
                        UpdateMaximizeGlyph(window.WindowState);
                    }
                };
            };
        }

        private void UpdateMaximizeGlyph(WindowState state)
        {
            var isMaximized = state == WindowState.Maximized;
            _maximizeGlyph.IsVisible = isMaximized == false;
            _restoreGlyph.IsVisible = isMaximized;
            ToolTip.SetTip(_maximizeButton, isMaximized ? "Restore" : "Maximize");
        }

        private Window? OwnerWindow() => TopLevel.GetTopLevel(this) as Window;
    }
}
