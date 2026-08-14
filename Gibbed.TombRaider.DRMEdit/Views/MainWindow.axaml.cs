using System.Collections.Generic;
using Avalonia.Controls;

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
        }
    }
}
