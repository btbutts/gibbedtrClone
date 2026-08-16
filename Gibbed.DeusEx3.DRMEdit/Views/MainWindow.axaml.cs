using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Gibbed.DeusEx3.DRMEdit.ViewModels;
using Gibbed.DeusEx3.DRMEdit.Views.TitleBar;

namespace Gibbed.DeusEx3.DRMEdit.Views
{
    public partial class MainWindow : Window
    {
        private const double MinTabWidth = 72;
        private const double MaxTabWidth = 200;

        // Must match the Border.tabTitle margins in MainWindow.axaml's hover/selected
        // styles (Margin="10,0,44,0") -- the fixed left padding and the width reserved for
        // the pop-out/close buttons, used to convert the pixel constants below into
        // per-tab fractions of the title text area's own (variable) width.
        private const double TabTitleLeftPadding = 10;
        private const double TabTitleButtonsZoneWidth = 44;

        // Tunable: blank buffer immediately left of the pop-out button where the title text
        // must already be fully invisible (filled only by the tab's own background color).
        private const double TabFadeKeepOutWidth = 6;

        // Tunable: horizontal pixel distance, immediately left of the keep-out zone above,
        // over which the title text fades from fully visible to fully invisible.
        private const double TabFadeWidth = 5;

        private readonly Grid _tabsHostGrid;
        private readonly ListBox _tabListBox;
        private readonly ScrollViewer _tabScrollViewer;
        private Point? _tabDragStart;
        private DocumentTabViewModel? _tabDragDocument;
        private bool _isDraggingTab;

        public MainWindow()
        {
            InitializeComponent();

            var isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            var controls = this.FindControl<StackPanel>("WindowControlsHost")!;

            if (isMac)
            {
                controls.Children.Add(new MacTitleBarButtons());
                DockPanel.SetDock(controls, Dock.Left);
            }
            else
            {
                controls.Children.Add(new WindowsTitleBarButtons());
            }

            // Drag affordance lives on a dedicated background layer behind the title bar's
            // content, not on the content itself: the title text is IsHitTestVisible="False"
            // so clicks fall through to it, and the tab ListBox has no background of its own
            // so empty space between tabs falls through too. Marking foreground content
            // (the tab ListBox, its buttons) as TitleBar directly swallows their clicks.
            var dragLayer = this.FindControl<Border>("TitleBarDragLayer")!;
            WindowDecorationProperties.SetElementRole(dragLayer, WindowDecorationsElementRole.TitleBar);

            // Tunnel phase: ListBoxItem's own click-to-select handling marks PointerPressed
            // handled during the bubble phase, so a bubble-routed handler on the ListBox
            // (the default for a plain XAML PointerPressed="..." attribute) never sees the
            // press. Tunnel handlers run first, before the event reaches the item.
            _tabListBox = this.FindControl<ListBox>("TabListBox")!;
            _tabScrollViewer = this.FindControl<ScrollViewer>("TabScrollViewer")!;
            _tabListBox.AddHandler(PointerPressedEvent, OnTabPointerPressed, RoutingStrategies.Tunnel);
            _tabListBox.AddHandler(PointerMovedEvent, OnTabPointerMoved, RoutingStrategies.Tunnel);
            _tabListBox.AddHandler(PointerReleasedEvent, OnTabPointerReleased, RoutingStrategies.Tunnel);
            _tabListBox.PointerCaptureLost += (_, _) => EndTabDrag();

            _tabsHostGrid = this.FindControl<Grid>("TabsHostGrid")!;
            _tabsHostGrid.PropertyChanged += (_, args) =>
            {
                if (args.Property == BoundsProperty)
                {
                    RecomputeTabWidths();
                }
            };

            DataContextChanged += (_, _) =>
            {
                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.OpenDocuments.CollectionChanged += OnOpenDocumentsChanged;
                    RecomputeTabWidths();
                }
            };
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

        private void OnOpenDocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RecomputeTabWidths();
        }

        // Chrome/Edge-style tab sizing: every tab is the same width, shrinking together
        // (down to MinTabWidth) as more tabs are opened so they all keep fitting the
        // available space, rather than each tab keeping a fixed width and pushing the
        // strip into horizontal scroll as soon as one more tab is opened.
        private void RecomputeTabWidths()
        {
            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var count = viewModel.OpenDocuments.Count;
            if (count == 0)
            {
                return;
            }

            var available = _tabsHostGrid.Bounds.Width;
            var width = Math.Clamp(available / count, MinTabWidth, MaxTabWidth);

            var textAreaWidth = Math.Max(1, width - TabTitleLeftPadding - TabTitleButtonsZoneWidth);
            var fadeEndFraction = Math.Clamp((textAreaWidth - TabFadeKeepOutWidth) / textAreaWidth, 0, 1);
            var fadeStartFraction = Math.Clamp((textAreaWidth - TabFadeKeepOutWidth - TabFadeWidth) / textAreaWidth, 0, 1);

            // A fresh LinearGradientBrush per tab, not one shared instance: Avalonia's
            // render layer appears to cache the resolved gradient geometry per-brush-
            // instance rather than strictly per-target, so sharing one instance as the
            // OpacityMask of multiple differently-positioned TextBlocks let the first tab's
            // rendered geometry bleed into later tabs despite identical RelativeUnit stops.
            foreach (var document in viewModel.OpenDocuments)
            {
                document.Width = width;
                document.TitleFadeMask = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Colors.White, fadeStartFraction),
                        new GradientStop(Colors.Transparent, fadeEndFraction),
                    },
                };
            }

            EnsureSelectedTabVisible(viewModel);
        }

        // Shrinking the window narrows every tab (above) without ever moving
        // TabScrollViewer's own scroll offset, which is otherwise only adjusted by
        // ListBox's own selection-driven scroll-into-view (still active; unaffected by the
        // BringIntoViewOnFocusChange="False" set on TabListBox, which only gates
        // focus-driven scrolling). So resizing the window narrow enough that the strip goes
        // from "everything fits" to "needs horizontal scroll" can leave the already-selected
        // tab sitting past the new, narrower viewport with no scroll adjustment ever
        // triggered for it -- most visibly the rightmost tab, since it has the most scroll
        // distance to fall behind by. UpdateLayout() forces the just-applied Width changes
        // to actually resolve into real container bounds before they're read.
        private void EnsureSelectedTabVisible(MainWindowViewModel viewModel)
        {
            if (viewModel.SelectedDocument == null)
            {
                return;
            }

            var index = viewModel.OpenDocuments.IndexOf(viewModel.SelectedDocument);
            if (index < 0)
            {
                return;
            }

            _tabScrollViewer.UpdateLayout();

            if (_tabListBox.ContainerFromIndex(index) is not Control container)
            {
                return;
            }

            var itemLeft = (container.TranslatePoint(new Point(0, 0), _tabListBox) ?? default).X;
            var itemRight = itemLeft + container.Bounds.Width;

            var offset = _tabScrollViewer.Offset;
            var viewportWidth = _tabScrollViewer.Viewport.Width;

            var newOffsetX = offset.X;
            if (itemLeft < offset.X)
            {
                newOffsetX = itemLeft;
            }
            else if (itemRight > offset.X + viewportWidth)
            {
                newOffsetX = itemRight - viewportWidth;
            }

            if (Math.Abs(newOffsetX - offset.X) > 0.01)
            {
                var maxOffsetX = Math.Max(0, _tabScrollViewer.Extent.Width - viewportWidth);
                _tabScrollViewer.Offset = new Vector(Math.Clamp(newOffsetX, 0, maxOffsetX), offset.Y);
            }
        }

        // Live tab reordering: no native OS drag-and-drop (its default ghost-image/cursor
        // chrome can't be suppressed cleanly on Windows). Instead, track the pointer while
        // captured and swap the dragged tab with whichever tab's real rendered bounds the
        // cursor is currently over -- an instant swap on each boundary crossed, no dragged
        // visual follows the cursor, no cursor changes.
        private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed != true)
            {
                return;
            }

            var item = (e.Source as Control)?.FindAncestorOfType<ListBoxItem>(true);
            if (item?.DataContext is not DocumentTabViewModel document)
            {
                return;
            }

            _tabDragDocument = document;
            _tabDragStart = e.GetPosition(_tabListBox);
        }

        private void OnTabPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_tabDragDocument == null || _tabDragStart == null)
            {
                return;
            }

            if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed != true)
            {
                EndTabDrag();
                return;
            }

            if (DataContext is not MainWindowViewModel viewModel)
            {
                return;
            }

            var current = e.GetPosition(_tabListBox);

            if (!_isDraggingTab)
            {
                if (Point.Distance(current, _tabDragStart.Value) < 4)
                {
                    return;
                }

                _isDraggingTab = true;
                e.Pointer.Capture(_tabListBox);
            }

            var draggedIndex = viewModel.OpenDocuments.IndexOf(_tabDragDocument);
            if (draggedIndex < 0)
            {
                return;
            }

            for (var i = 0; i < viewModel.OpenDocuments.Count; i++)
            {
                if (i == draggedIndex)
                {
                    continue;
                }

                if (_tabListBox.ContainerFromIndex(i) is not Control container)
                {
                    continue;
                }

                var topLeft = container.TranslatePoint(new Point(0, 0), _tabListBox) ?? default;
                var bounds = new Rect(topLeft, container.Bounds.Size);
                if (current.X >= bounds.Left && current.X <= bounds.Right)
                {
                    // ObservableCollection.Move raises CollectionChanged with Action=Move,
                    // but ListBox's selection tracking doesn't treat that as "the selected
                    // item just changed position" -- it clears SelectedItem, which (being
                    // two-way bound) blanks SelectedDocument and the whole content area.
                    // Reassert it immediately so a reordered active tab stays active.
                    var wasSelected = ReferenceEquals(viewModel.SelectedDocument, _tabDragDocument);
                    viewModel.OpenDocuments.Move(draggedIndex, i);
                    if (wasSelected)
                    {
                        viewModel.SelectedDocument = _tabDragDocument;
                    }
                    break;
                }
            }
        }

        private void OnTabPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isDraggingTab)
            {
                e.Pointer.Capture(null);
            }

            EndTabDrag();
        }

        private void EndTabDrag()
        {
            _tabDragDocument = null;
            _tabDragStart = null;
            _isDraggingTab = false;
        }
    }
}
