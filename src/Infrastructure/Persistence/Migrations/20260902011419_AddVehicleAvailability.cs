using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVehicleAvailability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryAttempts_Shipments_ShipmentId",
                table: "DeliveryAttempts");

            migrationBuilder.AddColumn<bool>(
                name: "Active",
                table: "Vehicles",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "ArrivedAtDestination",
                table: "Shipments",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<Guid>(
                name: "ShipmentId",
                table: "DeliveryAttempts",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryAttempts_Shipments_ShipmentId",
                table: "DeliveryAttempts",
                column: "ShipmentId",
                principalTable: "Shipments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DeliveryAttempts_Shipments_ShipmentId",
                table: "DeliveryAttempts");

            migrationBuilder.DropColumn(
                name: "Active",
                table: "Vehicles");

            migrationBuilder.DropColumn(
                name: "ArrivedAtDestination",
                table: "Shipments");

            migrationBuilder.AlterColumn<Guid>(
                name: "ShipmentId",
                table: "DeliveryAttempts",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddForeignKey(
                name: "FK_DeliveryAttempts_Shipments_ShipmentId",
                table: "DeliveryAttempts",
                column: "ShipmentId",
                principalTable: "Shipments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
