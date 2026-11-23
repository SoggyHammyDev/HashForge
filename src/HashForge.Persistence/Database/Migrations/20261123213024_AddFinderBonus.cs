using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HashForge.Persistence.Database.Migrations
{
    /// <inheritdoc />
    public partial class AddFinderBonus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FinderBonusSatoshis",
                table: "Pools",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinderBonusSatoshis",
                table: "Pools");
        }
    }
}
