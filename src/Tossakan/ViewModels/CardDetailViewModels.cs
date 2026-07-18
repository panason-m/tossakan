using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Collections.ObjectModel;
using Tossakan.Helpers;
using Tossakan.Models;

namespace Tossakan.ViewModels;

public partial class LabelChipVm : ObservableObject
{
    public int Id { get; }

    [ObservableProperty]
    private string name;

    [ObservableProperty]
    private string color;

    [ObservableProperty]
    private SolidColorBrush brush;

    [ObservableProperty]
    private bool isChecked;

    public LabelChipVm(Label label, bool assigned)
    {
        Id = label.Id;
        name = label.Name;
        color = label.Color;
        brush = Ui.ToBrush(label.Color);
        isChecked = assigned;
    }

    public void Update(string newName, string newColor)
    {
        Name = newName;
        Color = newColor;
        Brush = Ui.ToBrush(newColor);
    }
}

public partial class ChecklistItemVm : ObservableObject
{
    public int Id { get; }

    [ObservableProperty]
    private string text;

    [ObservableProperty]
    private bool isChecked;

    public ChecklistItemVm(ChecklistItem item)
    {
        Id = item.Id;
        text = item.Text;
        isChecked = item.IsChecked;
    }
}

public partial class ChecklistVm : ObservableObject
{
    public int Id { get; }

    [ObservableProperty]
    private string title;

    [ObservableProperty]
    private double progress;

    [ObservableProperty]
    private string progressText = "0%";

    [ObservableProperty]
    private string newItemText = "";

    public ObservableCollection<ChecklistItemVm> Items { get; } = new();

    public ChecklistVm(Checklist checklist)
    {
        Id = checklist.Id;
        title = checklist.Title;
        foreach (var item in checklist.Items.OrderBy(i => i.Position))
            Items.Add(new ChecklistItemVm(item));
        Recompute();
    }

    public void Recompute()
    {
        var done = Items.Count(i => i.IsChecked);
        Progress = Items.Count == 0 ? 0 : done * 100.0 / Items.Count;
        ProgressText = $"{(int)Math.Round(Progress)}%";
    }
}

public partial class CommentVm : ObservableObject
{
    public int Id { get; }

    [ObservableProperty]
    private string text;

    public string TimeText { get; }

    public CommentVm(Comment comment)
    {
        Id = comment.Id;
        text = comment.Text;
        TimeText = Ui.FormatTimestamp(comment.CreatedAt);
    }
}

public class AttachmentVm
{
    public int Id { get; }
    public string FileName { get; }
    public string StoredPath { get; }
    public string TimeText { get; }
    public bool IsImage { get; }
    public bool IsNotImage => !IsImage;
    public bool IsMarkdown { get; }
    public bool IsPreviewable => IsImage || IsMarkdown;
    public ImageSource? Thumbnail { get; }

    public AttachmentVm(Attachment attachment)
    {
        Id = attachment.Id;
        FileName = attachment.FileName;
        StoredPath = attachment.StoredPath;
        TimeText = Ui.FormatTimestamp(attachment.AddedAt);
        IsImage = FileKind.IsImage(FileName);
        IsMarkdown = FileKind.IsMarkdown(FileName);
        Thumbnail = IsImage && File.Exists(StoredPath) ? new BitmapImage(new Uri(StoredPath)) : null;
    }
}
