using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceAuthentication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Disable foreign key constraints temporarily
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);
            migrationBuilder.DropColumn(
                name: "DeviceType",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "HardwareVersion",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LastKnownBatteryLevel",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LastKnownTemperature",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "Settings",
                table: "Devices");

            migrationBuilder.RenameColumn(
                name: "LastKnownLocation",
                table: "Devices",
                newName: "ActivationCodeExpiry");

            migrationBuilder.AddColumn<string>(
                name: "ActivationCode",
                table: "Devices",
                type: "TEXT",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthToken",
                table: "Devices",
                type: "TEXT",
                maxLength: 128,
                nullable: true);

            migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivationCode",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "AuthToken",
                table: "Devices");

            migrationBuilder.RenameColumn(
                name: "ActivationCodeExpiry",
                table: "Devices",
                newName: "LastKnownLocation");

            migrationBuilder.AddColumn<string>(
                name: "DeviceType",
                table: "Devices",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HardwareVersion",
                table: "Devices",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastKnownBatteryLevel",
                table: "Devices",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LastKnownTemperature",
                table: "Devices",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "Devices",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "Settings",
                table: "Devices",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);
        }
    }
}
