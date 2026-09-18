using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDelayRiskMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "AverageTimeBetweenOrders",
                table: "Users",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DelayRiskPercentage",
                table: "Shipments",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AverageTimeBetweenOrders",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DelayRiskPercentage",
                table: "Shipments");
        }
    }
}
