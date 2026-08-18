using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Gibbed.DeusEx3.DRMEdit.Views.TitleBar;

namespace Gibbed.DeusEx3.DRMEdit.Views
{
    public partial class PopOutWindow : Window
    {
        // Tunable: gap kept between the window controls and the title text once
        // repositioned for macOS, mirrors MainWindow's MacWindowControlsGap constant. On
        // Windows/Linux this breathing room isn't needed since the controls are the
        // rightmost element with nothing after them.
        // Bumped from 8 to 16 per user feedback testing on real macOS hardware: the
        // original gap read as too tight between the traffic lights and the title text.
        private const double MacWindowControlsGap = 16;

        public PopOutWindow()
        {
            InitializeComponent();

            var isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            var controls = this.FindControl<StackPanel>("WindowControlsHost")!;

            if (isMac)
            {
                controls.Children.Add(new MacTitleBarButtons());
                RepositionForMacWindowControls(controls);
            }
            else
            {
                controls.Children.Add(new WindowsTitleBarButtons());
            }

            var dragLayer = this.FindControl<Border>("TitleBarDragLayer")!;
            WindowDecorationProperties.SetElementRole(dragLayer, WindowDecorationsElementRole.TitleBar);
        }

        // macOS convention puts window controls (traffic lights) at the top-left, not the
        // top-right like Windows/Linux. DockPanel.SetDock(controls, Dock.Left) previously
        // sat here instead and silently did nothing: WindowControlsHost lives inside
        // TitleBarContentGrid (a Grid), not a DockPanel, so that attached property was
        // never read by anything laying it out, same root cause MainWindow.axaml.cs already
        // documents and fixes for the main window's own title bar (RepositionForMacWindowControls
        // there). This is the PopOutWindow equivalent, simpler since there's no third
        // tabs column to preserve, just title text and controls swapping places.
        //
        // Not yet verified on real macOS hardware; best-effort layout fix based on reading
        // the code, mirroring the same fix already applied (and still unverified) on
        // MainWindow. See CLAUDE.md's Known Issues section.
        private void RepositionForMacWindowControls(StackPanel controls)
        {
            var grid = this.FindControl<Grid>("TitleBarContentGrid")!;
            var titleText = this.FindControl<TextBlock>("TitleText")!;

            grid.ColumnDefinitions = new ColumnDefinitions("Auto,*");

            Grid.SetColumn(controls, 0);
            Grid.SetColumn(titleText, 1);

            controls.Margin = new Thickness(0, 0, MacWindowControlsGap, 0);

            // TitleText's original Margin="12,0,16,0" assumed it was the leftmost element;
            // its left inset is no longer needed now that the window controls (plus the
            // gap just set above) already provide that space, so only the original right
            // margin (breathing room before the window's right edge) is kept.
            titleText.Margin = new Thickness(0, 0, 16, 0);
        }
    }
}
