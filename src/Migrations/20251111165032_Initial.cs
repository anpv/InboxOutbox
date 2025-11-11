using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace InboxOutbox.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "ix__outbox__id",
                table: "outbox",
                column: "id",
                filter: "status in (0, 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "outbox");
        }
    }
}
