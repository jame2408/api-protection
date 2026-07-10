using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApiKeyManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRotationLinkColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PredecessorKeyId",
                table: "ApiKeys",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SuccessorKeyId",
                table: "ApiKeys",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PredecessorKeyId",
                table: "ApiKeys");

            migrationBuilder.DropColumn(
                name: "SuccessorKeyId",
                table: "ApiKeys");
        }
    }
}
