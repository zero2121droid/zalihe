using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Zalihe.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Before roles every user saw and could do everything (SPEC 2.7), and invitations promised
            // the same; they all start as admins and the owner narrows them down.
            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "Invitations",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Admin");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "AspNetUsers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Admin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Role",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "AspNetUsers");
        }
    }
}
