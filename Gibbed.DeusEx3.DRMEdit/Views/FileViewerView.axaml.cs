using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Gibbed.DeusEx3.DRMEdit.ViewModels;

namespace Gibbed.DeusEx3.DRMEdit.Views
{
    public partial class FileViewerView : UserControl
    {
        // Cached per icon key (lazily, on first request) instead of decoding a fresh Bitmap
        // from the embedded asset stream on every single binding conversion -- tree
        // virtualization can call this converter repeatedly for the same section types as
        // nodes scroll in and out of view.
        private static readonly Dictionary<string, Bitmap?> _iconCache = new();

        public static readonly IValueConverter IconKeyToBitmapConverter =
            new FuncValueConverter<string?, Bitmap?>(GetIcon);

        private static Bitmap? GetIcon(string? iconKey)
        {
            if (iconKey == null)
            {
                return null;
            }

            if (_iconCache.TryGetValue(iconKey, out var cached))
            {
                return cached;
            }

            var uri = iconKey switch
            {
                "__DRM" => "avares://Gibbed.DeusEx3.DRMEdit/Assets/Icons/__DRM.png",
                "RenderResource" => "avares://Gibbed.DeusEx3.DRMEdit/Assets/Icons/RenderResource.png",
                "Script" => "avares://Gibbed.DeusEx3.DRMEdit/Assets/Icons/Script.png",
                "Wave" => "avares://Gibbed.DeusEx3.DRMEdit/Assets/Icons/Wave.png",
                _ => null,
            };

            var bitmap = uri == null ? null : new Bitmap(Avalonia.Platform.AssetLoader.Open(new Uri(uri)));
            _iconCache[iconKey] = bitmap;
            return bitmap;
        }

        private readonly TreeView _treeView;
        private ScrollViewer? _scrollViewer;

        public FileViewerView()
        {
            InitializeComponent();

            _treeView = this.FindControl<TreeView>("SectionsTreeView")!;
            _treeView.Loaded += OnTreeViewLoaded;

            AttachedToVisualTree += (_, _) =>
            {
                if (DataContext is FileViewerViewModel viewModel)
                {
                    viewModel.GetTopLevel = () => TopLevel.GetTopLevel(this);
                }
            };

            DetachedFromVisualTree += (_, _) =>
            {
                if (_scrollViewer != null)
                {
                    _scrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;
                    _scrollViewer = null;
                }
            };
        }

        // TreeView's internal ScrollViewer only exists once its control template has been
        // applied; Loaded (unlike AttachedToVisualTree) fires after that, so the descendant
        // search below is guaranteed to find it. Restoring/persisting Offset here (instead
        // of leaving it as TreeView-internal state) is what survives MainWindow's
        // ContentControl rebuilding this whole view from scratch on every tab switch.
        private void OnTreeViewLoaded(object? sender, RoutedEventArgs e)
        {
            if (_scrollViewer != null)
            {
                return;
            }

            _scrollViewer = _treeView.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            if (_scrollViewer == null)
            {
                return;
            }

            if (DataContext is FileViewerViewModel viewModel)
            {
                _scrollViewer.Offset = viewModel.ScrollOffset;
            }

            _scrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
        }

        private void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ScrollViewer.OffsetProperty &&
                sender is ScrollViewer scrollViewer &&
                DataContext is FileViewerViewModel viewModel)
            {
                viewModel.ScrollOffset = scrollViewer.Offset;
            }
        }

        private async void OnNodeDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (DataContext is FileViewerViewModel viewModel && viewModel.SelectedSection != null)
            {
                await viewModel.OpenSectionAsync(viewModel.SelectedSection, false);
            }
        }
    }
}
