using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseumSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotographyUploadRecoveryCorrelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PhotographyUploadFileOutcomeId",
                schema: "museum",
                table: "StorageOperationRecoveries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PhotographyUploadOperationId",
                schema: "museum",
                table: "StorageOperationRecoveries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperationRecoveries_PhotographyUploadFileOutcomeId",
                schema: "museum",
                table: "StorageOperationRecoveries",
                column: "PhotographyUploadFileOutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperationRecoveries_PhotographyUploadOperationId",
                schema: "museum",
                table: "StorageOperationRecoveries",
                column: "PhotographyUploadOperationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StorageOperationRecoveries_PhotographyUploadFileOutcomeId",
                schema: "museum",
                table: "StorageOperationRecoveries");

            migrationBuilder.DropIndex(
                name: "IX_StorageOperationRecoveries_PhotographyUploadOperationId",
                schema: "museum",
                table: "StorageOperationRecoveries");

            migrationBuilder.DropColumn(
                name: "PhotographyUploadFileOutcomeId",
                schema: "museum",
                table: "StorageOperationRecoveries");

            migrationBuilder.DropColumn(
                name: "PhotographyUploadOperationId",
                schema: "museum",
                table: "StorageOperationRecoveries");
        }
    }
}
