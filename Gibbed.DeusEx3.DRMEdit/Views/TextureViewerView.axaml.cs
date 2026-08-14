using System;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Gibbed.DeusEx3.DRMEdit.ViewModels;

namespace Gibbed.DeusEx3.DRMEdit.Views
{
    public partial class TextureViewerView : UserControl
    {
        public TextureViewerView()
        {
            InitializeComponent();

            AttachedToVisualTree += (_, _) =>
            {
                if (DataContext is TextureViewerViewModel viewModel)
                {
                    viewModel.GetTopLevel = () => TopLevel.GetTopLevel(this);
                }
            };
        }
    }

    public class BoolToStretchConverter : IValueConverter
    {
        public static readonly BoolToStretchConverter Instance = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => (value is true) ? Stretch.Uniform : Stretch.None;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
