using Microsoft.EntityFrameworkCore;
using Tossakan.Models;
using Tossakan.Services;

namespace Tossakan.Data;

public static class DatabaseInitializer
{
    /// <summary>Creates the schema if needed and seeds a welcome board on first run.</summary>
    public static async Task InitializeAsync(IDbContextFactory<AppDbContext> factory)
    {
        await using var db = await factory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        await EnsureBackgroundImageColumnAsync(db);
        await EnsureCardReferenceColumnsAsync(db);

        if (await db.Boards.AnyAsync()) return;

        var board = new Board
        {
            Name = "Welcome Board",
            BackgroundColor = "#0079BF",
            BackgroundImagePath = File.Exists(BackgroundImageService.DefaultImagePath) ? BackgroundImageService.DefaultImageMarker : null,
            Position = 1024,
            CreatedAt = DateTime.Now,
        };
        db.Boards.Add(board);

        var labels = new[]
        {
            new Label { Board = board, Name = "Priority", Color = "#EB5A46" },
            new Label { Board = board, Name = "Nice to have", Color = "#61BD4F" },
            new Label { Board = board, Name = "Waiting", Color = "#F2D600" },
            new Label { Board = board, Name = "", Color = "#FF9F1A" },
            new Label { Board = board, Name = "", Color = "#C377E0" },
            new Label { Board = board, Name = "", Color = "#0079BF" },
        };
        db.Labels.AddRange(labels);

        var todo = new BoardList { Board = board, Name = "To Do", Position = 1024 };
        var doing = new BoardList { Board = board, Name = "Doing", Position = 2048 };
        var done = new BoardList { Board = board, Name = "Done", Position = 3072 };
        db.Lists.AddRange(todo, doing, done);

        var card1 = new Card
        {
            List = todo,
            Title = "Double-click a card to open it",
            Description = "The card dialog holds the description, labels, due date, checklists, comments and attachments.",
            Position = 1024,
            CreatedAt = DateTime.Now,
            ReferenceNumber = 1,
        };
        var card2 = new Card
        {
            List = todo,
            Title = "Drag cards between lists",
            Position = 2048,
            CreatedAt = DateTime.Now,
            ReferenceNumber = 2,
        };
        var card3 = new Card
        {
            List = doing,
            Title = "Create your own board from the home screen",
            Position = 1024,
            CreatedAt = DateTime.Now,
            ReferenceNumber = 3,
        };
        board.NextCardNumber = 4;
        db.Cards.AddRange(card1, card2, card3);
        db.CardLabels.Add(new CardLabel { Card = card1, Label = labels[1] });
        db.CardLabels.Add(new CardLabel { Card = card3, Label = labels[0] });

        var checklist = new Checklist { Card = card1, Title = "Things to try", Position = 1024 };
        db.Checklists.Add(checklist);
        db.ChecklistItems.AddRange(
            new ChecklistItem { Checklist = checklist, Text = "Add a label", Position = 1024 },
            new ChecklistItem { Checklist = checklist, Text = "Set a due date", Position = 2048 },
            new ChecklistItem { Checklist = checklist, Text = "Tick a checklist item", IsChecked = true, Position = 3072 });

        db.Activities.Add(new ActivityEntry
        {
            Board = board,
            Description = "Board \"Welcome Board\" created",
            CreatedAt = DateTime.Now,
        });

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// EnsureCreatedAsync only builds the schema for a brand-new database file, so a database created
    /// before the BackgroundImagePath column existed needs it added by hand. Since this only runs the
    /// one time the column is actually missing, it also backfills the new default photo onto every
    /// existing board (which otherwise all have BackgroundImagePath = null) without ever overwriting a
    /// choice the user makes afterwards.
    /// </summary>
    private static async Task EnsureBackgroundImageColumnAsync(AppDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed) await connection.OpenAsync();
        try
        {
            var hasColumn = false;
            await using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(Boards)";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    if (string.Equals(reader.GetString(1), "BackgroundImagePath", StringComparison.OrdinalIgnoreCase))
                    {
                        hasColumn = true;
                        break;
                    }
                }
            }

            if (!hasColumn)
            {
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Boards ADD COLUMN BackgroundImagePath TEXT NULL");
                if (File.Exists(BackgroundImageService.DefaultImagePath))
                    await db.Database.ExecuteSqlRawAsync(
                        "UPDATE Boards SET BackgroundImagePath = {0}", BackgroundImageService.DefaultImageMarker);
            }
        }
        finally
        {
            if (wasClosed) await connection.CloseAsync();
        }
    }

    /// <summary>
    /// Adds Boards.NextCardNumber / Cards.ReferenceNumber to databases created before per-project card
    /// reference numbers (e.g. "TP-36") existed, then backfills every existing card with a sequential
    /// number in creation order so old cards get stable codes too, without ever renumbering a card that
    /// already has one.
    /// </summary>
    private static async Task EnsureCardReferenceColumnsAsync(AppDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed) await connection.OpenAsync();
        try
        {
            var boardsHasColumn = await HasColumnAsync(connection, "Boards", "NextCardNumber");
            var cardsHasColumn = await HasColumnAsync(connection, "Cards", "ReferenceNumber");

            if (!boardsHasColumn)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Boards ADD COLUMN NextCardNumber INTEGER NOT NULL DEFAULT 1");
            if (!cardsHasColumn)
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Cards ADD COLUMN ReferenceNumber INTEGER NOT NULL DEFAULT 0");

            if (!boardsHasColumn || !cardsHasColumn)
            {
                var boardIds = await db.Boards.Select(b => b.Id).ToListAsync();
                foreach (var boardId in boardIds)
                {
                    var cards = await db.Cards
                        .Where(c => c.List.BoardId == boardId)
                        .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
                        .ToListAsync();
                    var next = 1;
                    foreach (var card in cards)
                    {
                        if (card.ReferenceNumber == 0) card.ReferenceNumber = next;
                        next++;
                    }
                    var board = await db.Boards.FindAsync(boardId);
                    if (board is not null && board.NextCardNumber < next) board.NextCardNumber = next;
                }
                await db.SaveChangesAsync();
            }
        }
        finally
        {
            if (wasClosed) await connection.CloseAsync();
        }
    }

    private static async Task<bool> HasColumnAsync(System.Data.Common.DbConnection connection, string table, string column)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
