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
using Gibbed.TombRaider.DRMEdit.ViewModels;
using Gibbed.TombRaider.DRMEdit.Views.TitleBar;

namespace Gibbed.TombRaider.DRMEdit.Views
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

        // Tunable: minimum breathing room kept between the File/Windows menu and the
        // centered toolbar button group when the window is narrow enough that the group
        // would otherwise overlap the menu.
        private const double ToolbarMenuButtonGap = 8;

        // Tunable: fraction of the primary screen's own working-area width (DIP-corrected,
        // not raw device pixels -- see RecomputeMinWindowWidth) added to the title bar
        // text's footprint to form the window's minimum width. A fraction of the actual
        // display's own resolution, rather than a fixed pixel constant, keeps the minimum
        // proportionate whether the app runs on a 1080p, 1440p, or 4k screen -- a fixed
        // pixel count would eat a very different fraction of the viewport on each.
        private const double MinWidthScreenFraction = 1.0 / 8.0;

        // Tunable: gap kept between the window controls and whatever sits immediately next
        // to them once repositioned for macOS (see RepositionForMacWindowControls) -- on
        // Windows/Linux this same breathing room is provided by WindowControlsHost's own
        // XAML Margin="8,0,0,0" instead, which only makes sense for its default position at
        // the right of the tabs, not the left of the title text.
        // Bumped from 8 to 16 per user feedback testing on real macOS hardware: the
        // original gap read as too tight between the traffic lights and the title text.
        private const double MacWindowControlsGap = 16;

        private readonly Grid _tabsHostGrid;
        private readonly ListBox _tabListBox;
        private readonly ScrollViewer _tabScrollViewer;
        private readonly Grid _toolbarRootGrid;
        private readonly Menu _toolbarMenu;
        private readonly StackPanel _toolbarButtonsPanel;
        private readonly TextBlock _titleText;
        private readonly StackPanel _windowControlsHost;
        private readonly Grid _titleBarLayoutGrid;
        private Point? _tabDragStart;
        private DocumentTabViewModel? _tabDragDocument;
        private bool _isDraggingTab;

        public MainWindow()
        {
            InitializeComponent();

            var isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            var controls = this.FindControl<StackPanel>("WindowControlsHost")!;
            _windowControlsHost = controls;

            if (isMac)
            {
                controls.Children.Add(new MacTitleBarButtons());
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

            _toolbarRootGrid = this.FindControl<Grid>("ToolbarRootGrid")!;
            _toolbarMenu = this.FindControl<Menu>("ToolbarMenu")!;
            _toolbarButtonsPanel = this.FindControl<StackPanel>("ToolbarButtonsPanel")!;
            _titleText = this.FindControl<TextBlock>("TitleText")!;
            _titleBarLayoutGrid = this.FindControl<Grid>("TitleBarLayoutGrid")!;

            if (isMac)
            {
                RepositionForMacWindowControls();
            }

            // The root Grid's Bounds change on window resize, the menu's on theme/font
            // changes, and the buttons panel's whenever the per-tab dynamic toolbar content
            // swaps (ContentControl rebuild on tab switch) or the "Open DRM" text
            // shows/hides -- recomputing the overlap clamp from any of them keeps it in sync
            // with current layout rather than a one-time snapshot.
            foreach (var control in new Control[] { _toolbarRootGrid, _toolbarMenu, _toolbarButtonsPanel })
            {
                control.PropertyChanged += (_, args) =>
                {
                    if (args.Property == BoundsProperty)
                    {
                        RecomputeToolbarButtonsLeft();
                    }
                };
            }

            // Deliberately NOT re-run from _toolbarButtonsPanel's or _windowControlsHost's
            // Bounds: the minimum width must stay the same across every tab regardless of
            // which per-tab toolbar buttons happen to be showing (previously it wasn't,
            // which is what made the window's minimum size balloon specifically on the
            // texture tab's larger button set). Title text width only changes if the window
            // Title binding itself changes, which this app never does after startup, so a
            // single computation once real Bounds exist is sufficient -- still wired to
            // PropertyChanged rather than a one-shot call so it recomputes correctly even if
            // TitleText's first Bounds report is still 0x0 pre-layout.
            _titleText.PropertyChanged += (_, args) =>
            {
                if (args.Property == BoundsProperty)
                {
                    RecomputeMinWindowWidth();
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

            // Popped-out windows are independent Window instances, not children of this
            // one, so closing MainWindow alone leaves them open (the app keeps running
            // under the default OnLastWindowClose shutdown mode until the user closes each
            // of those too). The original WinForms MDI parent closed every child
            // automatically; CloseAllCommand already reaches both docked tabs and
            // popped-out documents, so running it here on Closing restores that behavior.
            Closing += (_, _) =>
            {
                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.CloseAllCommand.Execute(null);
                }
            };
        }

        // Keeps the centered Open DRM + dynamic per-tab toolbar button group horizontally
        // centered within the FULL toolbar row width when there's room, but never lets it
        // move further left than immediately after the File/Windows menu's own footprint
        // (plus a small gap) once the window gets too narrow for true centering -- avoids
        // the buttons sliding underneath/overlapping the menu instead of wrapping or
        // clipping. HorizontalAlignment="Left" is set on ToolbarButtonsPanel in XAML so this
        // Margin.Left is the only thing positioning it; Center alignment would otherwise
        // re-center within the space this margin already carved out.
        private void RecomputeToolbarButtonsLeft()
        {
            var available = _toolbarRootGrid.Bounds.Width;
            var buttonsWidth = _toolbarButtonsPanel.Bounds.Width;
            if (available <= 0 || buttonsWidth <= 0)
            {
                return;
            }

            var menuRight = _toolbarMenu.Bounds.Width + _toolbarMenu.Margin.Left + _toolbarMenu.Margin.Right;
            var centeredLeft = (available - buttonsWidth) / 2;
            var clampedLeft = Math.Max(centeredLeft, menuRight + ToolbarMenuButtonGap);

            var currentMargin = _toolbarButtonsPanel.Margin;
            if (Math.Abs(currentMargin.Left - clampedLeft) > 0.5)
            {
                _toolbarButtonsPanel.Margin = new Thickness(clampedLeft, currentMargin.Top, 0, currentMargin.Bottom);
            }
        }

        // Minimum window width = title bar text's own footprint (its left/right margin plus
        // its text width -- i.e. exactly how far right the first tab starts, since the title
        // TextBlock occupies the Grid's Auto-sized first column) + a tunable fraction of the
        // PRIMARY SCREEN's own working-area width. Using the actual display's resolution
        // (DIP-corrected via Scaling, not raw device pixels -- otherwise a 4k screen's
        // larger pixel count alone would produce a far bigger minimum than a 1080p screen
        // even though both should feel proportionately similar) rather than a hardcoded
        // pixel constant is what keeps this reasonable across 1080p/1440p/4k and similar
        // displays without needing a different constant per resolution class. Deliberately
        // NOT a function of toolbar button count/width or window control button width: this
        // must stay identical across every tab, and tying it to per-tab dynamic toolbar
        // content (the previous approach) was what made the window balloon in size
        // specifically when switching to the texture tab's larger button set.
        private void RecomputeMinWindowWidth()
        {
            var titleWidth = _titleText.Bounds.Width + _titleText.Margin.Left + _titleText.Margin.Right;
            if (titleWidth <= 0)
            {
                return;
            }

            var screen = Screens.Primary;
            if (screen == null)
            {
                return;
            }

            var screenWidthDip = screen.WorkingArea.Width / screen.Scaling;
            MinWidth = titleWidth + (screenWidthDip * MinWidthScreenFraction);
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

        // macOS convention puts window controls (traffic lights) at the top-LEFT, not the
        // top-right like Windows/Linux. The XAML layout defaults to the Windows/Linux
        // arrangement (TitleBarLayoutGrid = "Auto,*,Auto": title, tabs, then controls);
        // getting the (already correctly macOS-styled, via MacTitleBarButtons) controls to
        // actually RENDER on the left requires changing which column each of the three
        // elements occupies, and since the flexible tabs column has to stay the star column
        // wherever it ends up, the ColumnDefinitions themselves change shape too, not just
        // each element's Grid.Column. A previous version of this code called
        // DockPanel.SetDock(controls, Dock.Left) here instead, which silently did nothing:
        // WindowControlsHost lives inside this Grid, not a DockPanel, so that attached
        // property was never read by anything laying it out.
        //
        // Not yet verified on real macOS hardware (this project currently only builds and
        // runs on Windows) -- this is a best-effort layout fix based on reading the code,
        // not something that's been visually confirmed. See CLAUDE.md's Known Issues
        // section for the full note.
        private void RepositionForMacWindowControls()
        {
            _titleBarLayoutGrid.ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*");

            Grid.SetColumn(_windowControlsHost, 0);
            Grid.SetColumn(_titleText, 1);
            Grid.SetColumn(_tabsHostGrid, 2);

            // WindowControlsHost's XAML Margin="8,0,0,0" exists to gap it away from the tabs
            // that used to sit immediately to its left; now it's the leftmost element, so
            // that gap belongs on its right (toward the title text) instead. MacTitleBarButtons
            // already carries its own left inset for the traffic lights themselves (see that
            // file's Margin="12,0,0,0"), so this is purely the gap AFTER the controls.
            _windowControlsHost.Margin = new Thickness(0, 0, MacWindowControlsGap, 0);

            // TitleText's original Margin="12,0,16,0" assumed it was the leftmost element;
            // its left inset is no longer needed now that the window controls (plus the gap
            // just set above) already provide that space, so only the original right margin
            // (breathing room before the tabs) is kept.
            _titleText.Margin = new Thickness(0, 0, 16, 0);
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
