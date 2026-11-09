using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationLikeLegacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "instcode",
                schema: "apps",
                table: "sys_instance_profile",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "scope_instid",
                schema: "apps",
                table: "sys_instance_profile",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cmp_abbreviation",
                schema: "apps",
                table: "sys_company_profile",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "default_bu",
                schema: "apps",
                table: "sys_company_profile",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "sys_userinstances",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    defaultinstflag = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_userinstances", x => x.id);
                    table.ForeignKey(
                        name: "fk_sys_userinstances_sys_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "apps",
                        principalTable: "sys_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sys_userinstances_companies",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    instid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cmp_num = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    default_cmp_flag = table.Column<bool>(type: "boolean", nullable: false),
                    default_currency = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sys_userinstances_companies", x => x.id);
                    table.ForeignKey(
                        name: "fk_sys_userinstances_companies_sys_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "apps",
                        principalTable: "sys_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_sys_userinstances_user_id_entity_code",
                schema: "apps",
                table: "sys_userinstances",
                columns: new[] { "user_id", "instid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_userinstances_companies_user_id_entity_code_company_code",
                schema: "apps",
                table: "sys_userinstances_companies",
                columns: new[] { "user_id", "instid", "cmp_num" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sys_userinstances",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "sys_userinstances_companies",
                schema: "apps");

            migrationBuilder.DropColumn(
                name: "instcode",
                schema: "apps",
                table: "sys_instance_profile");

            migrationBuilder.DropColumn(
                name: "scope_instid",
                schema: "apps",
                table: "sys_instance_profile");

            migrationBuilder.DropColumn(
                name: "cmp_abbreviation",
                schema: "apps",
                table: "sys_company_profile");

            migrationBuilder.DropColumn(
                name: "default_bu",
                schema: "apps",
                table: "sys_company_profile");
        }
    }
}
