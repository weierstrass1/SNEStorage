using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace SNEStorage.Models;

public partial class ResourceTeams
{
    public static void Build(EntityTypeBuilder<ResourceTeams> entity)
    {
        entity.ToTable("resource_teams");

        entity.HasKey(p => p.Id)
            .HasName("PK_Resource_Teams");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.TeamId)
            .HasColumnName("team_id");
        entity.HasOne(d => d.Team).WithMany(p => p.ResourcesTeams)
            .HasForeignKey(d => d.TeamId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_teams_team");

        entity.Property(e => e.ResourceId)
            .HasColumnName("resource_id");
        entity.HasOne(d => d.Resource).WithMany(p => p.ResourcesTeams)
            .HasForeignKey(d => d.ResourceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_resource_teams_resource");
    }
}
