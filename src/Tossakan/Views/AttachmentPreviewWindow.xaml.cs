using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Tossakan.Helpers;
using Tossakan.ViewModels;
using Windows.Foundation;

namespace Tossakan.Views;

/// <summary>A real top-level window (not a Flyout/ContentDialog) so it gets native minimize/maximize/close
/// chrome and can host a zoomable, pannable image viewer independent of the CardDetailDialog that opened it.</summary>
public sealed partial class AttachmentPreviewWindow : Window
{
    private int _pixelWidth;
    private int _pixelHeight;

    /// <summary>True until the user manually zooms; while true, resizing/maximizing the window
    /// (and the initial image decode) keeps re-fitting the image to the current viewport.</summary>
    private bool _autoFit = true;

    private bool _isPanning;
    private Point _panStartPointerPosition;
    private double _panStartHorizontalOffset;
    private double _panStartVerticalOffset;

    public AttachmentPreviewWindow(AttachmentVm attachment)
    {
        InitializeComponent();
        Title = attachment.FileName;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(900, 700));

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);

        if (attachment.IsImage && File.Exists(attachment.StoredPath))
        {
            Toolbar.Visibility = Visibility.Visible;
            Scroller.ZoomMode = ZoomMode.Enabled;
            Scroller.MinZoomFactor = 0.1f;
            Scroller.MaxZoomFactor = 8f;
            Scroller.SizeChanged += (_, _) => { if (_autoFit) Fit(); };
            Scroller.PointerWheelChanged += Scroller_PointerWheelChanged;
            Scroller.PointerPressed += Scroller_PointerPressed;
            Scroller.PointerMoved += Scroller_PointerMoved;
            Scroller.PointerReleased += Scroller_PointerReleased;
            Scroller.PointerCaptureLost += Scroller_PointerReleased;

            var bitmap = new BitmapImage();
            bitmap.ImageOpened += Bitmap_Opened;
            bitmap.UriSource = new Uri(attachment.StoredPath);
            ContentHost.Children.Add(new Image { Source = bitmap, Stretch = Stretch.None });
        }
        else if (attachment.IsMarkdown && File.Exists(attachment.StoredPath))
        {
            ContentHost.Children.Add(MarkdownRenderer.Render(File.ReadAllText(attachment.StoredPath)));
        }
        else
        {
            ContentHost.Children.Add(new TextBlock { Text = "This file type can't be previewed here." });
        }
    }

    private void Bitmap_Opened(object sender, RoutedEventArgs e)
    {
        var bitmap = (BitmapImage)sender;
        _pixelWidth = bitmap.PixelWidth;
        _pixelHeight = bitmap.PixelHeight;
        if (_autoFit) Fit();
    }

    private void Fit()
    {
        if (_pixelWidth == 0 || _pixelHeight == 0) return;
        if (Scroller.ActualWidth <= 0 || Scroller.ActualHeight <= 0) return;
        var factor = Math.Min(Scroller.ActualWidth / _pixelWidth, Scroller.ActualHeight / _pixelHeight);
        factor = Math.Clamp(factor, Scroller.MinZoomFactor, Scroller.MaxZoomFactor);
        Scroller.ChangeView(0, 0, (float)factor, true);
    }

    private void ZoomIn_Click(object sender, RoutedEventArgs e)
    {
        _autoFit = false;
        Scroller.ChangeView(null, null, (float)Math.Min(Scroller.ZoomFactor * 1.25, Scroller.MaxZoomFactor), true);
    }

    private void ZoomOut_Click(object sender, RoutedEventArgs e)
    {
        _autoFit = false;
        Scroller.ChangeView(null, null, (float)Math.Max(Scroller.ZoomFactor / 1.25, Scroller.MinZoomFactor), true);
    }

    private void ZoomFit_Click(object sender, RoutedEventArgs e)
    {
        _autoFit = true;
        Fit();
    }

    private void Scroller_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(Scroller).Properties.MouseWheelDelta;
        if (delta == 0) return;

        _autoFit = false;
        var factor = delta > 0
            ? Math.Min(Scroller.ZoomFactor * 1.1, Scroller.MaxZoomFactor)
            : Math.Max(Scroller.ZoomFactor / 1.1, Scroller.MinZoomFactor);
        Scroller.ChangeView(null, null, (float)factor, true);
        e.Handled = true;
    }

    private void Scroller_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(Scroller);
        if (!point.Properties.IsMiddleButtonPressed) return;

        _isPanning = true;
        _panStartPointerPosition = point.Position;
        _panStartHorizontalOffset = Scroller.HorizontalOffset;
        _panStartVerticalOffset = Scroller.VerticalOffset;
        Scroller.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void Scroller_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPanning) return;

        var position = e.GetCurrentPoint(Scroller).Position;
        var dx = position.X - _panStartPointerPosition.X;
        var dy = position.Y - _panStartPointerPosition.Y;
        Scroller.ChangeView(_panStartHorizontalOffset - dx, _panStartVerticalOffset - dy, null, true);
        e.Handled = true;
    }

    private void Scroller_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isPanning) return;

        _isPanning = false;
        Scroller.ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }
}
