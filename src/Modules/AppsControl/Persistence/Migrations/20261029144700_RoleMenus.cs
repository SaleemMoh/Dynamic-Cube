using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoleMenus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "role_menu",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    menu_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_menu", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_menu_menus_menu_id",
                        column: x => x.menu_id,
                        principalSchema: "apps",
                        principalTable: "menu",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_role_menu_role_role_id",
                        column: x => x.role_id,
                        principalSchema: "apps",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_role_menu_menu_id",
                schema: "apps",
                table: "role_menu",
                column: "menu_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_menu_role_id_menu_id",
                schema: "apps",
                table: "role_menu",
                columns: new[] { "role_id", "menu_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "role_menu",
                schema: "apps");
        }
    }
}
