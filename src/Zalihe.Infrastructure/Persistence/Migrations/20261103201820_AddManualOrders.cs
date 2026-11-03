using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zalihe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddManualOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastManualOrderNumber",
                table: "Tenants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<Guid>(
                name: "ChannelId",
                table: "ExternalOrders",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "ExternalOrders",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "ExternalOrders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "ExternalOrders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                // Every order so far came from a shop.
                defaultValue: "WooCommerce");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrders_TenantId_ExternalId",
                table: "ExternalOrders",
                columns: new[] { "TenantId", "ExternalId" },
                unique: true,
                filter: "\"ChannelId\" IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ExternalOrders_TenantId_ExternalId",
                table: "ExternalOrders");

            migrationBuilder.DropColumn(
                name: "LastManualOrderNumber",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "ExternalOrders");

            migrationBuilder.DropColumn(
                name: "Reference",
                table: "ExternalOrders");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "ExternalOrders");

            migrationBuilder.AlterColumn<Guid>(
                name: "ChannelId",
                table: "ExternalOrders",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
