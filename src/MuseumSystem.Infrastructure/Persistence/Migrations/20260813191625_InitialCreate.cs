using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MuseumSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "museum");

            migrationBuilder.CreateTable(
                name: "ArtifactCategories",
                schema: "museum",
                columns: table => new
                {
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryCode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    NameArabic = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtifactCategories", x => x.CategoryId);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                schema: "museum",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                schema: "museum",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AuditEntries",
                schema: "museum",
                columns: table => new
                {
                    AuditEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ActionName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ModuleName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Summary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ChangeSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEntries", x => x.AuditEntryId);
                });

            migrationBuilder.CreateTable(
                name: "ImportBatches",
                schema: "museum",
                columns: table => new
                {
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UploadedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ValidatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CommittedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CommittedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TotalRows = table.Column<int>(type: "integer", nullable: false),
                    AcceptedRows = table.Column<int>(type: "integer", nullable: false),
                    RejectedRows = table.Column<int>(type: "integer", nullable: false),
                    ConcurrencyToken = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.ImportBatchId);
                });

            migrationBuilder.CreateTable(
                name: "Locations",
                schema: "museum",
                columns: table => new
                {
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    NameArabic = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    LocationType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ParentLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Locations", x => x.LocationId);
                    table.ForeignKey(
                        name: "FK_Locations_Locations_ParentLocationId",
                        column: x => x.ParentLocationId,
                        principalSchema: "museum",
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                schema: "museum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "museum",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "museum",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "museum",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "museum",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "museum",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "museum",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "museum",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "museum",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "museum",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "museum",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImportRows",
                schema: "museum",
                columns: table => new
                {
                    ImportRowId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowNumber = table.Column<int>(type: "integer", nullable: false),
                    CategoryValue = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    ItemNumberValue = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    LocationValue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DescriptionValue = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ProposedCategoryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProposedLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProposedArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Issues = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportRows", x => x.ImportRowId);
                    table.ForeignKey(
                        name: "FK_ImportRows_ImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalSchema: "museum",
                        principalTable: "ImportBatches",
                        principalColumn: "ImportBatchId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Artifacts",
                schema: "museum",
                columns: table => new
                {
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemNumber = table.Column<int>(type: "integer", nullable: false),
                    MuseumNumberDisplay = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: false),
                    BasicDescription = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CurrentStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CurrentLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentHolderType = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CurrentHolderName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastKnownStorageLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedFromImportBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConcurrencyToken = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "text", nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artifacts", x => x.ArtifactId);
                    table.ForeignKey(
                        name: "FK_Artifacts_ArtifactCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "museum",
                        principalTable: "ArtifactCategories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Artifacts_Locations_CurrentLocationId",
                        column: x => x.CurrentLocationId,
                        principalSchema: "museum",
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Artifacts_Locations_LastKnownStorageLocationId",
                        column: x => x.LastKnownStorageLocationId,
                        principalSchema: "museum",
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationSessions",
                schema: "museum",
                columns: table => new
                {
                    ReconciliationSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    LocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationSessions", x => x.ReconciliationSessionId);
                    table.ForeignKey(
                        name: "FK_ReconciliationSessions_Locations_LocationId",
                        column: x => x.LocationId,
                        principalSchema: "museum",
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentedCorrections",
                schema: "museum",
                columns: table => new
                {
                    CorrectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrectionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PreviousValueSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    NewValueSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CorrectedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrectedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentedCorrections", x => x.CorrectionId);
                    table.ForeignKey(
                        name: "FK_DocumentedCorrections_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalSchema: "museum",
                        principalTable: "Artifacts",
                        principalColumn: "ArtifactId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MovementRecords",
                schema: "museum",
                columns: table => new
                {
                    MovementId = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    MovementGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RecipientName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Purpose = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ReturnLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementRecords", x => x.MovementId);
                    table.ForeignKey(
                        name: "FK_MovementRecords_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalSchema: "museum",
                        principalTable: "Artifacts",
                        principalColumn: "ArtifactId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovementRecords_Locations_ReturnLocationId",
                        column: x => x.ReturnLocationId,
                        principalSchema: "museum",
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationResults",
                schema: "museum",
                columns: table => new
                {
                    ReconciliationResultId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReconciliationSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    ObservedMuseumNumber = table.Column<string>(type: "character varying(96)", maxLength: 96, nullable: true),
                    ExpectedLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ObservedLocationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IssueDescription = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsConfirmed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationResults", x => x.ReconciliationResultId);
                    table.ForeignKey(
                        name: "FK_ReconciliationResults_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalSchema: "museum",
                        principalTable: "Artifacts",
                        principalColumn: "ArtifactId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReconciliationResults_Locations_ExpectedLocationId",
                        column: x => x.ExpectedLocationId,
                        principalSchema: "museum",
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReconciliationResults_Locations_ObservedLocationId",
                        column: x => x.ObservedLocationId,
                        principalSchema: "museum",
                        principalTable: "Locations",
                        principalColumn: "LocationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReconciliationResults_ReconciliationSessions_Reconciliation~",
                        column: x => x.ReconciliationSessionId,
                        principalSchema: "museum",
                        principalTable: "ReconciliationSessions",
                        principalColumn: "ReconciliationSessionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArtifactCategories_CategoryCode",
                schema: "museum",
                table: "ArtifactCategories",
                column: "CategoryCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_CategoryId_ItemNumber",
                schema: "museum",
                table: "Artifacts",
                columns: new[] { "CategoryId", "ItemNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_CurrentLocationId",
                schema: "museum",
                table: "Artifacts",
                column: "CurrentLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Artifacts_LastKnownStorageLocationId",
                schema: "museum",
                table: "Artifacts",
                column: "LastKnownStorageLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "museum",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "museum",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "museum",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "museum",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "museum",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "museum",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "museum",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_ModuleName_EntityName_EntityId",
                schema: "museum",
                table: "AuditEntries",
                columns: new[] { "ModuleName", "EntityName", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_OccurredAt",
                schema: "museum",
                table: "AuditEntries",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentedCorrections_ArtifactId",
                schema: "museum",
                table: "DocumentedCorrections",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportRows_ImportBatchId",
                schema: "museum",
                table: "ImportRows",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_Locations_NameArabic_LocationType",
                schema: "museum",
                table: "Locations",
                columns: new[] { "NameArabic", "LocationType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Locations_ParentLocationId",
                schema: "museum",
                table: "Locations",
                column: "ParentLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_MovementRecords_ArtifactId",
                schema: "museum",
                table: "MovementRecords",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_MovementRecords_MovementGroupId",
                schema: "museum",
                table: "MovementRecords",
                column: "MovementGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MovementRecords_ReturnLocationId",
                schema: "museum",
                table: "MovementRecords",
                column: "ReturnLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationResults_ArtifactId",
                schema: "museum",
                table: "ReconciliationResults",
                column: "ArtifactId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationResults_ExpectedLocationId",
                schema: "museum",
                table: "ReconciliationResults",
                column: "ExpectedLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationResults_ObservedLocationId",
                schema: "museum",
                table: "ReconciliationResults",
                column: "ObservedLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationResults_ReconciliationSessionId",
                schema: "museum",
                table: "ReconciliationResults",
                column: "ReconciliationSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationSessions_LocationId",
                schema: "museum",
                table: "ReconciliationSessions",
                column: "LocationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "AuditEntries",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "DocumentedCorrections",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "ImportRows",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "MovementRecords",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "ReconciliationResults",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "AspNetRoles",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "AspNetUsers",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "ImportBatches",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "Artifacts",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "ReconciliationSessions",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "ArtifactCategories",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "Locations",
                schema: "museum");
        }
    }
}
