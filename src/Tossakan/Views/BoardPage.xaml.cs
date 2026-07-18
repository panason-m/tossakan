using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Tossakan.Helpers;
using Tossakan.Models;
using Tossakan.Services;
using Tossakan.ViewModels;

namespace Tossakan.Views;

public sealed partial class BoardPage : Page
{
    public record NavigationArgs(int BoardId, int? CardId);

    private readonly BoardService _service = App.Services.GetRequiredService<BoardService>();
    private readonly BackgroundImageService _backgroundImages = App.Services.GetRequiredService<BackgroundImageService>();
    private readonly DatabaseChangeWatcherService _dbWatcher = App.Services.GetRequiredService<DatabaseChangeWatcherService>();

    private int _boardId;
    private Board? _board;
    private Dictionary<int, Label> _boardLabels = new();
    private string _boardPrefix = "";
    private int? _pendingCardId;

    // Drag state shared between the per-list card ListViews
    private CardViewModel? _draggedCard;
    private ListViewModel? _dragSourceList;
    private bool _crossListDropHandled;

    public ObservableCollection<ListViewModel> Lists { get; } = new();
    public ObservableCollection<LabelFilterVm> FilterLabels { get; } = new();

    private readonly ObservableCollection<ArchivedItemVm> _archivedCards = new();
    private readonly ObservableCollection<ArchivedItemVm> _archivedLists = new();
    private bool _applyingFilter;

    public static readonly DependencyProperty CardsMaxHeightProperty = DependencyProperty.Register(
        nameof(CardsMaxHeight), typeof(double), typeof(BoardPage), new PropertyMetadata(480d));

    public double CardsMaxHeight
    {
        get => (double)GetValue(CardsMaxHeightProperty);
        set => SetValue(CardsMaxHeightProperty, value);
    }

    public BoardPage()
    {
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is NavigationArgs args)
        {
            _boardId = args.BoardId;
            _pendingCardId = args.CardId;
        }
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

    private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        => CardsMaxHeight = Math.Max(240, e.NewSize.Height - 190);

    private async Task LoadAsync()
    {
        _board = await _service.GetBoardFullAsync(_boardId);
        if (_board is null)
        {
            if (Frame.CanGoBack) Frame.GoBack();
            return;
        }

        RootGrid.Background = Ui.ToBackgroundBrush(_board.BackgroundImagePath, _board.BackgroundColor);
        BoardNameText.Text = _board.Name;
        _boardLabels = _board.Labels.ToDictionary(l => l.Id);
        _boardPrefix = ReferenceCode.ComputePrefix(_board.Name);

        RebuildFilterLabels();
        var filter = BuildFilter();
        FilterButtonText.Text = filter is null ? "Filter" : "Filter •";

        Lists.Clear();
        foreach (var list in _board.Lists.Where(l => !l.IsArchived).OrderBy(l => l.Position))
            Lists.Add(new ListViewModel(list, _boardLabels, _boardPrefix, filter));

        if (_pendingCardId is int cardId)
        {
            _pendingCardId = null;
            await OpenCardAsync(cardId);
        }
    }

    private ListViewModel? FindListContaining(CardViewModel card)
        => Lists.FirstOrDefault(l => l.Cards.Contains(card));

    /// <summary>Reloads a single card from the database and replaces its snapshot in place.</summary>
    private async Task RefreshCardAsync(int cardId)
    {
        var card = await _service.GetCardFullAsync(cardId);
        foreach (var list in Lists)
        {
            var index = list.Cards.ToList().FindIndex(c => c.Id == cardId);
            if (index < 0) continue;
            if (card is null || card.IsArchived || card.ListId != list.Id)
                list.Cards.RemoveAt(index);
            else
                list.Cards[index] = new CardViewModel(card, _boardLabels, _boardPrefix);
            return;
        }
    }

    private async Task OpenCardAsync(int cardId)
    {
        var dialog = new CardDetailDialog(cardId) { XamlRoot = XamlRoot };
        await dialog.ShowAsync();

        if (dialog.DeleteRequested)
        {
            var card = Lists.SelectMany(l => l.Cards).FirstOrDefault(c => c.Id == cardId);
            var confirmed = await Dialogs.ConfirmAsync(XamlRoot, $"Delete \"{card?.Title ?? "card"}\"?",
                "The card will be permanently deleted.");
            if (confirmed)
                await _service.DeleteCardAsync(cardId);
        }

        if (dialog.LabelsChanged)
            await LoadAsync(); // label edits can affect chips on many cards
        else
            await RefreshCardAsync(cardId);
    }

    // ---------- Board bar ----------

    private void CopyBoardReference_Click(object sender, RoutedEventArgs e)
    {
        if (_board is null) return;
        var package = new DataPackage();
        package.SetText($"Tossakan board \"{_board.Name}\"");
        Clipboard.SetContent(package);
    }

    private async void EditBoard_Click(object sender, RoutedEventArgs e)
    {
        if (_board is null) return;
        var dialog = new BoardEditDialog("Edit board", _board.Name, _board.BackgroundColor, _board.BackgroundImagePath)
            { XamlRoot = XamlRoot };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;
        if (dialog.BoardName != _board.Name)
            await _service.RenameBoardAsync(_boardId, dialog.BoardName);
        if (dialog.SelectedColor != _board.BackgroundColor)
            await _service.SetBoardBackgroundAsync(_boardId, dialog.SelectedColor);

        var imagePath = dialog.PendingImageSourcePath is string src
            ? await _backgroundImages.ImportCustomImageAsync(src)
            : dialog.SelectedImagePath;
        if (imagePath != _board.BackgroundImagePath)
            await _service.SetBoardBackgroundImageAsync(_boardId, imagePath);

        _board.Name = dialog.BoardName;
        _board.BackgroundColor = dialog.SelectedColor;
        _board.BackgroundImagePath = imagePath;
        BoardNameText.Text = _board.Name;
        RootGrid.Background = Ui.ToBackgroundBrush(_board.BackgroundImagePath, _board.BackgroundColor);
    }

    // ---------- Filter ----------

    private void RebuildFilterLabels()
    {
        var checkedIds = FilterLabels.Where(f => f.IsChecked).Select(f => f.Id).ToHashSet();
        if (FilterLabels.Select(f => f.Id).SequenceEqual(_boardLabels.Keys.OrderBy(id => id))) return;
        _applyingFilter = true;
        FilterLabels.Clear();
        foreach (var label in _boardLabels.Values.OrderBy(l => l.Id))
            FilterLabels.Add(new LabelFilterVm(label) { IsChecked = checkedIds.Contains(label.Id) });
        _applyingFilter = false;
    }

    private Func<Card, bool>? BuildFilter()
    {
        var text = FilterTextBox.Text.Trim();
        var labelIds = FilterLabels.Where(f => f.IsChecked).Select(f => f.Id).ToHashSet();
        var dueMode = DueFilterRadios.SelectedIndex;
        if (text.Length == 0 && labelIds.Count == 0 && dueMode <= 0) return null;

        var today = DateTime.Today;
        return card =>
        {
            if (text.Length > 0
                && !card.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                && !card.Description.Contains(text, StringComparison.OrdinalIgnoreCase))
                return false;
            if (labelIds.Count > 0 && !card.CardLabels.Any(cl => labelIds.Contains(cl.LabelId)))
                return false;
            return dueMode switch
            {
                1 => card.DueDate is not null && card.DueDate.Value.Date < today && !card.IsDueComplete,
                2 => card.DueDate is not null && card.DueDate.Value.Date >= today
                     && card.DueDate.Value.Date <= today.AddDays(7) && !card.IsDueComplete,
                3 => card.DueDate is null,
                _ => true,
            };
        };
    }

    /// <summary>Re-applies the filter by rebuilding card collections in place, so the
    /// filter flyout (anchored in the board bar) keeps focus and stays open.</summary>
    private async Task ApplyFilterAsync()
    {
        if (_applyingFilter) return;
        _applyingFilter = true;
        try
        {
            _board = await _service.GetBoardFullAsync(_boardId);
            if (_board is null) return;
            _boardLabels = _board.Labels.ToDictionary(l => l.Id);
            var filter = BuildFilter();
            FilterButtonText.Text = filter is null ? "Filter" : "Filter •";
            foreach (var listVm in Lists)
            {
                var list = _board.Lists.FirstOrDefault(l => l.Id == listVm.Id);
                if (list is null) continue;
                listVm.Cards.Clear();
                foreach (var card in list.Cards.Where(c => !c.IsArchived).OrderBy(c => c.Position))
                {
                    if (filter is null || filter(card))
                        listVm.Cards.Add(new CardViewModel(card, _boardLabels, _boardPrefix));
                }
            }
        }
        finally { _applyingFilter = false; }
    }

    private async void Filter_Changed(object sender, TextChangedEventArgs e) => await ApplyFilterAsync();

    private async void Filter_Changed(object sender, RoutedEventArgs e) => await ApplyFilterAsync();

    private async void Filter_Changed(object sender, SelectionChangedEventArgs e) => await ApplyFilterAsync();

    private async void ClearFilter_Click(object sender, RoutedEventArgs e)
    {
        _applyingFilter = true;
        FilterTextBox.Text = "";
        foreach (var label in FilterLabels) label.IsChecked = false;
        DueFilterRadios.SelectedIndex = 0;
        _applyingFilter = false;
        await ApplyFilterAsync();
    }

    // ---------- Activity & archive ----------

    private async void ActivityFlyout_Opening(object sender, object e)
    {
        var entries = await _service.GetActivityAsync(_boardId);
        ActivityList.ItemsSource = entries.Select(a => new ActivityVm(a)).ToList();
    }

    private async void ArchiveFlyout_Opening(object sender, object e)
    {
        var (cards, lists) = await _service.GetArchivedAsync(_boardId);
        _archivedCards.Clear();
        foreach (var card in cards)
            _archivedCards.Add(new ArchivedItemVm(card.Id, card.Title, $"in {card.List.Name}"));
        _archivedLists.Clear();
        foreach (var list in lists)
            _archivedLists.Add(new ArchivedItemVm(list.Id, list.Name));
        ArchivedCardsList.ItemsSource = _archivedCards;
        ArchivedListsList.ItemsSource = _archivedLists;
        UpdateArchiveEmptyTexts();
    }

    private void UpdateArchiveEmptyTexts()
    {
        NoArchivedCardsText.Visibility = _archivedCards.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        NoArchivedListsText.Visibility = _archivedLists.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void RestoreArchivedCard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ArchivedItemVm item) return;
        await _service.SetCardArchivedAsync(item.Id, false);
        _archivedCards.Remove(item);
        UpdateArchiveEmptyTexts();
        await LoadAsync();
    }

    private async void DeleteArchivedCard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ArchivedItemVm item) return;
        await _service.DeleteCardAsync(item.Id);
        _archivedCards.Remove(item);
        UpdateArchiveEmptyTexts();
    }

    private async void RestoreArchivedList_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ArchivedItemVm item) return;
        await _service.SetListArchivedAsync(item.Id, false);
        _archivedLists.Remove(item);
        UpdateArchiveEmptyTexts();
        await LoadAsync();
    }

    private async void DeleteArchivedList_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ArchivedItemVm item) return;
        await _service.DeleteListAsync(item.Id);
        _archivedLists.Remove(item);
        UpdateArchiveEmptyTexts();
    }

    // ---------- Lists ----------

    private void ShowAddList_Click(object sender, RoutedEventArgs e)
    {
        AddListButton.Visibility = Visibility.Collapsed;
        AddListPanel.Visibility = Visibility.Visible;
        AddListBox.Focus(FocusState.Programmatic);
    }

    private void CancelAddList_Click(object sender, RoutedEventArgs e)
    {
        AddListBox.Text = "";
        AddListPanel.Visibility = Visibility.Collapsed;
        AddListButton.Visibility = Visibility.Visible;
    }

    private async void ConfirmAddList_Click(object sender, RoutedEventArgs e) => await AddListAsync();

    private async void AddListBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter) await AddListAsync();
    }

    private async Task AddListAsync()
    {
        var name = AddListBox.Text.Trim();
        if (name.Length == 0) return;
        var list = await _service.AddListAsync(_boardId, name);
        Lists.Add(new ListViewModel(list, _boardLabels, _boardPrefix));
        AddListBox.Text = "";
    }

    private async void ListName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not ListViewModel list) return;
        var newName = box.Text.Trim();
        if (newName.Length == 0)
        {
            box.Text = list.Name;
            return;
        }
        if (newName == list.Name) return;
        await _service.RenameListAsync(list.Id, newName);
        list.Name = newName;
    }

    private void ListName_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || sender is not TextBox box) return;
        e.Handled = true;
        Focus(FocusState.Programmatic);
    }

    private async void ArchiveList_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ListViewModel list) return;
        await _service.SetListArchivedAsync(list.Id, true);
        Lists.Remove(list);
    }

    private async void DeleteList_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ListViewModel list) return;
        var confirmed = await Dialogs.ConfirmAsync(XamlRoot, $"Delete \"{list.Name}\"?",
            "The list and all of its cards will be permanently deleted.");
        if (!confirmed) return;
        await _service.DeleteListAsync(list.Id);
        Lists.Remove(list);
    }

    private async void ListsView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (args.DropResult != Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move) return;
        if (args.Items.FirstOrDefault() is not ListViewModel moved) return;
        var newIndex = Lists.IndexOf(moved);
        if (newIndex >= 0)
            await _service.MoveListAsync(moved.Id, newIndex);
    }

    // ---------- Cards: add / open / context menu ----------

    private void ShowAddCard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ListViewModel list)
            list.IsAddingCard = true;
    }

    private void CancelAddCard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ListViewModel list) return;
        list.NewCardText = "";
        list.IsAddingCard = false;
    }

    private async void ConfirmAddCard_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ListViewModel list)
            await AddCardAsync(list);
    }

    private async void AddCardBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && (sender as FrameworkElement)?.Tag is ListViewModel list)
            await AddCardAsync(list);
    }

    private async Task AddCardAsync(ListViewModel list)
    {
        var title = list.NewCardText.Trim();
        if (title.Length == 0) return;
        var card = await _service.AddCardAsync(list.Id, title);
        list.Cards.Add(new CardViewModel(card, _boardLabels, _boardPrefix));
        list.NewCardText = "";
    }

    private async void Cards_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CardViewModel card)
            await OpenCardAsync(card.Id);
    }

    private async void OpenCardMenu_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is CardViewModel card)
            await OpenCardAsync(card.Id);
    }

    private async void ArchiveCardMenu_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not CardViewModel card) return;
        await _service.SetCardArchivedAsync(card.Id, true);
        FindListContaining(card)?.Cards.Remove(card);
    }

    private async void DeleteCardMenu_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not CardViewModel card) return;
        var confirmed = await Dialogs.ConfirmAsync(XamlRoot, $"Delete \"{card.Title}\"?",
            "The card will be permanently deleted.");
        if (!confirmed) return;
        await _service.DeleteCardAsync(card.Id);
        FindListContaining(card)?.Cards.Remove(card);
    }

    // ---------- Cards: drag and drop ----------

    private void Cards_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        _draggedCard = e.Items.FirstOrDefault() as CardViewModel;
        _dragSourceList = (sender as FrameworkElement)?.Tag as ListViewModel;
        _crossListDropHandled = false;
        e.Data.RequestedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;

        // A card drag bubbles into the outer ListsView and would otherwise also be
        // recognized as a list-reorder gesture, leaving a gap where the list column
        // briefly "detaches". Suspend the outer list's own drag recognition for the
        // duration of the card drag.
        ListsView.CanDragItems = false;
        ListsView.CanReorderItems = false;
    }

    private void Cards_DragOver(object sender, DragEventArgs e)
    {
        if (_draggedCard is not null)
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move;
    }

    private async void Cards_Drop(object sender, DragEventArgs e)
    {
        if (_draggedCard is null || _dragSourceList is null) return;
        if (sender is not ListView targetView || targetView.Tag is not ListViewModel targetList) return;
        if (targetList == _dragSourceList) return; // within-list reorder is handled natively

        var card = _draggedCard;
        var index = ComputeDropIndex(targetView, e);
        _crossListDropHandled = true;
        _dragSourceList.Cards.Remove(card);
        targetList.Cards.Insert(index, card);
        await _service.MoveCardAsync(card.Id, targetList.Id, index);
    }

    private static int ComputeDropIndex(ListView listView, DragEventArgs e)
    {
        var index = listView.Items.Count;
        for (var i = 0; i < listView.Items.Count; i++)
        {
            if (listView.ContainerFromIndex(i) is not ListViewItem container) continue;
            var topLeft = container.TransformToVisual(listView).TransformPoint(new Windows.Foundation.Point(0, 0));
            var center = topLeft.Y + container.ActualHeight / 2;
            if (e.GetPosition(listView).Y < center)
            {
                index = i;
                break;
            }
        }
        return index;
    }

    private async void Cards_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        ListsView.CanDragItems = true;
        ListsView.CanReorderItems = true;

        var card = _draggedCard;
        var sourceList = _dragSourceList;
        var handledElsewhere = _crossListDropHandled;
        _draggedCard = null;
        _dragSourceList = null;
        _crossListDropHandled = false;

        if (handledElsewhere || card is null || sourceList is null) return;
        if (args.DropResult != Windows.ApplicationModel.DataTransfer.DataPackageOperation.Move) return;

        var newIndex = sourceList.Cards.IndexOf(card);
        if (newIndex >= 0)
            await _service.MoveCardAsync(card.Id, sourceList.Id, newIndex);
    }
}
