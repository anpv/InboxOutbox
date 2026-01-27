using System;
using InboxOutbox.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InboxOutbox.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        private readonly MonthlyRange _firstHalfOfYear2026 = new(FromYear: 2026, FromMonth: 1, NumberOfMonths: 6);
        
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inbox",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    topic = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    partition = table.Column<int>(type: "integer", nullable: false),
                    offset = table.Column<long>(type: "bigint", nullable: false),
                    key = table.Column<byte[]>(type: "bytea", nullable: true),
                    value = table.Column<byte[]>(type: "bytea", nullable: true),
                    headers = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<byte>(type: "smallint", nullable: false, defaultValue: (byte)0),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                })
                .Annotation("Ext:Npgsql:PartitionByRange:Key", "created_at");

            migrationBuilder.CreateTable(
                name: "measurement",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    value = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_measurement", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    topic = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    key = table.Column<byte[]>(type: "bytea", nullable: true),
                    value = table.Column<byte[]>(type: "bytea", nullable: true),
                    headers = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<byte>(type: "smallint", nullable: false, defaultValue: (byte)0),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                })
                .Annotation("Ext:Npgsql:PartitionByRange:Key", "created_at");

            migrationBuilder.CreateIndex(
                name: "ix__inbox__id",
                table: "inbox",
                columns: new[] { "id", "topic", "partition" },
                filter: "status in (0, 2)");

            migrationBuilder.CreateIndex(
                name: "ix__outbox__id",
                table: "outbox",
                column: "id",
                filter: "status in (0, 1)");
            
            migrationBuilder.CreateMonthlyPartitions("outbox", null, _firstHalfOfYear2026);
            migrationBuilder.CreateMonthlyPartitions("inbox", null, _firstHalfOfYear2026);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropMonthlyPartitions("inbox", null, _firstHalfOfYear2026);
            migrationBuilder.DropMonthlyPartitions("outbox", null, _firstHalfOfYear2026);
            
            migrationBuilder.DropTable(
                name: "inbox");

            migrationBuilder.DropTable(
                name: "measurement");

            migrationBuilder.DropTable(
                name: "outbox");
        }
    }
}
