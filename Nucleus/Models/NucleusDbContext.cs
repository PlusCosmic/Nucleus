using Microsoft.EntityFrameworkCore;

namespace Nucleus.Models;

public partial class NucleusDbContext : DbContext
{
    public NucleusDbContext()
    {
    }

    public NucleusDbContext(DbContextOptions<NucleusDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<DiscordUser> DiscordUsers { get; set; }

    public virtual DbSet<UserFrequentLink> UserFrequentLinks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DiscordUser>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("discord_user_pkey");

            entity.ToTable("discord_user");

            entity.HasIndex(e => e.DiscordId, "uq_discord_user_discord_id").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.DiscordId).HasColumnName("discord_id");
            entity.Property(e => e.Username).HasColumnName("username");
        });

        modelBuilder.Entity<UserFrequentLink>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("user_frequent_link_pkey");

            entity.ToTable("user_frequent_link");

            entity.HasIndex(e => e.UserId, "ix_user_frequent_link__user_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.ThumbnailUrl).HasColumnName("thumbnail_url");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.Url).HasColumnName("url");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.UserFrequentLinks)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_user_frequent_link__user");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
