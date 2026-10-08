using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class UserInfo
{
    public static void Build(EntityTypeBuilder<UserInfo> entity)
    {
        entity.ToTable("user_info");

        entity.HasKey(p => p.Id)
            .HasName("PK_User_Info");
        entity.Property(e => e.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        entity.Property(e => e.UsernameColor)
            .HasColumnType("nvarchar(7)")
            .HasColumnName("username_color");

        entity.Property(e => e.Title)
            .HasColumnType("nvarchar(100)")
            .HasColumnName("title");

        entity.Property(e => e.Birthday)
            .HasColumnName("birthday");

        entity.Property(e => e.Bio)
            .HasColumnType("nvarchar(max)")
            .HasColumnName("bio");

        entity.Property(e => e.Patreon)
            .HasColumnType("nvarchar(100)")
            .HasColumnName("patreon");

        entity.Property(e => e.Paypal)
            .HasColumnType("nvarchar(100)")
            .HasColumnName("paypal");

        entity.Property(e => e.BuyMeACoffee)
            .HasColumnType("nvarchar(100)")
            .HasColumnName("buy_me_a_coffee");

        entity.Property(e => e.Discord)
            .HasColumnType("nvarchar(100)")
            .HasColumnName("discord");

        entity.Property(e => e.Twitter)
            .HasColumnType("nvarchar(100)")
            .HasColumnName("twitter");

        entity.Property(e => e.Facebook)
            .HasColumnType("nvarchar(100)")
            .HasColumnName("facebook");

        entity.Property(e => e.Youtube)
            .HasColumnType("nvarchar(100)")
            .HasColumnName("youtube");

        entity.Property(e => e.Instagram)
            .HasColumnType("nvarchar(100)")
            .HasColumnName("instagram");

        entity.Property(e => e.Steam)
            .HasColumnType("nvarchar(100)")
            .HasColumnName("steam");

        entity.Property(e => e.Twitch)
            .HasColumnType("nvarchar(100)")
            .HasColumnName("twitch");

        entity.Property(e => e.Github)
            .HasColumnType("nvarchar(100)")
            .HasColumnName("github");

        entity.Property(e => e.NintendoNetwork)
            .HasColumnType("nvarchar(100)")
            .HasColumnName("nintendo_network");

        entity.Property(e => e.AvatarId)
            .HasColumnName("avatar_id");
        entity.HasOne(d => d.Avatar).WithOne(p => p.UserInfo)
            .HasForeignKey<UserInfo>(d => d.AvatarId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_user_info_file");

        entity.Property(e => e.EmailVisibilityId)
            .HasColumnName("email_visibility_id");
        entity.HasOne(d => d.EmailVisibility).WithMany(p => p.UserInfos)
            .HasForeignKey(d => d.EmailVisibilityId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_user_info_visibility");

        entity.Property(e => e.TimeZoneId)
            .HasColumnName("time_zone_id");
        entity.HasOne(d => d.TimeZone).WithMany(p => p.UserInfos)
            .HasForeignKey(d => d.TimeZoneId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_user_info_time_zone");

        entity.Property(e => e.UserId)
            .HasColumnType("nvarchar(450)")
            .HasColumnName("user_id");
        entity.HasOne(d => d.User).WithOne(p => p.UserInfo)
            .HasForeignKey<UserInfo>(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_user_info_user");
    }
}
