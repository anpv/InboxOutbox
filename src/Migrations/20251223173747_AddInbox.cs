using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InboxOutbox.Migrations
{
    /// <inheritdoc />
    public partial class AddInbox : Migration
    {
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

            migrationBuilder.CreateIndex(
                name: "ix__inbox__id",
                table: "inbox",
                columns: new[] { "id", "topic", "partition" },
                filter: "status in (0, 2)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inbox");
        }
    }
}
