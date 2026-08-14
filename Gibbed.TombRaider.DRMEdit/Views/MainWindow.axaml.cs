using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Gibbed.TombRaider.DRMEdit.ViewModels;

namespace Gibbed.TombRaider.DRMEdit.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        public MainWindow(List<string> startupFiles) : this()
        {
            Opened += (_, _) =>
            {
                if (DataContext is MainWindowViewModel viewModel)
                {
                    foreach (var path in startupFiles)
                    {
                        viewModel.OpenFile(path);
                    }
                }
            };
        }

        private void OnExitClick(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
