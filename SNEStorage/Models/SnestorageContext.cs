using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace SNEStorage.Models;

public partial class SnestorageContext : IdentityDbContext
{
    public SnestorageContext()
    {
    }
    public SnestorageContext(DbContextOptions<SnestorageContext> options)
        : base(options)
    {
    }
    public virtual DbSet<Comment> Comments { get; set; }
    public virtual DbSet<File> Files { get; set; }
    public virtual DbSet<FileType> FileTypes { get; set; }
    public virtual DbSet<LikeUsers> LikesUsers { get; set; }
    public virtual DbSet<Resource> Resources { get; set; }
    public virtual DbSet<ResourceComments> ResourcesComments { get; set; }
    public virtual DbSet<ResourceType> ResourceTypes { get; set; }
    public virtual DbSet<Score> Scores { get; set; }
    public virtual DbSet<ScoreUsers> ScoresUsers { get; set; }
    public virtual DbSet<Videogame> Videogames { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Comment>(Comment.Build);
        modelBuilder.Entity<File>(File.Build);
        modelBuilder.Entity<FileType>(FileType.Build);
        modelBuilder.Entity<LikeUsers>(LikeUsers.Build);
        modelBuilder.Entity<Resource>(Resource.Build);
        modelBuilder.Entity<ResourceComments>(ResourceComments.Build);
        modelBuilder.Entity<ResourceType>(ResourceType.Build);
        modelBuilder.Entity<Score>(Score.Build);
        modelBuilder.Entity<ScoreUsers>(ScoreUsers.Build);
        modelBuilder.Entity<Videogame>(Videogame.Build);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
