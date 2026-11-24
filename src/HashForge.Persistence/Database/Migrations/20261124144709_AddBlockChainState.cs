using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HashForge.Persistence.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockChainState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Confirmations",
                table: "Blocks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "Orphaned",
                table: "Blocks",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Confirmations",
                table: "Blocks");

            migrationBuilder.DropColumn(
                name: "Orphaned",
                table: "Blocks");
        }
    }
}
