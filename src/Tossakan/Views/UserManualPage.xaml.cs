using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Tossakan.Helpers;
using Colors = Microsoft.UI.Colors;

namespace Tossakan.Views;

public sealed partial class UserManualPage : Page
{
    private readonly Dictionary<string, Expander> _sections = new();

    public UserManualPage()
    {
        InitializeComponent();

        ContentPanel.Children.Add(MarkdownRenderer.Render(IntroText));
        ContentPanel.Children.Add(BuildInfographic());

        AddSection("boards", "Boards", BoardsText, expanded: true);
        AddSection("lists", "Lists", ListsText);
        AddSection("cards", "Cards", CardsText);
        AddSection("carddetails", "Card details", CardDetailsText);
        AddSection("quickview", "Quick View", QuickViewText);
        AddSection("backgrounds", "Backgrounds", BackgroundsText);
        AddSection("search", "Search and filtering", SearchText);
        AddSection("activity", "Activity and archive", ActivityText);
        AddSection("data", "Your data", DataText);
    }

    private void AddSection(string key, string title, string markdown, bool expanded = false)
    {
        var expander = new Expander
        {
            Header = title,
            Content = MarkdownRenderer.Render(markdown),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            IsExpanded = expanded,
            Margin = new Thickness(0, 0, 0, 8),
        };
        _sections[key] = expander;
        ContentPanel.Children.Add(expander);
    }

    private void JumpToSection(string key)
    {
        if (!_sections.TryGetValue(key, out var expander)) return;
        expander.IsExpanded = true;
        expander.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0.0 });
    }

    // ---------- Infographic ----------

    /// <summary>A native, clickable map of Board → List → Card → (everything on a card), plus the
    /// cross-board Quick View panel. Clicking a node expands and scrolls to its section below.</summary>
    private UIElement BuildInfographic()
    {
        var panel = new StackPanel { Spacing = 10, Margin = new Thickness(0, 4, 0, 20) };

        panel.Children.Add(new TextBlock
        {
            Text = "How it fits together",
            Style = (Style)Application.Current.Resources["BodyStrongTextBlockStyle"],
        });
        panel.Children.Add(new TextBlock
        {
            Text = "Click a box to jump to that section.",
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        var card = new Border
        {
            Background = (Brush)Application.Current.Resources["CardSurfaceBrush"],
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16),
        };
        var content = new StackPanel { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center };
        card.Child = content;

        var chain = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        chain.Children.Add(BuildNode("Board", "boards", "#2563EB"));
        chain.Children.Add(BuildArrow());
        chain.Children.Add(BuildNode("List", "lists", "#7C3AED"));
        chain.Children.Add(BuildArrow());
        chain.Children.Add(BuildNode("Card", "cards", "#059669"));
        content.Children.Add(chain);

        content.Children.Add(new TextBlock
        {
            Text = "opens to reveal ↓",
            FontSize = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });

        var detailRow1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var label in new[] { "Labels", "Due date", "Cover", "Description" })
            detailRow1.Children.Add(BuildNode(label, "carddetails", "#475569", small: true));
        content.Children.Add(detailRow1);

        var detailRow2 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var label in new[] { "Checklists", "Attachments", "Comments" })
            detailRow2.Children.Add(BuildNode(label, "carddetails", "#475569", small: true));
        content.Children.Add(detailRow2);

        content.Children.Add(new Border
        {
            Height = 1,
            Margin = new Thickness(0, 4, 0, 4),
            Background = (Brush)Application.Current.Resources["DividerStrokeColorDefaultBrush"],
            Width = 260,
        });

        var quickViewRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        quickViewRow.Children.Add(new TextBlock
        {
            Text = "Across every board:",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
        });
        quickViewRow.Children.Add(BuildNode("Quick View", "quickview", "#EB5A46"));
        content.Children.Add(quickViewRow);

        panel.Children.Add(card);
        return panel;
    }

    private Button BuildNode(string text, string sectionKey, string colorHex, bool small = false)
    {
        var border = new Border
        {
            Background = Ui.ToBrush(colorHex),
            CornerRadius = new CornerRadius(6),
            Padding = small ? new Thickness(10, 6, 10, 6) : new Thickness(16, 10, 16, 10),
        };
        border.Child = new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Colors.White),
            FontSize = small ? 11 : 13,
            FontWeight = small ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
        };

        var button = new Button
        {
            Content = border,
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
        };
        ToolTipService.SetToolTip(button, $"Jump to “{text}”");
        button.Click += (_, _) => JumpToSection(sectionKey);
        return button;
    }

    private static TextBlock BuildArrow() => new()
    {
        Text = "→",
        FontSize = 18,
        Margin = new Thickness(8, 0, 8, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
    };

    // ---------- Content ----------

    private const string IntroText = @"# User manual

Tossakan is a single-user, local kanban board app. Everything you create is stored on this
computer only, under **%LOCALAPPDATA%\Tossakan\**. Use the **Home**, **Settings** and
**User Manual** links in the left sidebar to get around.";

    private const string BoardsText = @"- On the home screen, click **Create new board** to start a new board.
- Right-click (or use the **…** menu on) a board tile to *Rename / background*, *Archive*, or *Delete* it.
- Archived boards appear in the *Archived boards* section on the home screen, where you can restore or permanently delete them.
- Deleting a board permanently deletes all of its lists, cards, checklists, comments and attachments.";

    private const string ListsText = @"- Inside a board, click **Add another list** to create a list.
- Click directly on a list's name to rename it in place — edit the text and click elsewhere (or press Enter) to save.
- Use a list's **…** menu to archive or delete it.
- Drag a list by its header to reorder it.";

    private const string CardsText = @"- Click **Add a card** at the bottom of a list to create a card.
- Click a card to open it and edit its details.
- Drag a card to move it within a list or into another list.
- Right-click a card for quick actions: *Open*, *Archive*, *Delete*.
- A card's cover color and label chips show right on its face, so a board stays scannable without opening every card.";

    private const string CardDetailsText = @"Opening a card lets you edit everything about it:

**Labels** — click a label chip to toggle it on or off the card, click the pencil icon on a chip to rename, recolor, or delete that label, and use **New label** to create one.

**Due date** — pick a date, tick **Complete**, or use **Remove** to clear it. Cards show an overdue/complete indicator on the board.

**Cover** — pick a color from the palette to show as a banner on the card, or click **None** to clear it.

**Description** — supports basic markdown: headers, bullet/numbered lists, **bold**, *italic*, `inline code` and images. Click the description to edit it, make your changes, then click **Save**. You can also paste an image straight from the clipboard (Ctrl+V) into the box — it's stored as an attachment and inserted inline automatically.

**Checklists** — **Add checklist** to create one, click a checklist's title to rename it, add items with the text box, and tick them off. The progress bar updates as items are checked.

**Attachments** — **Attach file** copies any file into Tossakan's own storage folder. Image and markdown attachments get a thumbnail and a **Preview** button that opens them in a preview window without leaving the app; other files use **Open** to launch them in their default program.

**Comments** — a running log of notes on the card, also rendered as markdown, with the same paste-image shortcut. Click a comment to edit it, then **Save**.";

    private const string QuickViewText = @"The **Quick View** panel on the right edge of the window lists cards from every board at once, so
you can see what's coming up without hunting through each board:

- Cards with a due date are listed soonest-first; cards with no due date are grouped in afterward.
- Click the bell icon at the top of the panel to filter down to only cards **without** a due date.
- Click a card in the list to open its full detail dialog.
- Use the move icon on a card to send it straight to another list on its board, without opening it.
- Collapse the panel with the arrow at its top-right to reclaim screen space, and use the refresh icon to pick up changes made elsewhere.";

    private const string BackgroundsText = @"- **Board background** — open a board and click its name (top-left), or use a board tile's *Rename / background* menu, to pick a solid color or a photo.
- **Homepage background** — open **Settings** from the sidebar to set a photo behind your board list on the home screen.";

    private const string SearchText = @"- The search box at the top of the window searches every card's title across all boards.
- Inside a board, the **Filter** button narrows the visible cards by keyword, label, or due date.";

    private const string ActivityText = @"- The **Activity** button on a board shows a history of changes made to it.
- The **Archive** button lists archived cards and lists, so you can restore or permanently delete them.";

    private const string DataText = @"Everything lives under **%LOCALAPPDATA%\Tossakan\**:

- **workmanagement.db** — the database with all boards, lists, cards and settings.
- **attachments\** — files attached to cards, including images pasted from the clipboard.
- **backgrounds\** — custom background photos you've picked.
- **app.log** — a log file that's useful if something goes wrong.";
}
