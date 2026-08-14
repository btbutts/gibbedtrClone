using Avalonia.Controls;
using Gibbed.TombRaider.DRMEdit.ViewModels;

namespace Gibbed.TombRaider.DRMEdit.Views
{
    public partial class RawViewerView : UserControl
    {
        public RawViewerView()
        {
            InitializeComponent();

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
