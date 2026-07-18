# Tossakan — Trello-like kanban app (WinUI 3)

Single-user, local-only Trello clone (no login). WinUI 3 + .NET 8 + CommunityToolkit.Mvvm + EF Core/SQLite.
Built entirely with the dotnet CLI — **no Visual Studio on this machine**.

## Commands

- Build: `dotnet build` (from repo root; Debug exe at `src\Tossakan\bin\x64\Debug\net8.0-windows10.0.19041.0\win-x64\Tossakan.exe`)
- Run: `dotnet run --project "src\Tossakan"`
- Publish standalone exe + desktop shortcut: `powershell -ExecutionPolicy Bypass -File .\publish.ps1` → `publish\Tossakan.exe` + `Tossakan.lnk` on the desktop

## Architecture

- `Models\Entities.cs` — all EF entities (Board → BoardList → Card → CardLabel/Checklist/ChecklistItem/Comment/Attachment; Label per board; ActivityEntry per board with non-FK CardId)
- `Data\AppDbContext.cs`, `Data\DatabaseInitializer.cs` — `EnsureCreated()` + welcome-board seed on first run (no migrations)
- `Services\BoardService.cs` — ALL CRUD/reorder/archive/search; every mutation also writes an ActivityEntry. Ordering = integer `Position` gaps of 1024, whole list renumbered on each move
- `Services\AttachmentService.cs` — copies files into `%LOCALAPPDATA%\Tossakan\attachments\`; deleting cards/lists/boards must clean up stored files
- `ViewModels\` — CardViewModel is an **immutable snapshot**, rebuilt/replaced in place after edits; ListViewModel holds the ObservableCollection used by drag-drop
- `Views\` — HomePage (board tiles), BoardPage (lists/cards, drag-drop, filter/activity/archive flyouts), CardDetailDialog (everything on a card), BoardEditDialog
- DI: plain ServiceCollection in `App.xaml.cs` (`App.Services`)
- User data: `%LOCALAPPDATA%\Tossakan\` (workmanagement.db, attachments\, app.log). Unhandled exceptions are logged to app.log — check it when the UI silently does nothing.

## Pitfalls already hit (do not regress)

- EF filtered includes: the same navigation repeated across `.Include()` chains needs the **identical** filter on every chain, or the query throws at runtime.
- Keep `Microsoft.WindowsAppSDK` on 1.8.x and EF Core on 8.x (net8 target). WindowsAppSDK 2.x requirements unverified.
- ToggleButton's checked visual state overrides local Background — label chips use Button + checkmark icon instead.
- Card moves persist via `MoveCardAsync(cardId, targetListId, indexAmongActiveCards)`; cross-list drops are handled in `Cards_Drop`, within-list reorder in `Cards_DragItemsCompleted` (guarded by `_crossListDropHandled`).
- Nested ListViews (lists + cards) share drag gestures: dragging a card bubbles into the outer `ListsView` and also gets recognized as a list-reorder gesture, leaving a visible gap where the list column briefly detaches. Fixed by toggling `ListsView.CanDragItems`/`CanReorderItems` off in `Cards_DragItemsStarting` and back on in `Cards_DragItemsCompleted`.

## Testing

No unit tests; service layer is UI-free if tests are added later. UI smoke-testing from the agent sandbox: synthetic mouse input is blocked — drive the app via UI Automation (InvokePattern/ValuePattern; buttons with panel content have no Name, find inner text and walk up the tree). Drag-and-drop and the file picker require a human.
