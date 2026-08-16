using Avalonia.Controls;

namespace Gibbed.DeusEx3.DRMEdit.Views.TitleBar
{
    public partial class WindowsTitleBarButtons : UserControl
    {
        public WindowsTitleBarButtons()
        {
            InitializeComponent();

            this.FindControl<Button>("MinimizeButton")!.Click += (_, _) => OwnerWindow()!.WindowState = WindowState.Minimized;
            this.FindControl<Button>("MaximizeButton")!.Click += (_, _) => OwnerWindow()!.WindowState =
                OwnerWindow()!.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            this.FindControl<Button>("CloseButton")!.Click += (_, _) => OwnerWindow()!.Close();
        }

        private Window? OwnerWindow() => TopLevel.GetTopLevel(this) as Window;
    }
}
