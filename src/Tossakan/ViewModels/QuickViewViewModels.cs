using Microsoft.UI.Xaml.Media;
using Tossakan.Helpers;
using Tossakan.Services;

namespace Tossakan.ViewModels;

/// <summary>Immutable snapshot of a card for the cross-board Quick View panel.</summary>
public sealed class QuickViewCardVm
{
    public int CardId { get; }
    public string Title { get; }
    public int BoardId { get; }
    public int ListId { get; }
    public string LocationText { get; }
    public bool HasDueDate { get; }
    public string DueText { get; }
    public SolidColorBrush DueBrush { get; }

    public QuickViewCardVm(QuickViewCardInfo info)
    {
        CardId = info.CardId;
        Title = info.CardTitle;
        BoardId = info.BoardId;
        ListId = info.ListId;
        LocationText = $"{info.BoardName} · {info.ListName}";
        HasDueDate = info.DueDate.HasValue;

        if (info.DueDate is { } due)
        {
            DueText = Ui.FormatDueDate(due);
            DueBrush = due.Date < DateTime.Today ? Ui.ToBrush("#EB5A46")
                : due.Date == DateTime.Today ? Ui.ToBrush("#FF9F1A")
                : Ui.ToBrush("#64748B");
        }
        else
        {
            DueText = "No due date";
            DueBrush = Ui.ToBrush("#EB5A46");
        }
    }
}
