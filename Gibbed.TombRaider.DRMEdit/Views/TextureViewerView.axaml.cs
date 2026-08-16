using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Gibbed.TombRaider.DRMEdit.ViewModels;

namespace Gibbed.TombRaider.DRMEdit.Views
{
    public partial class TextureViewerView : UserControl
    {
        public const double MinZoom = 0.1;
        public const double MaxZoom = 8.0;
        private const double WheelZoomStep = 1.1;

        private readonly ScrollViewer _scrollViewer;
        private double? _pinchStartZoom;

        public TextureViewerView()
        {
            InitializeComponent();

            _scrollViewer = this.FindControl<ScrollViewer>("ImageScrollViewer")!;
            _scrollViewer.AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged);
            _scrollViewer.AddHandler(InputElement.PinchEvent, OnPinch);
            _scrollViewer.AddHandler(InputElement.PinchEndedEvent, OnPinchEnded);

            AttachedToVisualTree += (_, _) =>
            {
                if (DataContext is TextureViewerViewModel viewModel)
                {
                    viewModel.GetTopLevel = () => TopLevel.GetTopLevel(this);
                    viewModel.RequestZoom += (_, factor) => ZoomAtCenter(factor);
                    viewModel.RequestZoomReset += (_, _) => FitToView();

                    // The ViewModel may already have decided IsZoomed==true in its
                    // constructor (before this View, and thus RequestZoomReset's only
                    // subscriber, existed) -- run the same computation once now so a
                    // freshly opened large texture actually opens fit-to-view.
                    if (viewModel.IsZoomed)
                    {
                        FitToView();
                    }
                }
            };
        }

        // Ctrl+scroll-wheel zoom: standard desktop hotkey combo for zoom, distinct from
        // plain scrolling (which the ScrollViewer already handles for panning).
        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (e.KeyModifiers != KeyModifiers.Control || DataContext is not TextureViewerViewModel viewModel)
            {
                return;
            }

            var factor = e.Delta.Y > 0 ? WheelZoomStep : 1.0 / WheelZoomStep;
            var newZoom = Math.Clamp(viewModel.ZoomFactor * factor, MinZoom, MaxZoom);
            ApplyZoomAtViewportPoint(viewModel, newZoom, e.GetPosition(_scrollViewer));
            e.Handled = true;
        }

        // Trackpad/touch pinch: ScaleOrigin arrives as a 0..1 fraction of the recognizer's
        // own element bounds (the ScrollViewer here), so it's converted to viewport pixels
        // before reusing the same anchor math as the wheel-zoom path.
        private void OnPinch(object? sender, PinchEventArgs e)
        {
            if (DataContext is not TextureViewerViewModel viewModel)
            {
                return;
            }

            _pinchStartZoom ??= viewModel.ZoomFactor;

            var newZoom = Math.Clamp(_pinchStartZoom.Value * e.Scale, MinZoom, MaxZoom);
            var anchor = new Point(e.ScaleOrigin.X * _scrollViewer.Bounds.Width, e.ScaleOrigin.Y * _scrollViewer.Bounds.Height);
            ApplyZoomAtViewportPoint(viewModel, newZoom, anchor);
            e.Handled = true;
        }

        private void OnPinchEnded(object? sender, PinchEndedEventArgs e)
        {
            _pinchStartZoom = null;
        }

        // Zooms so that the content point currently under `viewportPoint` (in ScrollViewer
        // viewport pixel coordinates) stays under that same viewport point after the zoom.
        // The offset can only be read back correctly after Avalonia re-measures/re-arranges
        // the LayoutTransformControl at the new scale, which UpdateLayout() forces
        // synchronously. A single deferred Dispatcher.Post at a fixed priority was tried
        // first and was unreliable: crossing the threshold where an Auto scrollbar
        // shows/hides triggers a SECOND layout pass after the posted callback already read
        // a stale Viewport size, producing a visible diagonal drift specifically on the
        // step that toggled a scrollbar. UpdateLayout() drains the whole invalidation queue
        // (including that second pass) before Extent/Viewport are read.
        private void ApplyZoomAtViewportPoint(TextureViewerViewModel viewModel, double newZoom, Point viewportPoint)
        {
            var oldZoom = viewModel.ZoomFactor;
            if (Math.Abs(newZoom - oldZoom) < 0.0001)
            {
                return;
            }

            var viewportVector = new Vector(viewportPoint.X, viewportPoint.Y);
            var contentPoint = (_scrollViewer.Offset + viewportVector) / oldZoom;
            var newOffset = (contentPoint * newZoom) - viewportVector;

            viewModel.ZoomFactor = newZoom;
            _scrollViewer.UpdateLayout();

            var maxX = Math.Max(0, _scrollViewer.Extent.Width - _scrollViewer.Viewport.Width);
            var maxY = Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height);
            _scrollViewer.Offset = new Vector(
                Math.Clamp(newOffset.X, 0, maxX),
                Math.Clamp(newOffset.Y, 0, maxY));
        }

        // Used by the Zoom In / Zoom Out toolbar buttons. Deliberately NOT the same
        // point-preserving anchor math as wheel/pinch: there's no cursor position to anchor
        // to, and re-anchoring around the viewport's geometric center (rather than the
        // image's own rendered center) was what caused the originally reported diagonal
        // drift, since undersized content isn't necessarily centered in a larger viewport
        // by default. Recentering unconditionally after every button zoom step is both
        // simpler and what was actually asked for: buttons always re-center, they never
        // preserve a point.
        public void ZoomAtCenter(double factor)
        {
            if (DataContext is not TextureViewerViewModel viewModel)
            {
                return;
            }

            viewModel.ZoomFactor = Math.Clamp(viewModel.ZoomFactor * factor, MinZoom, MaxZoom);
            CenterContent();
        }

        // Fit-to-view is computed directly from real geometry rather than delegated to
        // Image's Stretch=Uniform: the ScrollViewer's own Bounds (its DockPanel-allocated
        // size -- from the top of the toolbar to the top of the InfoText bar, full width)
        // is exactly the "actual allowable render area" fit-to-view should target, unlike
        // Viewport, which shrinks once a scrollbar is already showing from a PRIOR zoom
        // level. Using the shorter of the two width/height ratios keeps the image's aspect
        // ratio intact and guarantees the longer axis never overflows into needing a
        // scrollbar, which is what "fit to view" means.
        private void FitToView()
        {
            if (DataContext is not TextureViewerViewModel viewModel || viewModel.Preview == null)
            {
                return;
            }

            var pixelSize = viewModel.Preview.PixelSize;
            if (pixelSize.Width <= 0 || pixelSize.Height <= 0)
            {
                return;
            }

            _scrollViewer.UpdateLayout();
            var bounds = _scrollViewer.Bounds;
            var scale = Math.Min(bounds.Width / pixelSize.Width, bounds.Height / pixelSize.Height);

            viewModel.ApplyFitZoom(Math.Clamp(scale, MinZoom, MaxZoom));
            CenterContent();
        }

        private void CenterContent()
        {
            _scrollViewer.UpdateLayout();
            var maxX = Math.Max(0, _scrollViewer.Extent.Width - _scrollViewer.Viewport.Width);
            var maxY = Math.Max(0, _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height);
            _scrollViewer.Offset = new Vector(maxX / 2, maxY / 2);
        }
    }
}
