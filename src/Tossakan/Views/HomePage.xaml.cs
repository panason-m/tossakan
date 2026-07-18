using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Tossakan.Helpers;
using Tossakan.Models;
using Tossakan.Services;

namespace Tossakan.Views;

public sealed partial class HomePage : Page
{
    private readonly BoardService _service = App.Services.GetRequiredService<BoardService>();
    private readonly BackgroundImageService _backgroundImages = App.Services.GetRequiredService<BackgroundImageService>();
    private readonly AppSettingsService _settings = App.Services.GetRequiredService<AppSettingsService>();
    private readonly DatabaseChangeWatcherService _dbWatcher = App.Services.GetRequiredService<DatabaseChangeWatcherService>();

    public HomePage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        var settings = await _settings.LoadAsync();
        RootGrid.Background = Ui.ToBackgroundBrush(settings.HomeBackgroundImagePath, "");
        await LoadAsync();
        _dbWatcher.DatabaseChanged += OnDatabaseChanged;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _dbWatcher.DatabaseChanged -= OnDatabaseChanged;
    }

    private void OnDatabaseChanged(object? sender, EventArgs e) =>
        DispatcherQueue.TryEnqueue(async () => await LoadAsync());

    private async Task LoadAsync()
    {
        var all = await _service.GetBoardsAsync(includeArchived: true);
        BoardsView.ItemsSource = all.Where(b => !b.IsArchived).ToList();
        var archived = all.Where(b => b.IsArchived).ToList();
        ArchivedBoardsView.ItemsSource = archived;
        ArchivedExpander.Visibility = archived.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BoardsView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Board board)
            Frame.Navigate(typeof(BoardPage), new BoardPage.NavigationArgs(board.Id, null));
    }

    private async void CreateBoard_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new BoardEditDialog("Create board", initialImagePath: BackgroundImageService.DefaultImageMarker)
            { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            var imagePath = dialog.PendingImageSourcePath is string src
                ? await _backgroundImages.ImportCustomImageAsync(src)
                : dialog.SelectedImagePath;
            await _service.CreateBoardAsync(dialog.BoardName, dialog.SelectedColor, imagePath);
            await LoadAsync();
        }
    }

    private async void EditBoard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Board board) return;
        var dialog = new BoardEditDialog("Edit board", board.Name, board.BackgroundColor, board.BackgroundImagePath)
            { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (dialog.BoardName != board.Name)
                await _service.RenameBoardAsync(board.Id, dialog.BoardName);
            if (dialog.SelectedColor != board.BackgroundColor)
                await _service.SetBoardBackgroundAsync(board.Id, dialog.SelectedColor);

            var imagePath = dialog.PendingImageSourcePath is string src
                ? await _backgroundImages.ImportCustomImageAsync(src)
                : dialog.SelectedImagePath;
            if (imagePath != board.BackgroundImagePath)
                await _service.SetBoardBackgroundImageAsync(board.Id, imagePath);

            await LoadAsync();
        }
    }

    private async void ArchiveBoard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Board board) return;
        await _service.SetBoardArchivedAsync(board.Id, true);
        await LoadAsync();
    }

    private async void RestoreBoard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Board board) return;
        await _service.SetBoardArchivedAsync(board.Id, false);
        await LoadAsync();
    }

    private async void DeleteBoard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not Board board) return;
        var confirm = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = $"Delete \"{board.Name}\"?",
            Content = "The board and all of its lists, cards and attachments will be permanently deleted.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        if (await confirm.ShowAsync() == ContentDialogResult.Primary)
        {
            await _service.DeleteBoardAsync(board.Id);
            await LoadAsync();
        }
    }
}
