using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zalihe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderListDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PlacedAt",
                table: "ExternalOrders",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // Orders stored before this didn't keep when they were placed; when they arrived is the closest known.
            migrationBuilder.Sql("""UPDATE "ExternalOrders" SET "PlacedAt" = "CreatedAt";""");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "ExternalOrderLines",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrders_TenantId_PlacedAt",
                table: "ExternalOrders",
                columns: new[] { "TenantId", "PlacedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExternalOrders_TenantId_PlacedAt",
                table: "ExternalOrders");

            migrationBuilder.DropColumn(
                name: "PlacedAt",
                table: "ExternalOrders");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "ExternalOrderLines");
        }
    }
}
