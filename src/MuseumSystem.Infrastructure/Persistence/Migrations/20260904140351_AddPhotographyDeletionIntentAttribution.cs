using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseumSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotographyDeletionIntentAttribution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletionRequestedAt",
                schema: "museum",
                table: "ArtifactImages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletionRequestedByUserId",
                schema: "museum",
                table: "ArtifactImages",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeletionRequestedAt",
                schema: "museum",
                table: "ArtifactImages");

            migrationBuilder.DropColumn(
                name: "DeletionRequestedByUserId",
                schema: "museum",
                table: "ArtifactImages");
        }
    }
}
