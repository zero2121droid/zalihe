using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zalihe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddExternalOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExternalOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChannelId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalOrders_SalesChannels_ChannelId",
                        column: x => x.ChannelId,
                        principalTable: "SalesChannels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalOrders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ExternalOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalProductId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    OrderedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false),
                    AppliedQuantity = table.Column<decimal>(type: "numeric(18,3)", precision: 18, scale: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExternalOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExternalOrderLines_ExternalOrders_ExternalOrderId",
                        column: x => x.ExternalOrderId,
                        principalTable: "ExternalOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ExternalOrderLines_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExternalOrderLines_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrderLines_ExternalOrderId_ExternalProductId",
                table: "ExternalOrderLines",
                columns: new[] { "ExternalOrderId", "ExternalProductId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrderLines_ItemId",
                table: "ExternalOrderLines",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrderLines_TenantId",
                table: "ExternalOrderLines",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrders_ChannelId_ExternalId",
                table: "ExternalOrders",
                columns: new[] { "ChannelId", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ExternalOrders_TenantId",
                table: "ExternalOrders",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExternalOrderLines");

            migrationBuilder.DropTable(
                name: "ExternalOrders");
        }
    }
}
