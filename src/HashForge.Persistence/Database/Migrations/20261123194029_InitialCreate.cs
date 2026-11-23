using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HashForge.Persistence.Database.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Blocks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PoolName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Scheme = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Height = table.Column<int>(type: "integer", nullable: false),
                    Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    WorkerAddress = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    WorkerName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RewardSatoshis = table.Column<long>(type: "bigint", nullable: false),
                    EffortPercent = table.Column<double>(type: "double precision", nullable: false),
                    FoundAt = table.Column<long>(type: "bigint", nullable: false),
                    Accepted = table.Column<bool>(type: "boolean", nullable: false),
                    RejectReason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Blocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PoolHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PoolName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Timestamp = table.Column<long>(type: "bigint", nullable: false),
                    Hashrate = table.Column<double>(type: "double precision", nullable: false),
                    Miners = table.Column<int>(type: "integer", nullable: false),
                    Workers = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PoolHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pools",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Scheme = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Port = table.Column<int>(type: "integer", nullable: false),
                    FeePercent = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    FeeAddress = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PplnsWindowMultiplier = table.Column<double>(type: "double precision", nullable: false),
                    MaxCoinbaseOutputs = table.Column<int>(type: "integer", nullable: false),
                    MinimumPayoutSatoshis = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pools", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "BlockPayoutEntity",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BlockId = table.Column<long>(type: "bigint", nullable: false),
                    Address = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Satoshis = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockPayoutEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BlockPayoutEntity_Blocks_BlockId",
                        column: x => x.BlockId,
                        principalTable: "Blocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlockPayoutEntity_BlockId",
                table: "BlockPayoutEntity",
                column: "BlockId");

            migrationBuilder.CreateIndex(
                name: "IX_Blocks_FoundAt",
                table: "Blocks",
                column: "FoundAt");

            migrationBuilder.CreateIndex(
                name: "IX_PoolHistory_PoolName_Timestamp",
                table: "PoolHistory",
                columns: new[] { "PoolName", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockPayoutEntity");

            migrationBuilder.DropTable(
                name: "PoolHistory");

            migrationBuilder.DropTable(
                name: "Pools");

            migrationBuilder.DropTable(
                name: "Blocks");
        }
    }
}
