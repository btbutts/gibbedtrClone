using System;
using Avalonia.Controls;
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
