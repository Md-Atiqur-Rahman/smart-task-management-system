using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        private static readonly string[] EmailDeliveryColumns = ["EmailStatus", "EmailNextAttemptAt"];

        private static readonly string[] RecipientCreatedColumns = ["RecipientId", "CreatedAt"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActorName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EmailStatus = table.Column<int>(type: "int", nullable: false),
                    EmailAttempts = table.Column<int>(type: "int", nullable: false),
                    EmailNextAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EmailClaimedUntil = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EmailSentAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    EmailLastError = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    EmailClaimToken = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_EmailStatus_EmailNextAttemptAt",
                table: "Notifications",
                columns: EmailDeliveryColumns);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientId_CreatedAt",
                table: "Notifications",
                columns: RecipientCreatedColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");
        }
    }
}
