using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Gibbed.DeusEx3.DRMEdit.Views.TitleBar;

namespace Gibbed.DeusEx3.DRMEdit.Views
{
    public partial class PopOutWindow : Window
    {
        public PopOutWindow()
        {
            InitializeComponent();

            var isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            var controls = this.FindControl<StackPanel>("WindowControlsHost")!;

            if (isMac)
            {
                controls.Children.Add(new MacTitleBarButtons());
                DockPanel.SetDock(controls, Dock.Left);
            }
            else
            {
                controls.Children.Add(new WindowsTitleBarButtons());
            }

            var dragLayer = this.FindControl<Border>("TitleBarDragLayer")!;
            WindowDecorationProperties.SetElementRole(dragLayer, WindowDecorationsElementRole.TitleBar);
        }
    }
}
