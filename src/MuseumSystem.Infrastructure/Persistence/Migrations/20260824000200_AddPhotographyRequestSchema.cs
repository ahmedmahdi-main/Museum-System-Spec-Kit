using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseumSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotographyRequestSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_PhotographySets_PhotographySetId_ArtifactId_Purpose",
                schema: "museum",
                table: "PhotographySets",
                columns: new[] { "PhotographySetId", "ArtifactId", "Purpose" });

            migrationBuilder.CreateTable(
                name: "PhotographyRequests",
                schema: "museum",
                columns: table => new
                {
                    PhotographyRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RequestedByUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FulfillingPhotographySetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedByUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledByUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CancelledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotographyRequests", x => x.PhotographyRequestId);
                    table.CheckConstraint("CK_PhotographyRequests_CancelledMetadata", "(\"Status\" = 'Cancelled' AND \"CancelledByUserId\" IS NOT NULL AND \"CancelledAt\" IS NOT NULL AND \"FulfillingPhotographySetId\" IS NULL AND \"CompletedByUserId\" IS NULL AND \"CompletedAt\" IS NULL)\r\nOR (\"Status\" <> 'Cancelled' AND \"CancelledByUserId\" IS NULL AND \"CancelledAt\" IS NULL)");
                    table.CheckConstraint("CK_PhotographyRequests_CompletedMetadata", "(\"Status\" = 'Completed' AND \"FulfillingPhotographySetId\" IS NOT NULL AND \"CompletedByUserId\" IS NOT NULL AND \"CompletedAt\" IS NOT NULL AND \"CancelledByUserId\" IS NULL AND \"CancelledAt\" IS NULL)\r\nOR (\"Status\" <> 'Completed' AND \"FulfillingPhotographySetId\" IS NULL AND \"CompletedByUserId\" IS NULL AND \"CompletedAt\" IS NULL)");
                    table.CheckConstraint("CK_PhotographyRequests_Purpose", "\"Purpose\" IN ('GeneralDocumentation', 'PreMaintenance', 'DuringMaintenance', 'PostMaintenance')");
                    table.CheckConstraint("CK_PhotographyRequests_Status", "\"Status\" IN ('Pending', 'Completed', 'Cancelled')");
                    table.ForeignKey(
                        name: "FK_PhotographyRequests_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalSchema: "museum",
                        principalTable: "Artifacts",
                        principalColumn: "ArtifactId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhotographyRequests_PhotographySets_FulfillingPhotographySe~",
                        columns: x => new { x.FulfillingPhotographySetId, x.ArtifactId, x.Purpose },
                        principalSchema: "museum",
                        principalTable: "PhotographySets",
                        principalColumns: new[] { "PhotographySetId", "ArtifactId", "Purpose" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PhotographyRequests_ArtifactId",
                schema: "museum",
                table: "PhotographyRequests",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotographyRequests_ArtifactId_Status",
                schema: "museum",
                table: "PhotographyRequests",
                columns: new[] { "ArtifactId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotographyRequests_FulfillingPhotographySetId_ArtifactId_P~",
                schema: "museum",
                table: "PhotographyRequests",
                columns: new[] { "FulfillingPhotographySetId", "ArtifactId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotographyRequests_RequestedByUserId",
                schema: "museum",
                table: "PhotographyRequests",
                column: "RequestedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PhotographyRequests",
                schema: "museum");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PhotographySets_PhotographySetId_ArtifactId_Purpose",
                schema: "museum",
                table: "PhotographySets");
        }
    }
}
