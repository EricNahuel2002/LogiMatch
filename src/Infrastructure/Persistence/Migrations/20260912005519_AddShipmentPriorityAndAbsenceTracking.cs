using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentPriorityAndAbsenceTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AbsentDeliveriesCount",
                table: "Users",
                type: "int",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SucceededDeliveriesCount",
                table: "Users",
                type: "int",
                nullable: true,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Priority",
                table: "Shipments",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "Normal");

            migrationBuilder.AddColumn<string>(
                name: "FailureReason",
                table: "DeliveryAttempts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbsentDeliveriesCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SucceededDeliveriesCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Priority",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "FailureReason",
                table: "DeliveryAttempts");
        }
    }
}
