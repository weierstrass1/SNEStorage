using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class TeamUsers
{
    public static void Build(EntityTypeBuilder<TeamUsers> entity)
    {
        entity.ToTable("team_users");

        entity.HasKey(p => p.Id)
            .HasName("PK_Team_Users");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.UserId)
            .HasColumnType("nvarchar(450)")
            .HasColumnName("user_id");
        entity.HasOne(d => d.User).WithMany(p => p.TeamsUsers)
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_team_users_user");

        entity.Property(e => e.TeamId)
            .HasColumnName("team_id");
        entity.HasOne(d => d.Team).WithMany(p => p.TeamsUsers)
            .HasForeignKey(d => d.TeamId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_team_users_team");
    }
}
