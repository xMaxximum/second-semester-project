using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class Init_Achievements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Achievements",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<long>(type: "INTEGER", nullable: false),
                    AchievementId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    EarnedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    WeekIsoYear = table.Column<int>(type: "INTEGER", nullable: false),
                    WeekIsoNumber = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Achievements", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Achievements_UserId_AchievementId_WeekIsoYear_WeekIsoNumber",
                table: "Achievements",
                columns: new[] { "UserId", "AchievementId", "WeekIsoYear", "WeekIsoNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Achievements");
        }
    }
}
