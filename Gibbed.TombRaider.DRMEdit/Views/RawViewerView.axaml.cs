using System;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.TextFormatting;
using Avalonia.VisualTree;
using AvaloniaHex;
using AvaloniaHex.Rendering;
using Gibbed.TombRaider.DRMEdit.ViewModels;

namespace Gibbed.TombRaider.DRMEdit.Views
{
    public partial class RawViewerView : UserControl
    {
        private const int FixedBytesPerLine = 16;
        private const int MinSpanBytesPerLine = 1;

        // Fixed width of the invisible SpacerColumn inserted at each end of the column
        // layout, reserving breathing room between the offset column's row IDs / the ASCII
        // output and the viewport's true left/right edges.
        private const double EdgeSpacerWidth = 6;

        private readonly Grid _rootGrid;
        private readonly Grid _hexRowContainer;
        private readonly HexEditor _hexEditor;
        private readonly Thumb _splitterThumb;
        private readonly OffsetColumn _offsetColumn;
        private readonly HexColumn _hexColumn;
        private readonly AsciiColumn _asciiColumn;

        private RawViewerViewModel? _viewModel;
        private ScrollViewer? _hexScrollViewer;

        public RawViewerView()
        {
            InitializeComponent();

            // RootGrid (RowDefinitions="200,4,*") is the OUTER Grid -- the horizontal
            // GridSplitter between the hex area and the File Info/tabPage2 tabs below lives
            // in its Row 1. HexRowContainer is a DIFFERENT, inner Grid (no RowDefinitions of
            // its own) that only exists to overlay the span-mode Thumb on top of HexEditor;
            // it is NOT the Grid whose row height needs to be persisted. Indexing
            // HexRowContainer.RowDefinitions[0] on its empty collection was throwing and
            // aborting the rest of DataContextChanged before Columns/BytesPerLine setup ever
            // ran, which is what made the whole hex/ascii/splitter area render as nothing.
            _rootGrid = this.FindControl<Grid>("RootGrid")!;
            _hexRowContainer = this.FindControl<Grid>("HexRowContainer")!;
            _hexEditor = this.FindControl<HexEditor>("TheHexEditor")!;
            _splitterThumb = this.FindControl<Thumb>("SpanSplitterThumb")!;

            var columns = _hexEditor.HexView.Columns;
            _offsetColumn = columns.OfType<OffsetColumn>().First();
            _hexColumn = columns.OfType<HexColumn>().First();
            _asciiColumn = columns.OfType<AsciiColumn>().First();
            columns.Insert(0, new SpacerColumn(EdgeSpacerWidth));
            columns.Add(new SpacerColumn(EdgeSpacerWidth));

            // FontFamily is set as a XAML attribute on HexEditor itself (see .axaml) --
            // HexEditor.HexView is a live existing instance, and its own FontFamily property
            // was being overwritten by HexEditor's control-theme TemplateBinding whenever it
            // was assigned directly here in code-behind instead.
            _hexEditor.HexView.BytesPerLine = FixedBytesPerLine;

            _splitterThumb.DragDelta += OnSplitterDragDelta;

            _hexRowContainer.PropertyChanged += OnHexRowContainerPropertyChanged;
            _hexEditor.Loaded += OnHexEditorLoaded;

            AttachedToVisualTree += (_, _) =>
            {
                if (DataContext is RawViewerViewModel viewModel)
                {
                    viewModel.GetTopLevel = () => TopLevel.GetTopLevel(this);
                }
            };

            DataContextChanged += (_, _) =>
            {
                if (DataContext is RawViewerViewModel viewModel)
                {
                    _viewModel = viewModel;
                    viewModel.PropertyChanged += OnViewModelPropertyChanged;

                    _rootGrid.RowDefinitions[0] = new RowDefinition(viewModel.HexRowHeight, GridUnitType.Pixel);
                    _rootGrid.RowDefinitions[0].PropertyChanged += OnHexRowDefinitionPropertyChanged;

                    ApplyMode(viewModel.SpanWindowWidth);
                }
            };

            // The ViewModel outlives this View (a fresh View instance is created on every
            // tab-switch-back), so an unsubscribed PropertyChanged handler here would keep
            // firing against this now-detached instance's own (stale) controls forever --
            // wasteful, and the same category of bug that made TextureViewerView's
            // RequestZoom/RequestZoomReset leak corrupt zoom state across tab switches.
            DetachedFromVisualTree += (_, _) =>
            {
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                }

                if (_hexScrollViewer != null)
                {
                    _hexScrollViewer.PropertyChanged -= OnHexScrollViewerPropertyChanged;
                }
            };
        }

        // TreeView-style internal-ScrollViewer discovery (see FileViewerView): HexView's own
        // ScrollOffset property isn't backed by a real AvaloniaProperty (it reflects an
        // internal ScrollViewer's state rather than being independently settable), so this
        // finds that real ScrollViewer directly and persists/restores its Offset instead --
        // Loaded (not AttachedToVisualTree) guarantees HexEditor's control template, and
        // thus its internal ScrollViewer, actually exists yet.
        private void OnHexEditorLoaded(object? sender, RoutedEventArgs e)
        {
            if (_hexScrollViewer == null)
            {
                _hexScrollViewer = _hexEditor.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
                if (_hexScrollViewer != null)
                {
                    _hexScrollViewer.PropertyChanged += OnHexScrollViewerPropertyChanged;
                }
            }

            if (_hexScrollViewer != null && _viewModel != null)
            {
                _hexScrollViewer.Offset = _viewModel.HexScrollOffset;
            }

            // The reattach bug: DataContextChanged (where ApplyMode is normally invoked) can
            // fire before HexRowContainer has real layout, so a span-mode recompute done
            // there silently no-ops on a zero/stale Bounds.Width and BytesPerLine is left at
            // whatever FixedBytesPerLine was just set to -- visible as the splitter always
            // landing over column 10 (position 16) regardless of the persisted span
            // fraction. Loaded fires once real layout has actually happened, so reapplying
            // here is what actually restores the correct span width on tab-switch-back.
            if (_viewModel is { SpanWindowWidth: true })
            {
                ApplySpanBytesPerLine();
            }
        }

        private void OnHexScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ScrollViewer.OffsetProperty &&
                sender is ScrollViewer scrollViewer &&
                _viewModel != null)
            {
                _viewModel.HexScrollOffset = scrollViewer.Offset;
            }
        }

        private void OnHexRowDefinitionPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == RowDefinition.HeightProperty &&
                sender is RowDefinition rowDefinition &&
                _viewModel != null)
            {
                _viewModel.HexRowHeight = rowDefinition.Height.Value;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RawViewerViewModel.SpanWindowWidth) &&
                DataContext is RawViewerViewModel viewModel)
            {
                ApplyMode(viewModel.SpanWindowWidth);
            }
        }

        // Resizing the window while span mode is active recomputes bytes-per-line (and
        // repositions the splitter) so the Hex/Ascii split keeps the same PROPORTION of the
        // available content width -- which, combined with the hard viewport-fit clamp in
        // RecomputeBytesPerLine, is also what proportionally shrinks the hex region (moving
        // the splitter with it) if the window is narrowed while span was previously dragged
        // out to its maximum.
        private void OnHexRowContainerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == BoundsProperty &&
                DataContext is RawViewerViewModel { SpanWindowWidth: true })
            {
                ApplySpanBytesPerLine();
            }
        }

        private void ApplyMode(bool spanWindowWidth)
        {
            if (spanWindowWidth)
            {
                ApplySpanBytesPerLine();
            }
            else
            {
                _hexEditor.HexView.BytesPerLine = FixedBytesPerLine;
            }
        }

        private void ApplySpanBytesPerLine()
        {
            RecomputeBytesPerLine(_hexRowContainer.Bounds.Width);
        }

        // Solves for the bytes-per-line that best matches the ViewModel's persisted
        // SpanFraction share of the content budget, then clamps it against the actual
        // maximum that fits BOTH Hex and Ascii (plus the two spacer columns, the offset
        // column, and all four inter-column gaps) inside the available width -- this is
        // what guarantees the ASCII column can never be dragged (or resized) past the
        // viewport edge.
        //
        // Both HexColumn and AsciiColumn report a Width that's linear in bytes-per-line
        // (confirmed from AvaloniaHex's own source: CellBasedColumn.Width = base.Width +
        // WordCount*WordWidth + (WordCount-1)*GroupPadding). Rather than hand-deriving each
        // column's base/padding constants, this reads each column's OWN currently-reported
        // Width at the CURRENT bytes-per-line to back out its real per-byte slope and
        // intercept, so it stays correct even if AvaloniaHex changes unrelated internal
        // padding constants between versions.
        private void RecomputeBytesPerLine(double availableWidth)
        {
            if (_viewModel == null)
            {
                return;
            }

            var hexView = _hexEditor.HexView;
            var currentBytesPerLine = Math.Max(1, hexView.ActualBytesPerLine);

            var hexCellWidth = _hexColumn.CellSize.Width;
            var asciiCellWidth = _asciiColumn.CellSize.Width;
            if (hexCellWidth <= 0 || asciiCellWidth <= 0)
            {
                return;
            }

            var hexSlope = hexCellWidth * 3;
            var hexIntercept = _hexColumn.Width - hexSlope * currentBytesPerLine;
            var asciiSlope = asciiCellWidth;
            var asciiIntercept = _asciiColumn.Width - asciiSlope * currentBytesPerLine;

            // Layout order is LeftSpacer, gap, Offset, gap, Hex, gap, Ascii, gap, RightSpacer
            // (HexView.UpdateColumnBounds walks Columns in order, adding ColumnPadding after
            // each one) -- four gaps total, not two.
            var columnPadding = hexView.ColumnPadding;
            var fixedOverhead = EdgeSpacerWidth * 2 + columnPadding * 4 + _offsetColumn.Width;
            var contentBudget = availableWidth - fixedOverhead;

            var desiredHexWidth = contentBudget * _viewModel.SpanFraction;
            var desiredN = (int)Math.Round((desiredHexWidth - hexIntercept) / hexSlope);

            var maxN = (int)Math.Floor((contentBudget - hexIntercept - asciiIntercept) / (hexSlope + asciiSlope));

            var candidate = Math.Clamp(desiredN, MinSpanBytesPerLine, Math.Max(MinSpanBytesPerLine, maxN));

            hexView.BytesPerLine = candidate;
            _hexEditor.UpdateLayout();

            RepositionSplitterThumb();
        }

        // Centers the thumb within the real empty gap between Hex and Ascii (that gap is
        // exactly one ColumnPadding wide, per HexView.UpdateColumnBounds's own layout).
        private void RepositionSplitterThumb()
        {
            var columnPadding = _hexEditor.HexView.ColumnPadding;
            var hexRightEdge = EdgeSpacerWidth + columnPadding * 2 + _offsetColumn.Width + _hexColumn.Width;
            var gapCenter = hexRightEdge + columnPadding / 2;
            _splitterThumb.Margin = new Thickness(gapCenter - _splitterThumb.Width / 2, 0, 0, 0);
        }

        private void OnSplitterDragDelta(object? sender, VectorEventArgs e)
        {
            if (_viewModel == null)
            {
                return;
            }

            var hexView = _hexEditor.HexView;
            var availableWidth = _hexRowContainer.Bounds.Width;
            var columnPadding = hexView.ColumnPadding;
            var fixedOverhead = EdgeSpacerWidth * 2 + columnPadding * 4 + _offsetColumn.Width;
            var contentBudget = availableWidth - fixedOverhead;
            if (contentBudget <= 0)
            {
                return;
            }

            var newHexWidth = Math.Clamp(_hexColumn.Width + e.Vector.X, 0, contentBudget);
            _viewModel.SpanFraction = Math.Clamp(newHexWidth / contentBudget, 0.02, 0.98);

            ApplySpanBytesPerLine();
        }

        // A zero-content column used purely to reserve fixed breathing room at the start/end
        // of the column layout. HexView.UpdateColumnBounds walks Columns generically and
        // includes whatever width each one reports, so a real column is the only way to
        // reserve space that HexView's own layout (and its header background, which spans
        // HexView's own Bounds.Width regardless of where individual columns sit within it)
        // correctly accounts for.
        private sealed class SpacerColumn : Column
        {
            public override Size MinimumSize { get; }

            public SpacerColumn(double width)
            {
                MinimumSize = new Size(width, 0);
            }

            public override void Measure()
            {
            }

            public override TextLine? CreateTextLine(VisualBytesLine line) => null;
        }
    }
}
