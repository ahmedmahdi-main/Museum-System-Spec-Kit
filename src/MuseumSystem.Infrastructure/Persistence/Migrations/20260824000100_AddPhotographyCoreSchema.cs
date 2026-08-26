using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseumSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotographyCoreSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PhotographySets",
                schema: "museum",
                columns: table => new
                {
                    PhotographySetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PhotographyDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PhotographerUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyToken = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotographySets", x => x.PhotographySetId);
                    table.UniqueConstraint("AK_PhotographySets_PhotographySetId_ArtifactId", x => new { x.PhotographySetId, x.ArtifactId });
                    table.CheckConstraint("CK_PhotographySets_Purpose", "\"Purpose\" IN ('GeneralDocumentation', 'PreMaintenance', 'DuringMaintenance', 'PostMaintenance')");
                    table.ForeignKey(
                        name: "FK_PhotographySets_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalSchema: "museum",
                        principalTable: "Artifacts",
                        principalColumn: "ArtifactId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArtifactImages",
                schema: "museum",
                columns: table => new
                {
                    ArtifactImageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotographySetId = table.Column<Guid>(type: "uuid", nullable: false),
                    OriginalObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OriginalFilename = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    PixelWidth = table.Column<int>(type: "integer", nullable: false),
                    PixelHeight = table.Column<int>(type: "integer", nullable: false),
                    UploadedByUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Caption = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DeletedByUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletionMode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    DeletionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ConcurrencyToken = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactImages", x => x.ArtifactImageId);
                    table.UniqueConstraint("AK_ArtifactImages_ArtifactImageId_ArtifactId", x => new { x.ArtifactImageId, x.ArtifactId });
                    table.CheckConstraint("CK_ArtifactImages_DeletionMode", "\"DeletionMode\" IS NULL OR \"DeletionMode\" IN ('UploaderGracePeriod', 'Privileged')");
                    table.CheckConstraint("CK_ArtifactImages_FileSizeBytes", "\"FileSizeBytes\" > 0");
                    table.CheckConstraint("CK_ArtifactImages_PixelHeight", "\"PixelHeight\" > 0");
                    table.CheckConstraint("CK_ArtifactImages_PixelWidth", "\"PixelWidth\" > 0");
                    table.CheckConstraint("CK_ArtifactImages_Status", "\"Status\" IN ('Available', 'DeletePending', 'Deleted')");
                    table.ForeignKey(
                        name: "FK_ArtifactImages_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalSchema: "museum",
                        principalTable: "Artifacts",
                        principalColumn: "ArtifactId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArtifactImages_PhotographySets_PhotographySetId_ArtifactId",
                        columns: x => new { x.PhotographySetId, x.ArtifactId },
                        principalSchema: "museum",
                        principalTable: "PhotographySets",
                        principalColumns: new[] { "PhotographySetId", "ArtifactId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhotographyUploadOperations",
                schema: "museum",
                columns: table => new
                {
                    PhotographyUploadOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OperationKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotographySetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotographyUploadOperations", x => x.PhotographyUploadOperationId);
                    table.CheckConstraint("CK_PhotographyUploadOperations_OperationKind", "\"OperationKind\" IN ('CreateSetUpload', 'AppendToSetUpload')");
                    table.CheckConstraint("CK_PhotographyUploadOperations_Status", "\"Status\" IN ('InProgress', 'Completed', 'CompletedWithFailures', 'Failed', 'RecoveryNeeded')");
                    table.ForeignKey(
                        name: "FK_PhotographyUploadOperations_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalSchema: "museum",
                        principalTable: "Artifacts",
                        principalColumn: "ArtifactId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhotographyUploadOperations_PhotographySets_PhotographySetI~",
                        columns: x => new { x.PhotographySetId, x.ArtifactId },
                        principalSchema: "museum",
                        principalTable: "PhotographySets",
                        principalColumns: new[] { "PhotographySetId", "ArtifactId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArtifactImageDerivatives",
                schema: "museum",
                columns: table => new
                {
                    ArtifactImageDerivativeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactImageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    PixelWidth = table.Column<int>(type: "integer", nullable: false),
                    PixelHeight = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactImageDerivatives", x => x.ArtifactImageDerivativeId);
                    table.CheckConstraint("CK_ArtifactImageDerivatives_FileSizeBytes", "\"FileSizeBytes\" > 0");
                    table.CheckConstraint("CK_ArtifactImageDerivatives_Kind", "\"Kind\" IN ('Thumbnail', 'Preview')");
                    table.CheckConstraint("CK_ArtifactImageDerivatives_PixelHeight", "\"PixelHeight\" > 0");
                    table.CheckConstraint("CK_ArtifactImageDerivatives_PixelWidth", "\"PixelWidth\" > 0");
                    table.ForeignKey(
                        name: "FK_ArtifactImageDerivatives_ArtifactImages_ArtifactImageId",
                        column: x => x.ArtifactImageId,
                        principalSchema: "museum",
                        principalTable: "ArtifactImages",
                        principalColumn: "ArtifactImageId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArtifactPhotographyStates",
                schema: "museum",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    PrimaryImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyToken = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactPhotographyStates", x => x.ArtifactId);
                    table.ForeignKey(
                        name: "FK_ArtifactPhotographyStates_ArtifactImages_PrimaryImageId_Art~",
                        columns: x => new { x.PrimaryImageId, x.ArtifactId },
                        principalSchema: "museum",
                        principalTable: "ArtifactImages",
                        principalColumns: new[] { "ArtifactImageId", "ArtifactId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArtifactPhotographyStates_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalSchema: "museum",
                        principalTable: "Artifacts",
                        principalColumn: "ArtifactId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StorageOperationRecoveries",
                schema: "museum",
                columns: table => new
                {
                    StorageOperationRecoveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    ObjectKeys = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FailureSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAttemptedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StorageOperationRecoveries", x => x.StorageOperationRecoveryId);
                    table.CheckConstraint("CK_StorageOperationRecoveries_OperationType", "\"OperationType\" IN ('UploadCleanup', 'DeleteCleanup', 'DerivativeCleanup', 'MissingObject', 'DerivativeGeneration')");
                    table.CheckConstraint("CK_StorageOperationRecoveries_Status", "\"Status\" IN ('Pending', 'Retrying', 'Resolved', 'FailedNeedsAttention')");
                    table.ForeignKey(
                        name: "FK_StorageOperationRecoveries_ArtifactImages_ArtifactImageId",
                        column: x => x.ArtifactImageId,
                        principalSchema: "museum",
                        principalTable: "ArtifactImages",
                        principalColumn: "ArtifactImageId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StorageOperationRecoveries_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalSchema: "museum",
                        principalTable: "Artifacts",
                        principalColumn: "ArtifactId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhotographyUploadFileOutcomes",
                schema: "museum",
                columns: table => new
                {
                    PhotographyUploadFileOutcomeId = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotographyUploadOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientFileOrdinal = table.Column<int>(type: "integer", nullable: false),
                    OriginalFilename = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    InputFingerprint = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ArtifactImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginalObjectKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    DerivativeObjectKeys = table.Column<string>(type: "jsonb", nullable: false),
                    StaffFacingMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FinalizedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotographyUploadFileOutcomes", x => x.PhotographyUploadFileOutcomeId);
                    table.CheckConstraint("CK_PhotographyUploadFileOutcomes_ClientFileOrdinal", "\"ClientFileOrdinal\" >= 0");
                    table.CheckConstraint("CK_PhotographyUploadFileOutcomes_Status", "\"Status\" IN ('Succeeded', 'Rejected', 'Failed', 'CleanupPending', 'RecoveryNeeded')");
                    table.ForeignKey(
                        name: "FK_PhotographyUploadFileOutcomes_ArtifactImages_ArtifactImageId",
                        column: x => x.ArtifactImageId,
                        principalSchema: "museum",
                        principalTable: "ArtifactImages",
                        principalColumn: "ArtifactImageId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhotographyUploadFileOutcomes_PhotographyUploadOperations_P~",
                        column: x => x.PhotographyUploadOperationId,
                        principalSchema: "museum",
                        principalTable: "PhotographyUploadOperations",
                        principalColumn: "PhotographyUploadOperationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactImageDerivatives_ArtifactImageId",
                schema: "museum",
                table: "ArtifactImageDerivatives",
                column: "ArtifactImageId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactImageDerivatives_ObjectKey",
                schema: "museum",
                table: "ArtifactImageDerivatives",
                column: "ObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactImages_ArtifactId",
                schema: "museum",
                table: "ArtifactImages",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactImages_OriginalObjectKey",
                schema: "museum",
                table: "ArtifactImages",
                column: "OriginalObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactImages_PhotographySetId_ArtifactId",
                schema: "museum",
                table: "ArtifactImages",
                columns: new[] { "PhotographySetId", "ArtifactId" });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPhotographyStates_PrimaryImageId",
                schema: "museum",
                table: "ArtifactPhotographyStates",
                column: "PrimaryImageId");

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactPhotographyStates_PrimaryImageId_ArtifactId",
                schema: "museum",
                table: "ArtifactPhotographyStates",
                columns: new[] { "PrimaryImageId", "ArtifactId" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotographySets_ArtifactId",
                schema: "museum",
                table: "PhotographySets",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotographyUploadFileOutcomes_ArtifactImageId",
                schema: "museum",
                table: "PhotographyUploadFileOutcomes",
                column: "ArtifactImageId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotographyUploadFileOutcomes_OriginalObjectKey",
                schema: "museum",
                table: "PhotographyUploadFileOutcomes",
                column: "OriginalObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhotographyUploadFileOutcomes_PhotographyUploadOperationId_~",
                schema: "museum",
                table: "PhotographyUploadFileOutcomes",
                columns: new[] { "PhotographyUploadOperationId", "ClientFileOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhotographyUploadOperations_ActorUserId_OperationKind_Idemp~",
                schema: "museum",
                table: "PhotographyUploadOperations",
                columns: new[] { "ActorUserId", "OperationKind", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhotographyUploadOperations_ArtifactId",
                schema: "museum",
                table: "PhotographyUploadOperations",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotographyUploadOperations_PhotographySetId_ArtifactId",
                schema: "museum",
                table: "PhotographyUploadOperations",
                columns: new[] { "PhotographySetId", "ArtifactId" });

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperationRecoveries_ArtifactId",
                schema: "museum",
                table: "StorageOperationRecoveries",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_StorageOperationRecoveries_ArtifactImageId",
                schema: "museum",
                table: "StorageOperationRecoveries",
                column: "ArtifactImageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtifactImageDerivatives",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "ArtifactPhotographyStates",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "PhotographyUploadFileOutcomes",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "StorageOperationRecoveries",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "PhotographyUploadOperations",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "ArtifactImages",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "PhotographySets",
                schema: "museum");
        }
    }
}
