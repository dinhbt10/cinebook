using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CineBook.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedVouchers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedVouchers",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromotionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SavedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedVouchers", x => new { x.UserId, x.PromotionId });
                    table.ForeignKey(
                        name: "FK_SavedVouchers_Promotions_PromotionId",
                        column: x => x.PromotionId,
                        principalTable: "Promotions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SavedVouchers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedVouchers_PromotionId",
                table: "SavedVouchers",
                column: "PromotionId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedVouchers_UserId_SavedAtUtc",
                table: "SavedVouchers",
                columns: new[] { "UserId", "SavedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedVouchers");
        }
    }
}
