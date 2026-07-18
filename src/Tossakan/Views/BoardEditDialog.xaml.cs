using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Tossakan.Helpers;
using Tossakan.Services;

namespace Tossakan.Views;

public sealed partial class BoardEditDialog : ContentDialog
{
    private static readonly SolidColorBrush SelectedBrush = new(Microsoft.UI.Colors.DodgerBlue);
    private static readonly SolidColorBrush UnselectedBrush = new(Microsoft.UI.Colors.Transparent);

    // null = no photo (use color); DefaultImageMarker = bundled photo; anything else = a custom photo
    // (either already stored on disk, or a freshly browsed file that still needs to be imported on save).
    private string? _selectedPath;
    private bool _isNewCustomPick;

    public BoardEditDialog(string dialogTitle, string initialName = "", string? initialColor = null, string? initialImagePath = null)
    {
        InitializeComponent();
        Title = dialogTitle;
        NameBox.Text = initialName;
        PaletteView.ItemsSource = Ui.BoardPalette;
        PaletteView.SelectedItem = initialColor is not null && Ui.BoardPalette.Contains(initialColor)
            ? initialColor
            : Ui.BoardPalette[0];
        IsPrimaryButtonEnabled = initialName.Trim().Length > 0;
        NameBox.TextChanged += (_, _) => IsPrimaryButtonEnabled = NameBox.Text.Trim().Length > 0;

        if (File.Exists(BackgroundImageService.DefaultImagePath))
            DefaultPhotoImage.Source = new BitmapImage(new Uri(BackgroundImageService.DefaultImagePath));

        _selectedPath = initialImagePath switch
        {
            BackgroundImageService.DefaultImageMarker => BackgroundImageService.DefaultImageMarker,
            not null when File.Exists(initialImagePath) => initialImagePath,
            _ => null,
        };
        if (_selectedPath is not null && _selectedPath != BackgroundImageService.DefaultImageMarker)
        {
            CustomPhotoImage.Source = new BitmapImage(new Uri(_selectedPath));
            CustomPhotoSwatch.Visibility = Visibility.Visible;
        }
        UpdateSwatchHighlight();
    }

    public string BoardName => NameBox.Text.Trim();
    public string SelectedColor => (string?)PaletteView.SelectedItem ?? Ui.BoardPalette[0];

    /// <summary>The final image path to store as-is (null, the bundled default, or an already-stored custom photo).</summary>
    public string? SelectedImagePath => _isNewCustomPick ? null : _selectedPath;

    /// <summary>Set when the user just picked a new file that still needs to be copied into app storage.</summary>
    public string? PendingImageSourcePath => _isNewCustomPick ? _selectedPath : null;

    private void NoPhoto_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _selectedPath = null;
        _isNewCustomPick = false;
        UpdateSwatchHighlight();
    }

    private void DefaultPhoto_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _selectedPath = BackgroundImageService.DefaultImageMarker;
        _isNewCustomPick = false;
        UpdateSwatchHighlight();
    }

    private void CustomPhoto_Tapped(object sender, TappedRoutedEventArgs e) => UpdateSwatchHighlight();

    private async void BrowseImage_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".bmp" })
            picker.FileTypeFilter.Add(ext);
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;

        CustomPhotoImage.Source = new BitmapImage(new Uri(file.Path));
        CustomPhotoSwatch.Visibility = Visibility.Visible;
        _selectedPath = file.Path;
        _isNewCustomPick = true;
        UpdateSwatchHighlight();
    }

    private void UpdateSwatchHighlight()
    {
        NoPhotoSwatch.BorderBrush = _selectedPath is null ? SelectedBrush : UnselectedBrush;
        DefaultPhotoSwatch.BorderBrush = _selectedPath == BackgroundImageService.DefaultImageMarker ? SelectedBrush : UnselectedBrush;
        CustomPhotoSwatch.BorderBrush = _selectedPath is not null && _selectedPath != BackgroundImageService.DefaultImageMarker
            ? SelectedBrush : UnselectedBrush;
    }
}
