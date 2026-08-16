using Avalonia.Controls;

namespace Gibbed.DeusEx3.DRMEdit.Views.TitleBar
{
    public partial class MacTitleBarButtons : UserControl
    {
        public MacTitleBarButtons()
        {
            InitializeComponent();

            this.FindControl<Button>("CloseButton")!.Click += (_, _) => OwnerWindow()!.Close();
            this.FindControl<Button>("MinimizeButton")!.Click += (_, _) => OwnerWindow()!.WindowState = WindowState.Minimized;
            this.FindControl<Button>("MaximizeButton")!.Click += (_, _) => OwnerWindow()!.WindowState =
                OwnerWindow()!.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private Window? OwnerWindow() => TopLevel.GetTopLevel(this) as Window;
    }
}
