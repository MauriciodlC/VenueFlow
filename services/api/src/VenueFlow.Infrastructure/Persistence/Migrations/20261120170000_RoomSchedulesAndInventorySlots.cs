using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VenueFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoomSchedulesAndInventorySlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "inventory_slots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    total_capacity = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inventory_slots", x => x.id);
                    table.CheckConstraint("ck_inventory_slots_interval", "end_utc > start_utc");
                    table.CheckConstraint("ck_inventory_slots_total_capacity", "total_capacity >= 1");
                    table.ForeignKey(
                        name: "fk_inventory_slots_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "room_schedules",
                columns: table => new
                {
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_room_schedules", x => x.room_id);
                    table.ForeignKey(
                        name: "fk_room_schedules_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "opening_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weekday = table.Column<int>(type: "integer", nullable: false),
                    start_local = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_local = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    slot_minutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_opening_rules", x => x.id);
                    table.CheckConstraint("ck_opening_rules_slot_minutes", "slot_minutes BETWEEN 15 AND 720");
                    table.CheckConstraint("ck_opening_rules_weekday", "weekday BETWEEN 0 AND 6");
                    table.CheckConstraint("ck_opening_rules_window", "end_local > start_local");
                    table.ForeignKey(
                        name: "fk_opening_rules_room_schedules_room_id",
                        column: x => x.room_id,
                        principalTable: "room_schedules",
                        principalColumn: "room_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "schedule_exceptions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    start_local = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    end_local = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_schedule_exceptions", x => x.id);
                    table.CheckConstraint("ck_schedule_exceptions_capacity", "(kind = 'Closure' AND capacity IS NULL) OR (kind = 'CapacityLimit' AND capacity BETWEEN 1 AND 1000)");
                    table.CheckConstraint("ck_schedule_exceptions_kind", "kind IN ('Closure', 'CapacityLimit')");
                    table.CheckConstraint("ck_schedule_exceptions_window", "(start_local IS NULL AND end_local IS NULL) OR (start_local IS NOT NULL AND end_local IS NOT NULL AND end_local > start_local)");
                    table.ForeignKey(
                        name: "fk_schedule_exceptions_room_schedules_room_id",
                        column: x => x.room_id,
                        principalTable: "room_schedules",
                        principalColumn: "room_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_inventory_slots_room_id_start_utc",
                table: "inventory_slots",
                columns: new[] { "room_id", "start_utc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_opening_rules_room_id_weekday",
                table: "opening_rules",
                columns: new[] { "room_id", "weekday" });

            migrationBuilder.CreateIndex(
                name: "ix_schedule_exceptions_room_id_date",
                table: "schedule_exceptions",
                columns: new[] { "room_id", "date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "inventory_slots");

            migrationBuilder.DropTable(
                name: "opening_rules");

            migrationBuilder.DropTable(
                name: "schedule_exceptions");

            migrationBuilder.DropTable(
                name: "room_schedules");
        }
    }
}
