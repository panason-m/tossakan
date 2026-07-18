using Microsoft.EntityFrameworkCore;
using Tossakan.Models;

namespace Tossakan.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardList> Lists => Set<BoardList>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<CardLabel> CardLabels => Set<CardLabel>();
    public DbSet<Checklist> Checklists => Set<Checklist>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<ActivityEntry> Activities => Set<ActivityEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CardLabel>().HasKey(cl => new { cl.CardId, cl.LabelId });

        modelBuilder.Entity<Board>()
            .HasMany(b => b.Lists).WithOne(l => l.Board)
            .HasForeignKey(l => l.BoardId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Board>()
            .HasMany(b => b.Labels).WithOne(l => l.Board)
            .HasForeignKey(l => l.BoardId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Board>()
            .HasMany(b => b.Activities).WithOne(a => a.Board)
            .HasForeignKey(a => a.BoardId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BoardList>()
            .HasMany(l => l.Cards).WithOne(c => c.List)
            .HasForeignKey(c => c.ListId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Card>()
            .HasMany(c => c.CardLabels).WithOne(cl => cl.Card)
            .HasForeignKey(cl => cl.CardId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Label>()
            .HasMany(l => l.CardLabels).WithOne(cl => cl.Label)
            .HasForeignKey(cl => cl.LabelId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Card>()
            .HasMany(c => c.Checklists).WithOne(cl => cl.Card)
            .HasForeignKey(cl => cl.CardId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Checklist>()
            .HasMany(cl => cl.Items).WithOne(i => i.Checklist)
            .HasForeignKey(i => i.ChecklistId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Card>()
            .HasMany(c => c.Comments).WithOne(cm => cm.Card)
            .HasForeignKey(cm => cm.CardId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Card>()
            .HasMany(c => c.Attachments).WithOne(a => a.Card)
            .HasForeignKey(a => a.CardId).OnDelete(DeleteBehavior.Cascade);
    }
}
