using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MuseumSystem.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentationSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentationTemplates",
                schema: "museum",
                columns: table => new
                {
                    DocumentationTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactCategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentationTemplates", x => x.DocumentationTemplateId);
                    table.ForeignKey(
                        name: "FK_DocumentationTemplates_ArtifactCategories_ArtifactCategoryId",
                        column: x => x.ArtifactCategoryId,
                        principalSchema: "museum",
                        principalTable: "ArtifactCategories",
                        principalColumn: "CategoryId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentationTemplateVersions",
                schema: "museum",
                columns: table => new
                {
                    DocumentationTemplateVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentationTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ActivatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ActivatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    RetiredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RetiredBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    ConcurrencyToken = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentationTemplateVersions", x => x.DocumentationTemplateVersionId);
                    table.ForeignKey(
                        name: "FK_DocumentationTemplateVersions_DocumentationTemplates_Docume~",
                        column: x => x.DocumentationTemplateId,
                        principalSchema: "museum",
                        principalTable: "DocumentationTemplates",
                        principalColumn: "DocumentationTemplateId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentationRecords",
                schema: "museum",
                columns: table => new
                {
                    DocumentationRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentationTemplateVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Values = table.Column<string>(type: "jsonb", nullable: false),
                    CompletedBaselineValues = table.Column<string>(type: "jsonb", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    LastModifiedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastModifiedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyToken = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentationRecords", x => x.DocumentationRecordId);
                    table.ForeignKey(
                        name: "FK_DocumentationRecords_Artifacts_ArtifactId",
                        column: x => x.ArtifactId,
                        principalSchema: "museum",
                        principalTable: "Artifacts",
                        principalColumn: "ArtifactId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentationRecords_DocumentationTemplateVersions_Document~",
                        column: x => x.DocumentationTemplateVersionId,
                        principalSchema: "museum",
                        principalTable: "DocumentationTemplateVersions",
                        principalColumn: "DocumentationTemplateVersionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentationTemplateFields",
                schema: "museum",
                columns: table => new
                {
                    DocumentationTemplateFieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentationTemplateVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FieldKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Label = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FieldType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    Section = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    HelpText = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentationTemplateFields", x => x.DocumentationTemplateFieldId);
                    table.ForeignKey(
                        name: "FK_DocumentationTemplateFields_DocumentationTemplateVersions_D~",
                        column: x => x.DocumentationTemplateVersionId,
                        principalSchema: "museum",
                        principalTable: "DocumentationTemplateVersions",
                        principalColumn: "DocumentationTemplateVersionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentationRevisions",
                schema: "museum",
                columns: table => new
                {
                    DocumentationRevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentationRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    TemplateVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                    PreviousValues = table.Column<string>(type: "jsonb", nullable: false),
                    NewValues = table.Column<string>(type: "jsonb", nullable: false),
                    ChangeSummary = table.Column<string>(type: "jsonb", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentationRevisions", x => x.DocumentationRevisionId);
                    table.ForeignKey(
                        name: "FK_DocumentationRevisions_DocumentationRecords_DocumentationRe~",
                        column: x => x.DocumentationRecordId,
                        principalSchema: "museum",
                        principalTable: "DocumentationRecords",
                        principalColumn: "DocumentationRecordId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DocumentationRevisions_DocumentationTemplateVersions_Templa~",
                        column: x => x.TemplateVersionId,
                        principalSchema: "museum",
                        principalTable: "DocumentationTemplateVersions",
                        principalColumn: "DocumentationTemplateVersionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DocumentationTemplateFieldOptions",
                schema: "museum",
                columns: table => new
                {
                    DocumentationTemplateFieldOptionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DocumentationTemplateFieldId = table.Column<Guid>(type: "uuid", nullable: false),
                    OptionKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Label = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentationTemplateFieldOptions", x => x.DocumentationTemplateFieldOptionId);
                    table.ForeignKey(
                        name: "FK_DocumentationTemplateFieldOptions_DocumentationTemplateFiel~",
                        column: x => x.DocumentationTemplateFieldId,
                        principalSchema: "museum",
                        principalTable: "DocumentationTemplateFields",
                        principalColumn: "DocumentationTemplateFieldId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentationRecords_ArtifactId",
                schema: "museum",
                table: "DocumentationRecords",
                column: "ArtifactId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentationRecords_DocumentationTemplateVersionId",
                schema: "museum",
                table: "DocumentationRecords",
                column: "DocumentationTemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentationRevisions_DocumentationRecordId_RevisionNumber",
                schema: "museum",
                table: "DocumentationRevisions",
                columns: new[] { "DocumentationRecordId", "RevisionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentationRevisions_TemplateVersionId",
                schema: "museum",
                table: "DocumentationRevisions",
                column: "TemplateVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_DocumentationTemplateFieldOptions_DocumentationTemplateFiel~",
                schema: "museum",
                table: "DocumentationTemplateFieldOptions",
                columns: new[] { "DocumentationTemplateFieldId", "OptionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentationTemplateFields_DocumentationTemplateVersionId_~",
                schema: "museum",
                table: "DocumentationTemplateFields",
                columns: new[] { "DocumentationTemplateVersionId", "FieldKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentationTemplates_ArtifactCategoryId",
                schema: "museum",
                table: "DocumentationTemplates",
                column: "ArtifactCategoryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentationTemplateVersions_DocumentationTemplateId_Versi~",
                schema: "museum",
                table: "DocumentationTemplateVersions",
                columns: new[] { "DocumentationTemplateId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentationTemplateVersions_OneActivePerTemplate",
                schema: "museum",
                table: "DocumentationTemplateVersions",
                column: "DocumentationTemplateId",
                unique: true,
                filter: "\"Status\" = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentationRevisions",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "DocumentationTemplateFieldOptions",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "DocumentationRecords",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "DocumentationTemplateFields",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "DocumentationTemplateVersions",
                schema: "museum");

            migrationBuilder.DropTable(
                name: "DocumentationTemplates",
                schema: "museum");
        }
    }
}
