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
    public virtual DbSet<Flag> Flags { get; set; }
    public virtual DbSet<LikeUsers> LikesUsers { get; set; }
    public virtual DbSet<Reason> Reasons { get; set; }
    public virtual DbSet<Resource> Resources { get; set; }
    public virtual DbSet<ResourceAuthors> ResourcesAuthors { get; set; }
    public virtual DbSet<ResourceComments> ResourcesComments { get; set; }
    public virtual DbSet<ResourceExternalAuthor> ResourcesExternalAuthors { get; set; }
    public virtual DbSet<ResourceFlags> ResourcesFlags { get; set; }
    public virtual DbSet<ResourceTeams> ResourcesTeams { get; set; }
    public virtual DbSet<ResourceType> ResourceTypes { get; set; }
    public virtual DbSet<Score> Scores { get; set; }
    public virtual DbSet<ScoreUsers> ScoresUsers { get; set; }
    public virtual DbSet<Team> Teams { get; set; }
    public virtual DbSet<TeamUsers> TeamsUsers { get; set; }
    public virtual DbSet<TimeZone> TimeZones { get; set; }
    public virtual DbSet<UserInfo> UserInfos { get; set; }
    public virtual DbSet<Videogame> Videogames { get; set; }
    public virtual DbSet<Visibility> Visibilities { get; set; }
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Comment>(Comment.Build);
        modelBuilder.Entity<File>(File.Build);
        modelBuilder.Entity<FileType>(FileType.Build);
        modelBuilder.Entity<Flag>(Flag.Build);
        modelBuilder.Entity<LikeUsers>(LikeUsers.Build);
        modelBuilder.Entity<Reason>(Reason.Build);
        modelBuilder.Entity<Resource>(Resource.Build);
        modelBuilder.Entity<ResourceAuthors>(ResourceAuthors.Build);
        modelBuilder.Entity<ResourceComments>(ResourceComments.Build);
        modelBuilder.Entity<ResourceExternalAuthor>(ResourceExternalAuthor.Build);
        modelBuilder.Entity<ResourceFlags>(ResourceFlags.Build);
        modelBuilder.Entity<ResourceTeams>(ResourceTeams.Build);
        modelBuilder.Entity<ResourceType>(ResourceType.Build);
        modelBuilder.Entity<Score>(Score.Build);
        modelBuilder.Entity<ScoreUsers>(ScoreUsers.Build);
        modelBuilder.Entity<Team>(Team.Build);
        modelBuilder.Entity<TeamUsers>(TeamUsers.Build);
        modelBuilder.Entity<UserInfo>(UserInfo.Build);
        modelBuilder.Entity<TimeZone>(TimeZone.Build);
        modelBuilder.Entity<Videogame>(Videogame.Build);
        modelBuilder.Entity<Visibility>(Visibility.Build);

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
