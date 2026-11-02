using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DynamicCube.Modules.Payments.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BillEventSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_bill_event_bill_id_at",
                schema: "pay",
                table: "bill_event");

            migrationBuilder.AddColumn<long>(
                name: "sequence",
                schema: "pay",
                table: "bill_event",
                type: "bigint",
                nullable: false,
                defaultValue: 0L)
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn);

            migrationBuilder.CreateIndex(
                name: "ix_bill_event_bill_id_at_sequence",
                schema: "pay",
                table: "bill_event",
                columns: new[] { "bill_id", "at", "sequence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_bill_event_bill_id_at_sequence",
                schema: "pay",
                table: "bill_event");

            migrationBuilder.DropColumn(
                name: "sequence",
                schema: "pay",
                table: "bill_event");

            migrationBuilder.CreateIndex(
                name: "ix_bill_event_bill_id_at",
                schema: "pay",
                table: "bill_event",
                columns: new[] { "bill_id", "at" });
        }
    }
}
