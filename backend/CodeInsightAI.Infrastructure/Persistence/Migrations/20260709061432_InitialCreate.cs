using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodeInsightAI.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PullRequestReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RepoOwner = table.Column<string>(type: "TEXT", nullable: false),
                    RepoName = table.Column<string>(type: "TEXT", nullable: false),
                    PrNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    PrTitle = table.Column<string>(type: "TEXT", nullable: false),
                    PrUrl = table.Column<string>(type: "TEXT", nullable: false),
                    Author = table.Column<string>(type: "TEXT", nullable: false),
                    BaseBranch = table.Column<string>(type: "TEXT", nullable: false),
                    HeadBranch = table.Column<string>(type: "TEXT", nullable: false),
                    HeadSha = table.Column<string>(type: "TEXT", nullable: false),
                    DetectedPurpose = table.Column<string>(type: "TEXT", nullable: false),
                    ReliabilityScore = table.Column<int>(type: "INTEGER", nullable: false),
                    Verdict = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Recommendations = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequestReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PullRequestReportIssues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FilePath = table.Column<string>(type: "TEXT", nullable: false),
                    LineNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    LineContent = table.Column<string>(type: "TEXT", nullable: false),
                    Severity = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Suggestion = table.Column<string>(type: "TEXT", nullable: false),
                    RefactoredCode = table.Column<string>(type: "TEXT", nullable: false),
                    PullRequestReportId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PullRequestReportIssues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PullRequestReportIssues_PullRequestReports_PullRequestReportId",
                        column: x => x.PullRequestReportId,
                        principalTable: "PullRequestReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestReportIssues_PullRequestReportId",
                table: "PullRequestReportIssues",
                column: "PullRequestReportId");

            migrationBuilder.CreateIndex(
                name: "IX_PullRequestReports_RepoOwner_RepoName_PrNumber",
                table: "PullRequestReports",
                columns: new[] { "RepoOwner", "RepoName", "PrNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PullRequestReportIssues");

            migrationBuilder.DropTable(
                name: "PullRequestReports");
        }
    }
}
