using System;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Gibbed.DeusEx3.DRMEdit.ViewModels;

namespace Gibbed.DeusEx3.DRMEdit.Views
{
    public partial class FileViewerView : UserControl
    {
        public static readonly IValueConverter IconKeyToBitmapConverter =
            new FuncValueConverter<string?, Bitmap?>(iconKey =>
            {
                var uri = iconKey switch
                {
                    "__DRM" => "avares://Gibbed.DeusEx3.DRMEdit/Assets/Icons/__DRM.png",
                    "RenderResource" => "avares://Gibbed.DeusEx3.DRMEdit/Assets/Icons/RenderResource.png",
                    "Script" => "avares://Gibbed.DeusEx3.DRMEdit/Assets/Icons/Script.png",
                    "Wave" => "avares://Gibbed.DeusEx3.DRMEdit/Assets/Icons/Wave.png",
                    _ => null,
                };
                return uri == null ? null : new Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri(uri)));
            });

        public FileViewerView()
        {
            InitializeComponent();

            AttachedToVisualTree += (_, _) =>
            {
                if (DataContext is FileViewerViewModel viewModel)
                {
                    viewModel.GetTopLevel = () => TopLevel.GetTopLevel(this);
                }
            };
        }

        private void OnNodeDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is FileViewerViewModel viewModel && viewModel.SelectedSection != null)
            {
                viewModel.OpenSection(viewModel.SelectedSection, false);
            }
        }
    }
}
