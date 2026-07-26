using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiKeyManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLockRuleIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LockRuleId",
                table: "ApiKeys",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LockRuleId",
                table: "ApiKeys");
        }
    }
}
