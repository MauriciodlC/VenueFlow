using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VenueFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HostAccountsAndVenueDrafts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Every venue now needs an owning host. Databases created before this
            // migration only hold the synthetic demo seed, which has no owner, so
            // it is removed here (rooms cascade) and `seed` recreates it with one.
            // The table is then empty, so the NOT NULL columns need no default.
            migrationBuilder.Sql("DELETE FROM venues;");

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.CheckConstraint("ck_users_role", "role IN ('Guest', 'Host', 'Staff', 'Admin')");
                });

            migrationBuilder.AddColumn<Guid>(
                name: "host_id",
                table: "venues",
                type: "uuid",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "venues",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false);

            migrationBuilder.AddColumn<int>(
                name: "revision",
                table: "venues",
                type: "integer",
                nullable: false);

            migrationBuilder.AddColumn<string>(
                name: "description",
                table: "venues",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "venues",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "address_line",
                table: "venues",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "accessibility_notes",
                table: "venues",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_venues_status",
                table: "venues",
                sql: "status IN ('Draft', 'InReview', 'Published')");

            migrationBuilder.CreateIndex(
                name: "ux_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_venues_host_id",
                table: "venues",
                column: "host_id");

            migrationBuilder.AddForeignKey(
                name: "fk_venues_users_host_id",
                table: "venues",
                column: "host_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_venues_users_host_id",
                table: "venues");

            migrationBuilder.DropIndex(
                name: "ix_venues_host_id",
                table: "venues");

            migrationBuilder.DropCheckConstraint(
                name: "ck_venues_status",
                table: "venues");

            migrationBuilder.DropColumn(name: "accessibility_notes", table: "venues");
            migrationBuilder.DropColumn(name: "address_line", table: "venues");
            migrationBuilder.DropColumn(name: "city", table: "venues");
            migrationBuilder.DropColumn(name: "description", table: "venues");
            migrationBuilder.DropColumn(name: "revision", table: "venues");
            migrationBuilder.DropColumn(name: "status", table: "venues");
            migrationBuilder.DropColumn(name: "host_id", table: "venues");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
