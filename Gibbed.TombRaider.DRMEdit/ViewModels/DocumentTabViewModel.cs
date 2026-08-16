using System;
using Avalonia.Controls;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Gibbed.TombRaider.DRMEdit.ViewModels
{
    public abstract partial class DocumentTabViewModel : ViewModelBase
    {
        [ObservableProperty]
        public partial string Title { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool IsPoppedOut { get; set; }

        [ObservableProperty]
        public partial double Width { get; set; } = 200;

        // The title-text fade-toward-the-buttons mask, built in MainWindow.RecomputeTabWidths
        // from fixed pixel constants (TabFadeKeepOutWidth/TabFadeWidth) so it covers the same
        // absolute pixel span regardless of tab width. Built as a whole brush in code-behind
        // and bound as a single object -- a {Binding} on a nested GradientStop's own Offset
        // doesn't reliably resolve (GradientStop isn't part of the visual tree, so it doesn't
        // inherit DataContext the way Control.OpacityMask itself does).
        [ObservableProperty]
        public partial IBrush? TitleFadeMask { get; set; }

        public Func<TopLevel?>? GetTopLevel { get; set; }

        public event EventHandler? RequestPopOut;
        public event EventHandler? RequestPopIn;
        public event EventHandler? RequestClose;

        [RelayCommand]
        private void PopOut()
        {
            RequestPopOut?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        private void PopIn()
        {
            RequestPopIn?.Invoke(this, EventArgs.Empty);
        }

        [RelayCommand]
        public void Close()
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }
    }
}
