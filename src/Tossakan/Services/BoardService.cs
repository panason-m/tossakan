using Microsoft.EntityFrameworkCore;
using Tossakan.Data;
using Tossakan.Models;

namespace Tossakan.Services;

public record SearchResult(int CardId, string CardTitle, string ListName, int BoardId, string BoardName);

public record QuickViewCardInfo(
    int CardId, string CardTitle, DateTime? DueDate,
    int ListId, string ListName, int BoardId, string BoardName);

public record BoardListInfo(int ListId, string ListName);

public class BoardService
{
    private const int PositionGap = 1024;
    private readonly IDbContextFactory<AppDbContext> _factory;

    public BoardService(IDbContextFactory<AppDbContext> factory) => _factory = factory;

    private static void Log(AppDbContext db, int boardId, int? cardId, string description)
        => db.Activities.Add(new ActivityEntry
        {
            BoardId = boardId,
            CardId = cardId,
            Description = description,
            CreatedAt = DateTime.Now,
        });

    // ---------- Boards ----------

    public async Task<List<Board>> GetBoardsAsync(bool includeArchived = false)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var query = db.Boards.AsNoTracking();
        if (!includeArchived) query = query.Where(b => !b.IsArchived);
        return await query.OrderBy(b => b.Position).ToListAsync();
    }

    public async Task<Board?> GetBoardFullAsync(int boardId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        // Repeated navigations must carry identical filters, so the archive filter
        // appears verbatim on every chain; ordering happens in the view models.
        return await db.Boards.AsNoTracking()
            .Include(b => b.Labels)
            .Include(b => b.Lists.Where(l => !l.IsArchived))
                .ThenInclude(l => l.Cards.Where(c => !c.IsArchived))
                .ThenInclude(c => c.CardLabels)
            .Include(b => b.Lists.Where(l => !l.IsArchived))
                .ThenInclude(l => l.Cards.Where(c => !c.IsArchived))
                .ThenInclude(c => c.Checklists).ThenInclude(cl => cl.Items)
            .Include(b => b.Lists.Where(l => !l.IsArchived))
                .ThenInclude(l => l.Cards.Where(c => !c.IsArchived))
                .ThenInclude(c => c.Comments)
            .Include(b => b.Lists.Where(l => !l.IsArchived))
                .ThenInclude(l => l.Cards.Where(c => !c.IsArchived))
                .ThenInclude(c => c.Attachments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == boardId);
    }

    public async Task<Board> CreateBoardAsync(string name, string backgroundColor, string? backgroundImagePath = null)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var maxPos = await db.Boards.MaxAsync(b => (int?)b.Position) ?? 0;
        var board = new Board
        {
            Name = name,
            BackgroundColor = backgroundColor,
            BackgroundImagePath = backgroundImagePath,
            Position = maxPos + PositionGap,
            CreatedAt = DateTime.Now,
        };
        db.Boards.Add(board);
        await db.SaveChangesAsync();

        // Default Trello-style labels for the new board
        string[][] defaults =
        {
            new[] { "", "#61BD4F" }, new[] { "", "#F2D600" }, new[] { "", "#FF9F1A" },
            new[] { "", "#EB5A46" }, new[] { "", "#C377E0" }, new[] { "", "#0079BF" },
        };
        foreach (var d in defaults)
            db.Labels.Add(new Label { BoardId = board.Id, Name = d[0], Color = d[1] });
        Log(db, board.Id, null, $"Board \"{name}\" created");
        await db.SaveChangesAsync();
        return board;
    }

    public async Task RenameBoardAsync(int boardId, string newName)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var board = await db.Boards.FindAsync(boardId);
        if (board is null) return;
        Log(db, boardId, null, $"Board renamed from \"{board.Name}\" to \"{newName}\"");
        board.Name = newName;
        await db.SaveChangesAsync();
    }

    public async Task SetBoardBackgroundAsync(int boardId, string color)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var board = await db.Boards.FindAsync(boardId);
        if (board is null) return;
        board.BackgroundColor = color;
        await db.SaveChangesAsync();
    }

    /// <summary>Sets or clears the board's background photo. Pass null to fall back to the solid color.</summary>
    public async Task SetBoardBackgroundImageAsync(int boardId, string? imagePath)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var board = await db.Boards.FindAsync(boardId);
        if (board is null) return;
        var oldPath = board.BackgroundImagePath;
        board.BackgroundImagePath = imagePath;
        Log(db, boardId, null, imagePath is null
            ? $"Board \"{board.Name}\" background photo removed"
            : $"Board \"{board.Name}\" background photo changed");
        await db.SaveChangesAsync();

        if (oldPath is not null && oldPath != imagePath && BackgroundImageService.IsOwnedCustomImage(oldPath))
        {
            try { if (File.Exists(oldPath)) File.Delete(oldPath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public async Task SetBoardArchivedAsync(int boardId, bool archived)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var board = await db.Boards.FindAsync(boardId);
        if (board is null) return;
        board.IsArchived = archived;
        Log(db, boardId, null, archived ? $"Board \"{board.Name}\" archived" : $"Board \"{board.Name}\" restored");
        await db.SaveChangesAsync();
    }

    public async Task DeleteBoardAsync(int boardId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var board = await db.Boards.FindAsync(boardId);
        if (board is null) return;
        var files = await db.Attachments
            .Where(a => db.Cards.Any(c => c.Id == a.CardId && db.Lists.Any(l => l.Id == c.ListId && l.BoardId == boardId)))
            .Select(a => a.StoredPath).ToListAsync();
        db.Boards.Remove(board);
        await db.SaveChangesAsync();
        AttachmentService.DeleteFiles(files);
    }

    // ---------- Lists ----------

    public async Task<BoardList> AddListAsync(int boardId, string name)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var maxPos = await db.Lists.Where(l => l.BoardId == boardId).MaxAsync(l => (int?)l.Position) ?? 0;
        var list = new BoardList { BoardId = boardId, Name = name, Position = maxPos + PositionGap };
        db.Lists.Add(list);
        Log(db, boardId, null, $"List \"{name}\" added");
        await db.SaveChangesAsync();
        return list;
    }

    public async Task RenameListAsync(int listId, string newName)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var list = await db.Lists.FindAsync(listId);
        if (list is null) return;
        Log(db, list.BoardId, null, $"List \"{list.Name}\" renamed to \"{newName}\"");
        list.Name = newName;
        await db.SaveChangesAsync();
    }

    public async Task SetListArchivedAsync(int listId, bool archived)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var list = await db.Lists.FindAsync(listId);
        if (list is null) return;
        list.IsArchived = archived;
        Log(db, list.BoardId, null, archived ? $"List \"{list.Name}\" archived" : $"List \"{list.Name}\" restored");
        await db.SaveChangesAsync();
    }

    public async Task DeleteListAsync(int listId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var list = await db.Lists.FindAsync(listId);
        if (list is null) return;
        var files = await db.Attachments
            .Where(a => db.Cards.Any(c => c.Id == a.CardId && c.ListId == listId))
            .Select(a => a.StoredPath).ToListAsync();
        Log(db, list.BoardId, null, $"List \"{list.Name}\" deleted");
        db.Lists.Remove(list);
        await db.SaveChangesAsync();
        AttachmentService.DeleteFiles(files);
    }

    /// <summary>Moves a list to <paramref name="targetIndex"/> among the board's active lists.</summary>
    public async Task MoveListAsync(int listId, int targetIndex)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var list = await db.Lists.FindAsync(listId);
        if (list is null) return;
        var lists = await db.Lists
            .Where(l => l.BoardId == list.BoardId && !l.IsArchived)
            .OrderBy(l => l.Position).ToListAsync();
        lists.RemoveAll(l => l.Id == listId);
        targetIndex = Math.Clamp(targetIndex, 0, lists.Count);
        lists.Insert(targetIndex, list);
        for (int i = 0; i < lists.Count; i++)
            lists[i].Position = (i + 1) * PositionGap;
        await db.SaveChangesAsync();
    }

    // ---------- Cards ----------

    public async Task<Card> AddCardAsync(int listId, string title)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var list = await db.Lists.Include(l => l.Board).FirstOrDefaultAsync(l => l.Id == listId)
            ?? throw new InvalidOperationException($"List {listId} not found");
        var maxPos = await db.Cards.Where(c => c.ListId == listId).MaxAsync(c => (int?)c.Position) ?? 0;
        var card = new Card
        {
            ListId = listId,
            Title = title,
            Position = maxPos + PositionGap,
            CreatedAt = DateTime.Now,
            ReferenceNumber = list.Board.NextCardNumber,
        };
        list.Board.NextCardNumber++;
        db.Cards.Add(card);
        await db.SaveChangesAsync();
        Log(db, list.BoardId, card.Id, $"Card \"{title}\" added to \"{list.Name}\"");
        await db.SaveChangesAsync();
        return card;
    }

    public async Task<Card?> GetCardFullAsync(int cardId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Cards.AsNoTracking()
            .Include(c => c.List).ThenInclude(l => l.Board)
            .Include(c => c.CardLabels).ThenInclude(cl => cl.Label)
            .Include(c => c.Checklists.OrderBy(cl => cl.Position)).ThenInclude(cl => cl.Items.OrderBy(i => i.Position))
            .Include(c => c.Comments)
            .Include(c => c.Attachments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(c => c.Id == cardId);
    }

    public async Task UpdateCardTitleAsync(int cardId, string title)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var card = await db.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null) return;
        Log(db, card.List.BoardId, cardId, $"Card \"{card.Title}\" renamed to \"{title}\"");
        card.Title = title;
        await db.SaveChangesAsync();
    }

    public async Task UpdateCardDescriptionAsync(int cardId, string description)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var card = await db.Cards.FindAsync(cardId);
        if (card is null) return;
        card.Description = description;
        await db.SaveChangesAsync();
    }

    public async Task UpdateCardDueDateAsync(int cardId, DateTime? dueDate, bool isDueComplete)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var card = await db.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null) return;
        if (card.DueDate != dueDate)
            Log(db, card.List.BoardId, cardId, dueDate is null
                ? $"Due date removed from \"{card.Title}\""
                : $"Card \"{card.Title}\" due {dueDate:d}");
        card.DueDate = dueDate;
        card.IsDueComplete = isDueComplete;
        await db.SaveChangesAsync();
    }

    public async Task UpdateCardCoverAsync(int cardId, string? coverColor)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var card = await db.Cards.FindAsync(cardId);
        if (card is null) return;
        card.CoverColor = coverColor;
        await db.SaveChangesAsync();
    }

    public async Task SetCardArchivedAsync(int cardId, bool archived)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var card = await db.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null) return;
        card.IsArchived = archived;
        Log(db, card.List.BoardId, cardId, archived ? $"Card \"{card.Title}\" archived" : $"Card \"{card.Title}\" restored");
        await db.SaveChangesAsync();
    }

    public async Task DeleteCardAsync(int cardId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var card = await db.Cards.Include(c => c.List).Include(c => c.Attachments)
            .FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null) return;
        var files = card.Attachments.Select(a => a.StoredPath).ToList();
        Log(db, card.List.BoardId, null, $"Card \"{card.Title}\" deleted");
        db.Cards.Remove(card);
        await db.SaveChangesAsync();
        AttachmentService.DeleteFiles(files);
    }

    /// <summary>Moves a card into <paramref name="targetListId"/> at <paramref name="targetIndex"/> among active cards.</summary>
    public async Task MoveCardAsync(int cardId, int targetListId, int targetIndex)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var card = await db.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId);
        if (card is null) return;
        var targetList = await db.Lists.FindAsync(targetListId);
        if (targetList is null) return;

        if (card.ListId != targetListId)
            Log(db, targetList.BoardId, cardId, $"Card \"{card.Title}\" moved from \"{card.List.Name}\" to \"{targetList.Name}\"");

        card.ListId = targetListId;
        var cards = await db.Cards
            .Where(c => c.ListId == targetListId && !c.IsArchived && c.Id != cardId)
            .OrderBy(c => c.Position).ToListAsync();
        targetIndex = Math.Clamp(targetIndex, 0, cards.Count);
        cards.Insert(targetIndex, card);
        for (int i = 0; i < cards.Count; i++)
            cards[i].Position = (i + 1) * PositionGap;
        await db.SaveChangesAsync();
    }

    // ---------- Labels ----------

    public async Task<List<Label>> GetLabelsAsync(int boardId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Labels.AsNoTracking().Where(l => l.BoardId == boardId).OrderBy(l => l.Id).ToListAsync();
    }

    public async Task<Label> CreateLabelAsync(int boardId, string name, string color)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var label = new Label { BoardId = boardId, Name = name, Color = color };
        db.Labels.Add(label);
        await db.SaveChangesAsync();
        return label;
    }

    public async Task UpdateLabelAsync(int labelId, string name, string color)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var label = await db.Labels.FindAsync(labelId);
        if (label is null) return;
        label.Name = name;
        label.Color = color;
        await db.SaveChangesAsync();
    }

    public async Task DeleteLabelAsync(int labelId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var label = await db.Labels.FindAsync(labelId);
        if (label is null) return;
        db.Labels.Remove(label);
        await db.SaveChangesAsync();
    }

    public async Task SetCardLabelAsync(int cardId, int labelId, bool assigned)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var existing = await db.CardLabels.FindAsync(cardId, labelId);
        if (assigned && existing is null)
            db.CardLabels.Add(new CardLabel { CardId = cardId, LabelId = labelId });
        else if (!assigned && existing is not null)
            db.CardLabels.Remove(existing);
        await db.SaveChangesAsync();
    }

    // ---------- Checklists ----------

    public async Task<Checklist> AddChecklistAsync(int cardId, string title)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var maxPos = await db.Checklists.Where(c => c.CardId == cardId).MaxAsync(c => (int?)c.Position) ?? 0;
        var checklist = new Checklist { CardId = cardId, Title = title, Position = maxPos + PositionGap };
        db.Checklists.Add(checklist);
        await db.SaveChangesAsync();
        return checklist;
    }

    public async Task RenameChecklistAsync(int checklistId, string title)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var checklist = await db.Checklists.FindAsync(checklistId);
        if (checklist is null) return;
        checklist.Title = title;
        await db.SaveChangesAsync();
    }

    public async Task DeleteChecklistAsync(int checklistId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var checklist = await db.Checklists.FindAsync(checklistId);
        if (checklist is null) return;
        db.Checklists.Remove(checklist);
        await db.SaveChangesAsync();
    }

    public async Task<ChecklistItem> AddChecklistItemAsync(int checklistId, string text)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var maxPos = await db.ChecklistItems.Where(i => i.ChecklistId == checklistId).MaxAsync(i => (int?)i.Position) ?? 0;
        var item = new ChecklistItem { ChecklistId = checklistId, Text = text, Position = maxPos + PositionGap };
        db.ChecklistItems.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    public async Task UpdateChecklistItemAsync(int itemId, string text, bool isChecked)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var item = await db.ChecklistItems.FindAsync(itemId);
        if (item is null) return;
        item.Text = text;
        item.IsChecked = isChecked;
        await db.SaveChangesAsync();
    }

    public async Task DeleteChecklistItemAsync(int itemId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var item = await db.ChecklistItems.FindAsync(itemId);
        if (item is null) return;
        db.ChecklistItems.Remove(item);
        await db.SaveChangesAsync();
    }

    // ---------- Comments ----------

    public async Task<Comment> AddCommentAsync(int cardId, string text)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var card = await db.Cards.Include(c => c.List).FirstOrDefaultAsync(c => c.Id == cardId)
            ?? throw new InvalidOperationException($"Card {cardId} not found");
        var comment = new Comment { CardId = cardId, Text = text, CreatedAt = DateTime.Now };
        db.Comments.Add(comment);
        Log(db, card.List.BoardId, cardId, $"Comment added on \"{card.Title}\"");
        await db.SaveChangesAsync();
        return comment;
    }

    public async Task UpdateCommentAsync(int commentId, string text)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var comment = await db.Comments.FindAsync(commentId);
        if (comment is null) return;
        comment.Text = text;
        await db.SaveChangesAsync();
    }

    public async Task DeleteCommentAsync(int commentId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var comment = await db.Comments.FindAsync(commentId);
        if (comment is null) return;
        db.Comments.Remove(comment);
        await db.SaveChangesAsync();
    }

    // ---------- Quick view ----------

    public async Task<List<QuickViewCardInfo>> GetQuickViewCardsAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Cards.AsNoTracking()
            .Where(c => !c.IsArchived && !c.IsDueComplete
                     && !c.List.IsArchived && !c.List.Board.IsArchived)
            .OrderBy(c => c.DueDate == null)
            .ThenBy(c => c.DueDate)
            .Select(c => new QuickViewCardInfo(
                c.Id, c.Title, c.DueDate,
                c.ListId, c.List.Name, c.List.BoardId, c.List.Board.Name))
            .ToListAsync();
    }

    public async Task<List<BoardListInfo>> GetActiveListsForBoardAsync(int boardId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Lists.AsNoTracking()
            .Where(l => l.BoardId == boardId && !l.IsArchived)
            .OrderBy(l => l.Position)
            .Select(l => new BoardListInfo(l.Id, l.Name))
            .ToListAsync();
    }

    // ---------- Search & activity ----------

    public async Task<List<SearchResult>> SearchCardsAsync(string text, int take = 20)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<SearchResult>();
        await using var db = await _factory.CreateDbContextAsync();
        var pattern = $"%{text.Trim()}%";
        return await db.Cards.AsNoTracking()
            .Where(c => !c.IsArchived && !c.List.IsArchived && !c.List.Board.IsArchived)
            .Where(c => EF.Functions.Like(c.Title, pattern) || EF.Functions.Like(c.Description, pattern))
            .OrderBy(c => c.List.Board.Position).ThenBy(c => c.List.Position).ThenBy(c => c.Position)
            .Take(take)
            .Select(c => new SearchResult(c.Id, c.Title, c.List.Name, c.List.BoardId, c.List.Board.Name))
            .ToListAsync();
    }

    public async Task<List<ActivityEntry>> GetActivityAsync(int boardId, int take = 100)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Activities.AsNoTracking()
            .Where(a => a.BoardId == boardId)
            .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id)
            .Take(take).ToListAsync();
    }

    public async Task<(List<Card> Cards, List<BoardList> Lists)> GetArchivedAsync(int boardId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var cards = await db.Cards.AsNoTracking()
            .Include(c => c.List)
            .Where(c => c.IsArchived && c.List.BoardId == boardId)
            .OrderByDescending(c => c.Id).ToListAsync();
        var lists = await db.Lists.AsNoTracking()
            .Where(l => l.IsArchived && l.BoardId == boardId)
            .OrderByDescending(l => l.Id).ToListAsync();
        return (cards, lists);
    }
}
