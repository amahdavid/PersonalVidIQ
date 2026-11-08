using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PersonalVidIQ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "channels",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    youtube_channel_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    custom_url = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    thumbnail_url = table.Column<string>(type: "text", nullable: true),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    country = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    uploads_playlist_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    is_tracked = table.Column<bool>(type: "boolean", nullable: false),
                    tracked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_uploads_check_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_refreshed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_channels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "quota_usage",
                columns: table => new
                {
                    quota_day_start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    calls = table.Column<int>(type: "integer", nullable: false),
                    units = table.Column<int>(type: "integer", nullable: false),
                    last_call_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quota_usage", x => new { x.quota_day_start_utc, x.endpoint });
                });

            migrationBuilder.CreateTable(
                name: "search_cache",
                columns: table => new
                {
                    key = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    result_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_search_cache", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "channel_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    channel_id = table.Column<long>(type: "bigint", nullable: false),
                    subscriber_count = table.Column<long>(type: "bigint", nullable: true),
                    subscriber_count_hidden = table.Column<bool>(type: "boolean", nullable: false),
                    view_count = table.Column<long>(type: "bigint", nullable: true),
                    video_count = table.Column<long>(type: "bigint", nullable: true),
                    captured_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_channel_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_channel_snapshots_channels_channel_id",
                        column: x => x.channel_id,
                        principalTable: "channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "videos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    youtube_video_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    channel_id = table.Column<long>(type: "bigint", nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    published_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    thumbnail_url = table.Column<string>(type: "text", nullable: true),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    category_id = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    live_broadcast_content = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    is_tracked = table.Column<bool>(type: "boolean", nullable: false),
                    tracking_source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    tracked_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_refreshed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_videos", x => x.id);
                    table.ForeignKey(
                        name: "fk_videos_channels_channel_id",
                        column: x => x.channel_id,
                        principalTable: "channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "video_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    video_id = table.Column<long>(type: "bigint", nullable: false),
                    view_count = table.Column<long>(type: "bigint", nullable: true),
                    like_count = table.Column<long>(type: "bigint", nullable: true),
                    comment_count = table.Column<long>(type: "bigint", nullable: true),
                    captured_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    data_source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_video_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_video_snapshots_videos_video_id",
                        column: x => x.video_id,
                        principalTable: "videos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_channel_snapshots_captured_at_utc",
                table: "channel_snapshots",
                column: "captured_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_channel_snapshots_channel_id_captured_at_utc",
                table: "channel_snapshots",
                columns: new[] { "channel_id", "captured_at_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_channels_is_tracked",
                table: "channels",
                column: "is_tracked");

            migrationBuilder.CreateIndex(
                name: "ix_channels_youtube_channel_id",
                table: "channels",
                column: "youtube_channel_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_search_cache_expires_at_utc",
                table: "search_cache",
                column: "expires_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_video_snapshots_captured_at_utc",
                table: "video_snapshots",
                column: "captured_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_video_snapshots_video_id_captured_at_utc",
                table: "video_snapshots",
                columns: new[] { "video_id", "captured_at_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_videos_channel_id",
                table: "videos",
                column: "channel_id");

            migrationBuilder.CreateIndex(
                name: "ix_videos_is_tracked",
                table: "videos",
                column: "is_tracked");

            migrationBuilder.CreateIndex(
                name: "ix_videos_published_at_utc",
                table: "videos",
                column: "published_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_videos_youtube_video_id",
                table: "videos",
                column: "youtube_video_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_snapshots");

            migrationBuilder.DropTable(
                name: "quota_usage");

            migrationBuilder.DropTable(
                name: "search_cache");

            migrationBuilder.DropTable(
                name: "video_snapshots");

            migrationBuilder.DropTable(
                name: "videos");

            migrationBuilder.DropTable(
                name: "channels");
        }
    }
}
