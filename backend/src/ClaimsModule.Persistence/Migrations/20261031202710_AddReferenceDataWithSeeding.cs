using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ClaimsModule.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceDataWithSeeding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CauseOfLossCodes",
                columns: table => new
                {
                    CauseOfLossCodeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PerilCategory = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CauseOfLossCodes", x => x.CauseOfLossCodeId);
                    table.UniqueConstraint("AK_CauseOfLossCodes_Code", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Policies",
                columns: table => new
                {
                    PolicyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PolicyNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpirationDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CoverageTypes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Policies", x => x.PolicyId);
                });

            migrationBuilder.InsertData(
                table: "CauseOfLossCodes",
                columns: new[] { "CauseOfLossCodeId", "Code", "IsActive", "Name", "PerilCategory", "SortOrder" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0001-000000000001"), "COL-FIRE", true, "Fire", "Property", 1 },
                    { new Guid("00000000-0000-0000-0001-000000000002"), "COL-FLOOD", true, "Flood", "Weather", 2 },
                    { new Guid("00000000-0000-0000-0001-000000000003"), "COL-THEFT", true, "Theft", "Crime", 3 },
                    { new Guid("00000000-0000-0000-0001-000000000004"), "COL-VEH-COL", true, "Vehicle Collision", "Auto", 4 },
                    { new Guid("00000000-0000-0000-0001-000000000005"), "COL-VEH-COMP", true, "Vehicle Comprehensive", "Auto", 5 },
                    { new Guid("00000000-0000-0000-0001-000000000006"), "COL-LIAB", true, "Third Party Liability", "Liability", 6 },
                    { new Guid("00000000-0000-0000-0001-000000000007"), "COL-EQUIP", true, "Equipment Breakdown", "Equipment", 7 },
                    { new Guid("00000000-0000-0000-0001-000000000008"), "COL-WIND", true, "Wind / Storm", "Weather", 8 },
                    { new Guid("00000000-0000-0000-0001-000000000009"), "COL-INJURY", true, "Bodily Injury", "Liability", 9 },
                    { new Guid("00000000-0000-0000-0001-000000000010"), "COL-OTHER", true, "Other / Unknown", "General", 10 }
                });

            migrationBuilder.InsertData(
                table: "Policies",
                columns: new[] { "PolicyId", "ClientName", "CoverageTypes", "EffectiveDate", "ExpirationDate", "PolicyNumber", "Status" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0002-000000000001"), "Meridian Transport LLC", "Vehicle,Cargo", new DateOnly(2024, 1, 1), new DateOnly(2026, 12, 31), "POL-2024-001001", "Active" },
                    { new Guid("00000000-0000-0000-0002-000000000002"), "Harborview Properties Inc", "Property,Liability", new DateOnly(2024, 6, 1), new DateOnly(2026, 5, 31), "POL-2024-001002", "Active" },
                    { new Guid("00000000-0000-0000-0002-000000000003"), "Coastal Builders Group", "Property,Equipment", new DateOnly(2025, 3, 1), new DateOnly(2027, 2, 28), "POL-2025-002001", "Active" },
                    { new Guid("00000000-0000-0000-0002-000000000004"), "Stanton Medical Group", "Liability,Vehicle", new DateOnly(2025, 1, 1), new DateOnly(2026, 12, 31), "POL-2025-002002", "Active" },
                    { new Guid("00000000-0000-0000-0002-000000000005"), "Archived Corp", "Property", new DateOnly(2020, 1, 1), new DateOnly(2021, 12, 31), "POL-2023-000099", "Expired" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LossEvents_CauseOfLossCode",
                table: "LossEvents",
                column: "CauseOfLossCode");

            migrationBuilder.CreateIndex(
                name: "IX_CauseOfLossCodes_Code",
                table: "CauseOfLossCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Policies_PolicyNumber",
                table: "Policies",
                column: "PolicyNumber",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LossEvents_CauseOfLossCodes_CauseOfLossCode",
                table: "LossEvents",
                column: "CauseOfLossCode",
                principalTable: "CauseOfLossCodes",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LossEvents_CauseOfLossCodes_CauseOfLossCode",
                table: "LossEvents");

            migrationBuilder.DropTable(
                name: "CauseOfLossCodes");

            migrationBuilder.DropTable(
                name: "Policies");

            migrationBuilder.DropIndex(
                name: "IX_LossEvents_CauseOfLossCode",
                table: "LossEvents");
        }
    }
}
