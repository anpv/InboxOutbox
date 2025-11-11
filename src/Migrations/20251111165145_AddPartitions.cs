using InboxOutbox.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InboxOutbox.Migrations
{
    /// <inheritdoc />
    public partial class AddPartitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateRangePartition("outbox_202511", null, "outbox", null, "'2025-10-01'", "'2025-11-01'");
            migrationBuilder.CreateRangePartition("outbox_202512", null, "outbox", null, "'2025-11-01'", "'2025-12-01'");
            migrationBuilder.CreateRangePartition("outbox_202601", null, "outbox", null, "'2025-12-01'", "'2026-01-01'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPartition("outbox_202601", null);
            migrationBuilder.DropPartition("outbox_202512", null);
            migrationBuilder.DropPartition("outbox_202511", null);
        }
    }
}
