using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiKeyManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddKeyHashUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys",
                column: "KeyHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApiKeys_KeyHash",
                table: "ApiKeys");
        }
    }
}
