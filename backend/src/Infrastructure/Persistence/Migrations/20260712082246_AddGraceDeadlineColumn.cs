using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiKeyManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGraceDeadlineColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GraceDeadline",
                table: "ApiKeys",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GraceDeadline",
                table: "ApiKeys");
        }
    }
}
