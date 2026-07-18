using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Tossakan.Services;

namespace Tossakan.Views;

public sealed partial class SettingsPage : Page
{
    private static readonly SolidColorBrush SelectedBrush = new(Microsoft.UI.Colors.DodgerBlue);
    private static readonly SolidColorBrush UnselectedBrush = new(Microsoft.UI.Colors.Transparent);

    private readonly AppSettingsService _settings = App.Services.GetRequiredService<AppSettingsService>();
    private readonly BackgroundImageService _backgroundImages = App.Services.GetRequiredService<BackgroundImageService>();

    // null = no photo; HomeDefaultImageMarker = bundled photo; anything else = a custom photo
    // (either already stored on disk, or a freshly browsed file that still needs to be imported on save).
    private string? _selectedPath;
    private bool _isNewCustomPick;
    private string? _savedPath;

    public SettingsPage()
    {
        InitializeComponent();
        if (File.Exists(BackgroundImageService.DefaultHomeImagePath))
            DefaultPhotoImage.Source = new BitmapImage(new Uri(BackgroundImageService.DefaultHomeImagePath));
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        SavedText.Visibility = Visibility.Collapsed;

        var settings = await _settings.LoadAsync();
        _savedPath = settings.HomeBackgroundImagePath;
        _selectedPath = _savedPath switch
        {
            BackgroundImageService.HomeDefaultImageMarker => BackgroundImageService.HomeDefaultImageMarker,
            not null when File.Exists(_savedPath) => _savedPath,
            _ => null,
        };
        _isNewCustomPick = false;

        if (_selectedPath is not null && _selectedPath != BackgroundImageService.HomeDefaultImageMarker)
        {
            CustomPhotoImage.Source = new BitmapImage(new Uri(_selectedPath));
            CustomPhotoSwatch.Visibility = Visibility.Visible;
        }
        else
        {
            CustomPhotoSwatch.Visibility = Visibility.Collapsed;
        }
        UpdateSwatchHighlight();
    }

    private void NoPhoto_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _selectedPath = null;
        _isNewCustomPick = false;
        UpdateSwatchHighlight();
    }

    private void DefaultPhoto_Tapped(object sender, TappedRoutedEventArgs e)
    {
        _selectedPath = BackgroundImageService.HomeDefaultImageMarker;
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
        DefaultPhotoSwatch.BorderBrush = _selectedPath == BackgroundImageService.HomeDefaultImageMarker ? SelectedBrush : UnselectedBrush;
        CustomPhotoSwatch.BorderBrush = _selectedPath is not null && _selectedPath != BackgroundImageService.HomeDefaultImageMarker
            ? SelectedBrush : UnselectedBrush;
        SavedText.Visibility = Visibility.Collapsed;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        var imagePath = _isNewCustomPick && _selectedPath is not null
            ? await _backgroundImages.ImportCustomImageAsync(_selectedPath)
            : _selectedPath;

        await _settings.SaveAsync(new AppSettings { HomeBackgroundImagePath = imagePath });

        if (_savedPath is not null && _savedPath != imagePath && BackgroundImageService.IsOwnedCustomImage(_savedPath))
        {
            try { if (File.Exists(_savedPath)) File.Delete(_savedPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        _savedPath = imagePath;
        _isNewCustomPick = false;
        SavedText.Visibility = Visibility.Visible;
    }
}
