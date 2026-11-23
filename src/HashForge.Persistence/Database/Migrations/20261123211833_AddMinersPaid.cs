using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HashForge.Persistence.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddMinersPaid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinersPaid",
                table: "Blocks",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinersPaid",
                table: "Blocks");
        }
    }
}
