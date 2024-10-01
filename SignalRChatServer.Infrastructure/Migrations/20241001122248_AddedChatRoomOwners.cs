using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalRChatServer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddedChatRoomOwners : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Owner",
                table: "Groups",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Owner",
                table: "Groups");
        }
    }
}
