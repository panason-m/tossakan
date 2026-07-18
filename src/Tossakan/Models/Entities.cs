namespace Tossakan.Models;

public class Board
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string BackgroundColor { get; set; } = "#0079BF";
    public string? BackgroundImagePath { get; set; }
    public int Position { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Next value to hand out for Card.ReferenceNumber on this board; never reused once assigned.</summary>
    public int NextCardNumber { get; set; } = 1;

    public List<BoardList> Lists { get; } = new();
    public List<Label> Labels { get; } = new();
    public List<ActivityEntry> Activities { get; } = new();
}

public class BoardList
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public Board Board { get; set; } = null!;
    public string Name { get; set; } = "";
    public int Position { get; set; }
    public bool IsArchived { get; set; }

    public List<Card> Cards { get; } = new();
}

public class Card
{
    public int Id { get; set; }
    public int ListId { get; set; }
    public BoardList List { get; set; } = null!;
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public int Position { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsDueComplete { get; set; }
    public string? CoverColor { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Sequential per-board number assigned at creation (e.g. 36 for "TP-36"); stable for the card's lifetime.</summary>
    public int ReferenceNumber { get; set; }

    public List<CardLabel> CardLabels { get; } = new();
    public List<Checklist> Checklists { get; } = new();
    public List<Comment> Comments { get; } = new();
    public List<Attachment> Attachments { get; } = new();
}

public class Label
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public Board Board { get; set; } = null!;
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#61BD4F";

    public List<CardLabel> CardLabels { get; } = new();
}

public class CardLabel
{
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;
    public int LabelId { get; set; }
    public Label Label { get; set; } = null!;
}

public class Checklist
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;
    public string Title { get; set; } = "";
    public int Position { get; set; }

    public List<ChecklistItem> Items { get; } = new();
}

public class ChecklistItem
{
    public int Id { get; set; }
    public int ChecklistId { get; set; }
    public Checklist Checklist { get; set; } = null!;
    public string Text { get; set; } = "";
    public bool IsChecked { get; set; }
    public int Position { get; set; }
}

public class Comment
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public class Attachment
{
    public int Id { get; set; }
    public int CardId { get; set; }
    public Card Card { get; set; } = null!;
    public string FileName { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public DateTime AddedAt { get; set; }
}

public class ActivityEntry
{
    public int Id { get; set; }
    public int BoardId { get; set; }
    public Board Board { get; set; } = null!;
    // Intentionally not a foreign key: activity history must survive card deletion.
    public int? CardId { get; set; }
    public string Description { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
