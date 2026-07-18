using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Tossakan.Helpers;
using Tossakan.Models;
using Tossakan.Services;
using Tossakan.ViewModels;

namespace Tossakan.Views;

public sealed partial class CardDetailDialog : ContentDialog
{
    private readonly BoardService _service = App.Services.GetRequiredService<BoardService>();
    private readonly AttachmentService _attachments = App.Services.GetRequiredService<AttachmentService>();

    private readonly int _cardId;
    private Card? _card;
    private int _boardId;
    private bool _initializing = true;

    public ObservableCollection<LabelChipVm> LabelChips { get; } = new();
    public ObservableCollection<ChecklistVm> Checklists { get; } = new();
    public ObservableCollection<CommentVm> Comments { get; } = new();
    public ObservableCollection<AttachmentVm> Attachments { get; } = new();

    /// <summary>Set when the user asked to delete the card; the caller confirms and deletes.</summary>
    public bool DeleteRequested { get; private set; }

    /// <summary>Set when board labels were created or edited, so the caller reloads the board.</summary>
    public bool LabelsChanged { get; private set; }

    public CardDetailDialog(int cardId)
    {
        InitializeComponent();
        // handledEventsToo: true so this still fires when the double-tap lands on the rendered
        // description text — RichTextBlock's own text-selection handling (double-tap = select word)
        // marks the event handled before it would otherwise bubble up to this Border.
        DescriptionViewBorder.AddHandler(DoubleTappedEvent, new DoubleTappedEventHandler(DescriptionView_DoubleTapped), true);
        _cardId = cardId;
        Opened += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _initializing = true;
        _card = await _service.GetCardFullAsync(_cardId);
        if (_card is null)
        {
            Hide();
            return;
        }
        _boardId = _card.List.BoardId;

        ReferenceCodeText.Text = Helpers.ReferenceCode.Format(_card.List.Board.Name, _card.ReferenceNumber);
        TitleBox.Text = _card.Title;
        ListLocationText.Text = $"in list {_card.List.Name}";
        DescriptionBox.Text = _card.Description;
        DescriptionViewBorder.Child = MarkdownRenderer.Render(_card.Description);

        DuePicker.Date = _card.DueDate is null ? null : new DateTimeOffset(_card.DueDate.Value);
        DueCompleteBox.IsChecked = _card.IsDueComplete;

        CoverPalette.ItemsSource = Ui.LabelPalette;
        CoverPalette.SelectedItem = Ui.LabelPalette.Contains(_card.CoverColor ?? "") ? _card.CoverColor : null;

        var assigned = _card.CardLabels.Select(cl => cl.LabelId).ToHashSet();
        LabelChips.Clear();
        foreach (var label in await _service.GetLabelsAsync(_boardId))
            LabelChips.Add(new LabelChipVm(label, assigned.Contains(label.Id)));

        Checklists.Clear();
        foreach (var checklist in _card.Checklists.OrderBy(c => c.Position))
            Checklists.Add(new ChecklistVm(checklist));

        Comments.Clear();
        foreach (var comment in _card.Comments.OrderByDescending(c => c.CreatedAt))
            Comments.Add(new CommentVm(comment));

        Attachments.Clear();
        foreach (var attachment in _card.Attachments.OrderByDescending(a => a.AddedAt))
            Attachments.Add(new AttachmentVm(attachment));

        _initializing = false;
    }

    private async void Dialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
    {
        if (_card is null) return;
        var deferral = args.GetDeferral();
        var title = TitleBox.Text.Trim();
        if (title.Length > 0 && title != _card.Title)
            await _service.UpdateCardTitleAsync(_cardId, title);
        if (DescriptionBox.Text != _card.Description)
            await _service.UpdateCardDescriptionAsync(_cardId, DescriptionBox.Text);
        foreach (var checklist in Checklists)
            foreach (var item in checklist.Items)
                await _service.UpdateChecklistItemAsync(item.Id, item.Text, item.IsChecked);
        deferral.Complete();
    }

    private void CopyReference_Click(object sender, RoutedEventArgs e)
    {
        if (_card is null) return;
        var code = Helpers.ReferenceCode.Format(_card.List.Board.Name, _card.ReferenceNumber);
        var text = $"Tossakan card {code} \"{_card.Title}\" (board: {_card.List.Board.Name})";
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    // ---------- Description ----------

    private void DescriptionView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        // Deferred: the RichTextBlock inside DescriptionViewBorder is still mid-way through its own
        // double-tap word-selection handling at this point. Collapsing it (and its container)
        // synchronously here pulls it out of the visual tree while that's still in flight, which
        // crashed/froze the app. Queuing the switch for the next dispatcher pass lets that finish first.
        DispatcherQueue.TryEnqueue(() =>
        {
            DescriptionViewBorder.Visibility = Visibility.Collapsed;
            DescriptionEditPanel.Visibility = Visibility.Visible;
            DescriptionBox.Focus(FocusState.Programmatic);
        });
    }

    private async void SaveDescription_Click(object sender, RoutedEventArgs e) => await CommitDescriptionAsync();

    private async void DescriptionBox_LostFocus(object sender, RoutedEventArgs e) => await CommitDescriptionAsync();

    private async Task CommitDescriptionAsync()
    {
        if (_card is null) return;
        var text = DescriptionBox.Text;
        await _service.UpdateCardDescriptionAsync(_cardId, text);
        _card.Description = text;
        DescriptionViewBorder.Child = MarkdownRenderer.Render(text);
        DescriptionEditPanel.Visibility = Visibility.Collapsed;
        DescriptionViewBorder.Visibility = Visibility.Visible;
    }

    private async void DescriptionBox_Paste(object sender, TextControlPasteEventArgs e)
    {
        if (!AttachmentService.ClipboardHasImage()) return;
        e.Handled = true;
        var attachment = await _attachments.AddClipboardImageAsync(_cardId);
        if (attachment is null) return;
        InsertImageMarkdown(DescriptionBox, attachment);
        Attachments.Insert(0, new AttachmentVm(attachment));
    }

    /// <summary>Inserts a markdown image line at the caret so a clipboard-pasted image shows up
    /// inline once the surrounding text is rendered by MarkdownRenderer.</summary>
    private static void InsertImageMarkdown(TextBox box, Attachment attachment)
    {
        var markdown = $"![{attachment.FileName}]({attachment.StoredPath})";
        var text = box.Text ?? "";
        var start = box.SelectionStart;
        var needsLeadingNewline = start > 0 && text[start - 1] != '\n';
        var insertion = (needsLeadingNewline ? "\n" : "") + markdown + "\n";
        box.Text = text.Insert(start, insertion);
        box.SelectionStart = start + insertion.Length;
        box.SelectionLength = 0;
    }

    // ---------- Due date ----------

    private async void DuePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
    {
        if (_initializing) return;
        await _service.UpdateCardDueDateAsync(_cardId, sender.Date?.DateTime.Date, DueCompleteBox.IsChecked == true);
    }

    private async void DueComplete_Changed(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        await _service.UpdateCardDueDateAsync(_cardId, DuePicker.Date?.DateTime.Date, DueCompleteBox.IsChecked == true);
    }

    private async void ClearDue_Click(object sender, RoutedEventArgs e)
    {
        _initializing = true;
        DuePicker.Date = null;
        DueCompleteBox.IsChecked = false;
        _initializing = false;
        await _service.UpdateCardDueDateAsync(_cardId, null, false);
    }

    // ---------- Cover ----------

    private async void Cover_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing) return;
        if (CoverPalette.SelectedItem is string color)
            await _service.UpdateCardCoverAsync(_cardId, color);
    }

    private async void ClearCover_Click(object sender, RoutedEventArgs e)
    {
        _initializing = true;
        CoverPalette.SelectedItem = null;
        _initializing = false;
        await _service.UpdateCardCoverAsync(_cardId, null);
    }

    // ---------- Labels ----------

    private async void LabelChip_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not LabelChipVm chip) return;
        chip.IsChecked = !chip.IsChecked;
        await _service.SetCardLabelAsync(_cardId, chip.Id, chip.IsChecked);
    }

    private void LabelChip_RightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is LabelChipVm chip)
            ShowLabelEditor((FrameworkElement)sender, chip);
    }

    private void LabelChip_Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is LabelChipVm chip)
            ShowLabelEditor((FrameworkElement)sender, chip);
    }

    private void NewLabel_Click(object sender, RoutedEventArgs e)
        => ShowLabelEditor((FrameworkElement)sender, null);

    private void ShowLabelEditor(FrameworkElement anchor, LabelChipVm? chip)
    {
        var nameBox = new TextBox
        {
            PlaceholderText = "Label name (optional)",
            Text = chip?.Name ?? "",
            MinWidth = 240,
        };
        var selectedColor = chip?.Color ?? Ui.LabelPalette[0];
        var preview = new Border
        {
            Height = 26,
            CornerRadius = new CornerRadius(4),
            Background = Ui.ToBrush(selectedColor),
        };
        var swatches = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        foreach (var color in Ui.LabelPalette)
        {
            var swatch = new Button
            {
                Width = 28,
                Height = 22,
                Padding = new Thickness(0),
                Background = Ui.ToBrush(color),
                BorderThickness = new Thickness(color == selectedColor ? 2 : 0),
                BorderBrush = new SolidColorBrush(Colors.White),
                Tag = color,
            };
            swatch.Click += (s, _) =>
            {
                selectedColor = (string)((Button)s).Tag;
                preview.Background = Ui.ToBrush(selectedColor);
                foreach (var child in swatches.Children.OfType<Button>())
                    child.BorderThickness = new Thickness((string)child.Tag == selectedColor ? 2 : 0);
            };
            swatches.Children.Add(swatch);
        }

        var saveButton = new Button { Content = chip is null ? "Create" : "Save", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        buttons.Children.Add(saveButton);
        var flyout = new Flyout
        {
            Content = new StackPanel { Spacing = 8, Children = { nameBox, preview, swatches, buttons } },
        };

        if (chip is not null)
        {
            var deleteButton = new Button { Content = "Delete label" };
            deleteButton.Click += async (_, _) =>
            {
                flyout.Hide();
                await _service.DeleteLabelAsync(chip.Id);
                LabelChips.Remove(chip);
                LabelsChanged = true;
            };
            buttons.Children.Add(deleteButton);
        }

        saveButton.Click += async (_, _) =>
        {
            flyout.Hide();
            var name = nameBox.Text.Trim();
            if (chip is null)
            {
                var label = await _service.CreateLabelAsync(_boardId, name, selectedColor);
                LabelChips.Add(new LabelChipVm(label, false));
            }
            else
            {
                await _service.UpdateLabelAsync(chip.Id, name, selectedColor);
                chip.Update(name, selectedColor);
            }
            LabelsChanged = true;
        };

        flyout.ShowAt(anchor);
    }

    // ---------- Checklists ----------

    private void AddChecklist_Click(object sender, RoutedEventArgs e)
    {
        var anchor = (FrameworkElement)sender;
        var nameBox = new TextBox { PlaceholderText = "Add a checklist title…", MinWidth = 220 };
        var addButton = new Button { Content = "Add", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };
        var flyout = new Flyout { Content = new StackPanel { Spacing = 8, Children = { nameBox, addButton } } };
        addButton.Click += async (_, _) =>
        {
            flyout.Hide();
            var title = nameBox.Text.Trim();
            if (title.Length == 0) return;
            var checklist = await _service.AddChecklistAsync(_cardId, title);
            Checklists.Add(new ChecklistVm(checklist));
        };
        flyout.ShowAt(anchor);
    }

    private async void ChecklistTitle_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox box || box.Tag is not ChecklistVm checklist) return;
        var newTitle = box.Text.Trim();
        if (newTitle.Length == 0)
        {
            box.Text = checklist.Title;
            return;
        }
        if (newTitle == checklist.Title) return;
        await _service.RenameChecklistAsync(checklist.Id, newTitle);
        checklist.Title = newTitle;
    }

    private void ChecklistTitle_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter || sender is not TextBox) return;
        e.Handled = true;
        Focus(FocusState.Programmatic);
    }

    private async void DeleteChecklist_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ChecklistVm checklist) return;
        await _service.DeleteChecklistAsync(checklist.Id);
        Checklists.Remove(checklist);
    }

    private async void AddChecklistItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is ChecklistVm checklist)
            await AddChecklistItemAsync(checklist);
    }

    private async void AddChecklistItemBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter && (sender as FrameworkElement)?.Tag is ChecklistVm checklist)
            await AddChecklistItemAsync(checklist);
    }

    private async Task AddChecklistItemAsync(ChecklistVm checklist)
    {
        var text = checklist.NewItemText.Trim();
        if (text.Length == 0) return;
        var item = await _service.AddChecklistItemAsync(checklist.Id, text);
        checklist.Items.Add(new ChecklistItemVm(item));
        checklist.NewItemText = "";
        checklist.Recompute();
    }

    private async void ChecklistItem_Toggled(object sender, RoutedEventArgs e)
    {
        if (_initializing) return;
        if (sender is not CheckBox checkBox || checkBox.Tag is not ChecklistItemVm item) return;
        // Read the checkbox's own value rather than item.IsChecked: with x:Bind TwoWay
        // there's no guaranteed order between this handler and the binding's write-back,
        // so item.IsChecked can still hold the pre-toggle value here.
        item.IsChecked = checkBox.IsChecked == true;
        Checklists.FirstOrDefault(c => c.Items.Contains(item))?.Recompute();
        await _service.UpdateChecklistItemAsync(item.Id, item.Text, item.IsChecked);
    }

    private async void DeleteChecklistItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not ChecklistItemVm item) return;
        var checklist = Checklists.FirstOrDefault(c => c.Items.Contains(item));
        await _service.DeleteChecklistItemAsync(item.Id);
        checklist?.Items.Remove(item);
        checklist?.Recompute();
    }

    // ---------- Attachments ----------

    private async void AttachFile_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        picker.FileTypeFilter.Add("*");
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        var file = await picker.PickSingleFileAsync();
        if (file is null) return;
        var attachment = await _attachments.AddAttachmentAsync(_cardId, file.Path);
        Attachments.Insert(0, new AttachmentVm(attachment));
    }

    private async void OpenAttachment_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not AttachmentVm attachment) return;
        if (!File.Exists(attachment.StoredPath)) return;
        var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(attachment.StoredPath);
        await Launcher.LaunchFileAsync(file);
    }

    private void PreviewAttachment_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is AttachmentVm attachment)
            new AttachmentPreviewWindow(attachment).Activate();
    }

    private void AttachmentThumbnail_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is AttachmentVm attachment)
            new AttachmentPreviewWindow(attachment).Activate();
    }

    private async void RemoveAttachment_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not AttachmentVm attachment) return;
        await _attachments.RemoveAttachmentAsync(attachment.Id);
        Attachments.Remove(attachment);
    }

    // ---------- Comments ----------

    private async void AddComment_Click(object sender, RoutedEventArgs e)
    {
        var text = CommentBox.Text.Trim();
        if (text.Length == 0) return;
        var comment = await _service.AddCommentAsync(_cardId, text);
        Comments.Insert(0, new CommentVm(comment));
        CommentBox.Text = "";
    }

    private async void DeleteComment_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not CommentVm comment) return;
        await _service.DeleteCommentAsync(comment.Id);
        Comments.Remove(comment);
    }

    private async void CommentBox_Paste(object sender, TextControlPasteEventArgs e)
    {
        if (!AttachmentService.ClipboardHasImage()) return;
        e.Handled = true;
        var attachment = await _attachments.AddClipboardImageAsync(_cardId);
        if (attachment is null) return;
        InsertImageMarkdown(CommentBox, attachment);
        Attachments.Insert(0, new AttachmentVm(attachment));
    }

    private async void CommentEditBox_Paste(object sender, TextControlPasteEventArgs e)
    {
        if (sender is not TextBox box || !AttachmentService.ClipboardHasImage()) return;
        e.Handled = true;
        var attachment = await _attachments.AddClipboardImageAsync(_cardId);
        if (attachment is null) return;
        InsertImageMarkdown(box, attachment);
        Attachments.Insert(0, new AttachmentVm(attachment));
    }

    private void CommentView_Tapped(object sender, TappedRoutedEventArgs e)
    {
        var border = (FrameworkElement)sender;
        var panel = (StackPanel)border.Parent;
        var editPanel = panel.Children.OfType<StackPanel>().First();
        var editBox = editPanel.Children.OfType<TextBox>().First();
        border.Visibility = Visibility.Collapsed;
        editPanel.Visibility = Visibility.Visible;
        editBox.Focus(FocusState.Programmatic);
    }

    private async void SaveComment_Click(object sender, RoutedEventArgs e)
    {
        var button = (FrameworkElement)sender;
        var editPanel = (StackPanel)button.Parent;
        var panel = (StackPanel)editPanel.Parent;
        var border = panel.Children.OfType<Border>().First();
        editPanel.Visibility = Visibility.Collapsed;
        border.Visibility = Visibility.Visible;
        if (button.Tag is CommentVm comment)
            await _service.UpdateCommentAsync(comment.Id, comment.Text);
    }

    // ---------- Archive / delete ----------

    private async void ArchiveCard_Click(object sender, RoutedEventArgs e)
    {
        await _service.SetCardArchivedAsync(_cardId, true);
        Hide();
    }

    private void DeleteCard_Click(object sender, RoutedEventArgs e)
    {
        DeleteRequested = true;
        Hide();
    }
}
