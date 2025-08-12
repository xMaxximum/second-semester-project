using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Server.Migrations
{
    /// <inheritdoc />
    public partial class UpdateSensorDataPacketAcceleration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AveragedAccelerationX",
                table: "SensorDataPackets");

            migrationBuilder.DropColumn(
                name: "AveragedAccelerationY",
                table: "SensorDataPackets");

            migrationBuilder.RenameColumn(
                name: "PeakAccelerationZ",
                table: "SensorDataPackets",
                newName: "ElevationGain");

            migrationBuilder.RenameColumn(
                name: "PeakAccelerationY",
                table: "SensorDataPackets",
                newName: "AccelerationZ");

            migrationBuilder.RenameColumn(
                name: "PeakAccelerationX",
                table: "SensorDataPackets",
                newName: "AccelerationY");

            migrationBuilder.RenameColumn(
                name: "AveragedAccelerationZ",
                table: "SensorDataPackets",
                newName: "AccelerationX");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ElevationGain",
                table: "SensorDataPackets",
                newName: "PeakAccelerationZ");

            migrationBuilder.RenameColumn(
                name: "AccelerationZ",
                table: "SensorDataPackets",
                newName: "PeakAccelerationY");

            migrationBuilder.RenameColumn(
                name: "AccelerationY",
                table: "SensorDataPackets",
                newName: "PeakAccelerationX");

            migrationBuilder.RenameColumn(
                name: "AccelerationX",
                table: "SensorDataPackets",
                newName: "AveragedAccelerationZ");

            migrationBuilder.AddColumn<double>(
                name: "AveragedAccelerationX",
                table: "SensorDataPackets",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "AveragedAccelerationY",
                table: "SensorDataPackets",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
