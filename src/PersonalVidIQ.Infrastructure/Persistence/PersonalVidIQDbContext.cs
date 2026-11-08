using Microsoft.EntityFrameworkCore;
using PersonalVidIQ.Domain.Entities;

namespace PersonalVidIQ.Infrastructure.Persistence;

public sealed class PersonalVidIQDbContext(DbContextOptions<PersonalVidIQDbContext> options) : DbContext(options)
{
    public DbSet<Channel> Channels => Set<Channel>();

    public DbSet<ChannelSnapshot> ChannelSnapshots => Set<ChannelSnapshot>();

    public DbSet<Video> Videos => Set<Video>();

    public DbSet<VideoSnapshot> VideoSnapshots => Set<VideoSnapshot>();

    public DbSet<QuotaUsageEntry> QuotaUsage => Set<QuotaUsageEntry>();

    public DbSet<SearchCacheEntry> SearchCache => Set<SearchCacheEntry>();

    public DbSet<CollectionRun> CollectionRuns => Set<CollectionRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Channel>(e =>
        {
            e.HasIndex(c => c.YouTubeChannelId).IsUnique().HasDatabaseName("ix_channels_youtube_channel_id");
            e.HasIndex(c => c.IsTracked);
            e.Property(c => c.YouTubeChannelId).HasMaxLength(64).HasColumnName("youtube_channel_id");
            e.Property(c => c.Title).HasMaxLength(500);
            e.Property(c => c.CustomUrl).HasMaxLength(200);
            e.Property(c => c.UploadsPlaylistId).HasMaxLength(64);
            e.Property(c => c.Country).HasMaxLength(8);
        });

        modelBuilder.Entity<ChannelSnapshot>(e =>
        {
            e.HasOne(s => s.Channel).WithMany(c => c.Snapshots).HasForeignKey(s => s.ChannelId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.ChannelId, s.CapturedAtUtc }).IsUnique();
            e.HasIndex(s => s.CapturedAtUtc);
            e.Property(s => s.DataSource).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<Video>(e =>
        {
            e.HasIndex(v => v.YouTubeVideoId).IsUnique().HasDatabaseName("ix_videos_youtube_video_id");
            e.HasIndex(v => v.IsTracked);
            e.HasIndex(v => v.PublishedAtUtc);
            e.HasOne(v => v.Channel).WithMany(c => c.Videos).HasForeignKey(v => v.ChannelId).OnDelete(DeleteBehavior.SetNull);
            e.Property(v => v.YouTubeVideoId).HasMaxLength(32).HasColumnName("youtube_video_id");
            e.Property(v => v.Title).HasMaxLength(500);
            e.Property(v => v.CategoryId).HasMaxLength(16);
            e.Property(v => v.LiveBroadcastContent).HasMaxLength(16);
            e.Property(v => v.TrackingSource).HasConversion<string>().HasMaxLength(16);
        });

        modelBuilder.Entity<VideoSnapshot>(e =>
        {
            e.HasOne(s => s.Video).WithMany(v => v.Snapshots).HasForeignKey(s => s.VideoId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(s => new { s.VideoId, s.CapturedAtUtc }).IsUnique();
            e.HasIndex(s => s.CapturedAtUtc);
            e.Property(s => s.DataSource).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<QuotaUsageEntry>(e =>
        {
            e.HasKey(q => new { q.QuotaDayStartUtc, q.Endpoint });
            e.Property(q => q.Endpoint).HasMaxLength(64);
        });

        modelBuilder.Entity<SearchCacheEntry>(e =>
        {
            e.HasKey(s => s.Key);
            e.Property(s => s.Key).HasMaxLength(512);
            e.Property(s => s.ResultJson).HasColumnType("jsonb");
            e.HasIndex(s => s.ExpiresAtUtc);
        });

        modelBuilder.Entity<CollectionRun>(e =>
        {
            e.HasIndex(r => r.StartedAtUtc);
            e.Property(r => r.Trigger).HasMaxLength(16);
            e.Property(r => r.Status).HasMaxLength(16);
            e.Property(r => r.Error).HasMaxLength(2000);
        });
    }
}
