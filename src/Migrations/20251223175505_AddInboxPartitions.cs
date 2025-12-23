using InboxOutbox.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InboxOutbox.Migrations
{
    /// <inheritdoc />
    public partial class AddInboxPartitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateRangePartition("inbox_202511", null, "inbox", null, "'2025-10-01'", "'2025-11-01'");
            migrationBuilder.CreateRangePartition("inbox_202512", null, "inbox", null, "'2025-11-01'", "'2025-12-01'");
            migrationBuilder.CreateRangePartition("inbox_202601", null, "inbox", null, "'2025-12-01'", "'2026-01-01'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPartition("inbox_202601", null);
            migrationBuilder.DropPartition("inbox_202512", null);
            migrationBuilder.DropPartition("inbox_202511", null);
        }
    }
}
