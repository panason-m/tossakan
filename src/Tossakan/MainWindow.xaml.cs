using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Tossakan.Helpers;
using Tossakan.Services;
using Tossakan.ViewModels;
using Tossakan.Views;

namespace Tossakan;

public sealed partial class MainWindow : Window
{
    private static readonly SolidColorBrush NoDueDateActiveBrush = Ui.ToBrush("#EB5A46");

    private readonly BoardService _boardService = App.Services.GetRequiredService<BoardService>();
    private bool _quickViewExpanded = true;
    private bool _showOnlyNoDueDate;
    private List<QuickViewCardVm> _quickViewCards = new();
    private Brush? _noDueDateDefaultBrush;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Tossakan";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1360, 840));

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath)) AppWindow.SetIcon(iconPath);

        _noDueDateDefaultBrush = NoDueDateBellIcon.Foreground;

        NavView.SelectedItem = HomeItem;
        RootFrame.Navigate(typeof(HomePage));
        ApplyQuickViewState();
    }

    public Frame NavigationFrame => RootFrame;

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (RootFrame.CanGoBack) RootFrame.GoBack();
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not NavigationViewItem item) return;
        switch (item.Tag as string)
        {
            case "home": RootFrame.Navigate(typeof(HomePage)); break;
            case "settings": RootFrame.Navigate(typeof(SettingsPage)); break;
            case "manual": RootFrame.Navigate(typeof(UserManualPage)); break;
        }
    }

    private async void RootFrame_Navigated(object sender, NavigationEventArgs e)
    {
        BackButton.IsEnabled = RootFrame.CanGoBack;
        await LoadQuickViewAsync();
    }

    // ---------- Quick View ----------

    private async void RootGrid_Loaded(object sender, RoutedEventArgs e) => await LoadQuickViewAsync();

    private async Task LoadQuickViewAsync()
    {
        var cards = await _boardService.GetQuickViewCardsAsync();
        _quickViewCards = cards.Select(c => new QuickViewCardVm(c)).ToList();
        ApplyQuickViewFilter();

        var noDueDateCount = _quickViewCards.Count(c => !c.HasDueDate);
        NoDueDateCountText.Text = noDueDateCount.ToString();
        NoDueDateFilterButton.Visibility = noDueDateCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ApplyQuickViewFilter()
    {
        QuickViewList.ItemsSource = _showOnlyNoDueDate
            ? _quickViewCards.Where(c => !c.HasDueDate).ToList()
            : _quickViewCards;
    }

    private void NoDueDateFilterButton_Click(object sender, RoutedEventArgs e)
    {
        _showOnlyNoDueDate = !_showOnlyNoDueDate;
        ApplyQuickViewFilter();

        var brush = _showOnlyNoDueDate ? NoDueDateActiveBrush : _noDueDateDefaultBrush;
        NoDueDateBellIcon.Foreground = brush;
        NoDueDateCountText.Foreground = brush;
    }

    private void ApplyQuickViewState()
    {
        if (_quickViewExpanded)
        {
            QuickViewColumn.Width = new GridLength(260);
            QuickViewPanel.Visibility = Visibility.Visible;
            QuickViewCollapsedStrip.Visibility = Visibility.Collapsed;
        }
        else
        {
            QuickViewColumn.Width = new GridLength(48);
            QuickViewPanel.Visibility = Visibility.Collapsed;
            QuickViewCollapsedStrip.Visibility = Visibility.Visible;
        }
    }

    private void QuickViewToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _quickViewExpanded = !_quickViewExpanded;
        ApplyQuickViewState();
    }

    private async void QuickViewRefreshButton_Click(object sender, RoutedEventArgs e) => await LoadQuickViewAsync();

    private async void QuickViewList_ItemClick(object sender, ItemClickEventArgs e)
    {
        var vm = (QuickViewCardVm)e.ClickedItem;
        var dialog = new Views.CardDetailDialog(vm.CardId) { XamlRoot = Content.XamlRoot };
        await dialog.ShowAsync();
        await LoadQuickViewAsync();
    }

    private async void MoveListButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not QuickViewCardVm vm) return;
        var button = (Button)sender;

        var lists = await _boardService.GetActiveListsForBoardAsync(vm.BoardId);
        var flyout = new MenuFlyout();
        foreach (var list in lists.Where(l => l.ListId != vm.ListId))
        {
            var item = new MenuFlyoutItem { Text = list.ListName, Tag = (vm.CardId, list.ListId) };
            item.Click += MoveListFlyoutItem_Click;
            flyout.Items.Add(item);
        }
        flyout.ShowAt(button);
    }

    private async void MoveListFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        var (cardId, targetListId) = ((int CardId, int ListId))((MenuFlyoutItem)sender).Tag;
        await _boardService.MoveCardAsync(cardId, targetListId, int.MaxValue);
        await LoadQuickViewAsync();
    }

    private async void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
        var service = App.Services.GetRequiredService<BoardService>();
        var results = await service.SearchCardsAsync(sender.Text);
        sender.ItemsSource = results
            .Select(r => new SearchSuggestion(r, $"{r.CardTitle}  —  {r.ListName} · {r.BoardName}"))
            .ToList();
    }

    private void SearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is not SearchSuggestion suggestion) return;
        sender.Text = "";
        RootFrame.Navigate(typeof(BoardPage),
            new BoardPage.NavigationArgs(suggestion.Result.BoardId, suggestion.Result.CardId));
    }

    private sealed record SearchSuggestion(SearchResult Result, string Display)
    {
        public override string ToString() => Display;
    }
}
