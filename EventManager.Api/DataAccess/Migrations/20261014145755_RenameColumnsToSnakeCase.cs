using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EventManager.Api.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class RenameColumnsToSnakeCase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Title",
                table: "events",
                newName: "title");

            migrationBuilder.RenameColumn(
                name: "Description",
                table: "events",
                newName: "description");

            migrationBuilder.RenameColumn(
                name: "TotalSeats",
                table: "events",
                newName: "total_seats");

            migrationBuilder.RenameColumn(
                name: "StartAt",
                table: "events",
                newName: "start_at");

            migrationBuilder.RenameColumn(
                name: "EndAt",
                table: "events",
                newName: "end_at");

            migrationBuilder.RenameColumn(
                name: "AvailableSeats",
                table: "events",
                newName: "available_seats");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "bookings",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "bookings",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "ProcessedAt",
                table: "bookings",
                newName: "processed_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "bookings",
                newName: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "title",
                table: "events",
                newName: "Title");

            migrationBuilder.RenameColumn(
                name: "description",
                table: "events",
                newName: "Description");

            migrationBuilder.RenameColumn(
                name: "total_seats",
                table: "events",
                newName: "TotalSeats");

            migrationBuilder.RenameColumn(
                name: "start_at",
                table: "events",
                newName: "StartAt");

            migrationBuilder.RenameColumn(
                name: "end_at",
                table: "events",
                newName: "EndAt");

            migrationBuilder.RenameColumn(
                name: "available_seats",
                table: "events",
                newName: "AvailableSeats");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "bookings",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "bookings",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "processed_at",
                table: "bookings",
                newName: "ProcessedAt");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "bookings",
                newName: "CreatedAt");
        }
    }
}
