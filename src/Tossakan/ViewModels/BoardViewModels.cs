using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using System.Collections.ObjectModel;
using Tossakan.Helpers;
using Tossakan.Models;

namespace Tossakan.ViewModels;

/// <summary>Immutable snapshot of a card for the board view; rebuilt whenever the card changes.</summary>
public class CardViewModel
{
    public int Id { get; }
    public string Title { get; }
    public string ReferenceCode { get; }
    public string? CoverColor { get; }
    public bool HasCover { get; }
    public SolidColorBrush CoverBrush { get; }
    public List<Label> Labels { get; }
    public bool HasLabels => Labels.Count > 0;
    public bool HasDescription { get; }
    public DateTime? DueDate { get; }
    public bool IsDueComplete { get; }
    public bool HasDue => DueDate is not null;
    public string DueText { get; }
    public SolidColorBrush DueBrush { get; }
    public string ChecklistText { get; }
    public bool HasChecklist { get; }
    public SolidColorBrush ChecklistBrush { get; }
    public int CommentCount { get; }
    public bool HasComments => CommentCount > 0;
    public string CommentText => CommentCount.ToString();
    public int AttachmentCount { get; }
    public bool HasAttachments => AttachmentCount > 0;
    public string AttachmentText => AttachmentCount.ToString();

    public CardViewModel(Card card, IReadOnlyDictionary<int, Label> boardLabels, string boardPrefix)
    {
        Id = card.Id;
        Title = card.Title;
        ReferenceCode = $"{boardPrefix}-{card.ReferenceNumber}";
        CoverColor = card.CoverColor;
        HasCover = !string.IsNullOrEmpty(card.CoverColor);
        CoverBrush = Ui.ToBrush(card.CoverColor);
        Labels = card.CardLabels
            .Where(cl => boardLabels.ContainsKey(cl.LabelId))
            .Select(cl => boardLabels[cl.LabelId])
            .OrderBy(l => l.Id)
            .ToList();
        HasDescription = !string.IsNullOrWhiteSpace(card.Description);
        DueDate = card.DueDate;
        IsDueComplete = card.IsDueComplete;
        DueText = Ui.FormatDueDate(card.DueDate);
        DueBrush =
            card.IsDueComplete ? Ui.ToBrush("#61BD4F")
            : card.DueDate?.Date < DateTime.Today ? Ui.ToBrush("#EB5A46")
            : card.DueDate?.Date == DateTime.Today ? Ui.ToBrush("#FF9F1A")
            : Ui.ToBrush("#64748B");

        var totalItems = card.Checklists.Sum(c => c.Items.Count);
        var doneItems = card.Checklists.Sum(c => c.Items.Count(i => i.IsChecked));
        HasChecklist = totalItems > 0;
        ChecklistText = $"{doneItems}/{totalItems}";
        ChecklistBrush = totalItems > 0 && doneItems == totalItems
            ? Ui.ToBrush("#61BD4F")
            : Ui.ToBrush("#64748B");

        CommentCount = card.Comments.Count;
        AttachmentCount = card.Attachments.Count;
    }
}

public partial class LabelFilterVm : ObservableObject
{
    public int Id { get; }
    public string DisplayName { get; }
    public SolidColorBrush Brush { get; }

    [ObservableProperty]
    private bool isChecked;

    public LabelFilterVm(Label label)
    {
        Id = label.Id;
        DisplayName = string.IsNullOrEmpty(label.Name) ? "(no name)" : label.Name;
        Brush = Ui.ToBrush(label.Color);
    }
}

public class ActivityVm
{
    public string Description { get; }
    public string TimeText { get; }

    public ActivityVm(ActivityEntry entry)
    {
        Description = entry.Description;
        TimeText = Ui.FormatTimestamp(entry.CreatedAt);
    }
}

public class ArchivedItemVm
{
    public int Id { get; }
    public string Title { get; }
    public string Subtitle { get; }

    public ArchivedItemVm(int id, string title, string subtitle = "")
    {
        Id = id;
        Title = title;
        Subtitle = subtitle;
    }
}

/// <summary>A list (column) on the board, with live card collection for drag and drop.</summary>
public partial class ListViewModel : ObservableObject
{
    public int Id { get; }

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private bool isAddingCard;

    [ObservableProperty]
    private string newCardText = "";

    public ObservableCollection<CardViewModel> Cards { get; } = new();

    public ListViewModel(BoardList list, IReadOnlyDictionary<int, Label> boardLabels, string boardPrefix, Func<Card, bool>? filter = null)
    {
        Id = list.Id;
        name = list.Name;
        foreach (var card in list.Cards.Where(c => !c.IsArchived).OrderBy(c => c.Position))
        {
            if (filter is null || filter(card))
                Cards.Add(new CardViewModel(card, boardLabels, boardPrefix));
        }
    }
}
