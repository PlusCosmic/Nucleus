﻿using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;

namespace Nucleus.Repository;

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
    
    public virtual DbSet<Clip> Clips { get; set; }
    
    public virtual DbSet<ClipCollection> ClipCollections { get; set; }

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
        
        modelBuilder.Entity<Clip>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("clip_pkey");

            entity.ToTable("clip");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.OwnerId).HasColumnName("owner_id");
            entity.Property(e => e.VideoId).HasColumnName("video_id");
            entity.Property(e => e.CategoryEnum).HasColumnName("category");
        });

        modelBuilder.Entity<ClipCollection>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("clip_collection_pkey");

            entity.ToTable("clip_collection");
            
            entity.HasIndex(e => e.OwnerId, "ix_clip_collection__owner_id");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("id");
            entity.Property(e => e.CollectionId).HasColumnName("collection_id");
            entity.Property(e => e.OwnerId).HasColumnName("owner_id");
            entity.Property(e => e.CategoryEnum).HasColumnName("category");
            
            entity.HasOne<DiscordUser>().WithMany()
                .HasForeignKey(d => d.OwnerId)
                .HasConstraintName("fk_clip_collection__discord_user");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
