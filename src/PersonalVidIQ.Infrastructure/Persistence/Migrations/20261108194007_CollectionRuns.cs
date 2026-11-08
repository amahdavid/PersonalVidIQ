using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PersonalVidIQ.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CollectionRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "collection_runs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    started_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    trigger = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    forced = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    channels_refreshed = table.Column<int>(type: "integer", nullable: false),
                    videos_refreshed = table.Column<int>(type: "integer", nullable: false),
                    new_videos_tracked = table.Column<int>(type: "integer", nullable: false),
                    snapshots_saved = table.Column<int>(type: "integer", nullable: false),
                    units_used = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_collection_runs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_collection_runs_started_at_utc",
                table: "collection_runs",
                column: "started_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "collection_runs");
        }
    }
}
