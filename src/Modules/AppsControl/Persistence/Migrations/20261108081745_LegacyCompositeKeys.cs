using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LegacyCompositeKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(LegacyKeysDataFix.BeforeUp);

            migrationBuilder.DropIndex(
                name: "ix_sys_roles_code",
                schema: "apps",
                table: "sys_roles");

            migrationBuilder.DropIndex(
                name: "ix_sys_menu_groups_app_id",
                schema: "apps",
                table: "sys_menu_groups");

            migrationBuilder.DropIndex(
                name: "ix_sys_menu_groups_code",
                schema: "apps",
                table: "sys_menu_groups");

            migrationBuilder.DropIndex(
                name: "ix_sys_menu_app_id",
                schema: "apps",
                table: "sys_menu");

            migrationBuilder.DropIndex(
                name: "ix_sys_menu_code",
                schema: "apps",
                table: "sys_menu");

            migrationBuilder.DropIndex(
                name: "ix_sys_company_profile_code",
                schema: "apps",
                table: "sys_company_profile");

            migrationBuilder.DropIndex(
                name: "ix_dd_domain_class_id_code",
                schema: "apps",
                table: "dd_domain");

            migrationBuilder.AddColumn<string>(
                name: "cmp_num",
                schema: "apps",
                table: "sys_roles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "instid",
                schema: "apps",
                table: "sys_company_profile",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cmp_num",
                schema: "apps",
                table: "dd_domain",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.Sql(LegacyKeysDataFix.Up);

            migrationBuilder.DropColumn(
                name: "cmp_nums",
                schema: "apps",
                table: "dd_domain");

            migrationBuilder.CreateIndex(
                name: "ix_sys_roles_code",
                schema: "apps",
                table: "sys_roles",
                column: "role_code",
                unique: true,
                filter: "cmp_num IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sys_roles_entity_code_company_code_code",
                schema: "apps",
                table: "sys_roles",
                columns: new[] { "instid", "cmp_num", "role_code" },
                unique: true,
                filter: "cmp_num IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_sys_menu_groups_app_id_business_app_code",
                schema: "apps",
                table: "sys_menu_groups",
                columns: new[] { "app_id", "business_app", "menu_group_code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_sys_menu_app_id_business_app_code",
                schema: "apps",
                table: "sys_menu",
                columns: new[] { "app_id", "business_app", "menu_code" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_sys_company_profile_entity_code_code",
                schema: "apps",
                table: "sys_company_profile",
                columns: new[] { "instid", "cmp_num" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dd_domain_class_id_code",
                schema: "apps",
                table: "dd_domain",
                columns: new[] { "class_id", "value" },
                unique: true,
                filter: "cmp_num IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_dd_domain_class_id_entity_code_company_code_code",
                schema: "apps",
                table: "dd_domain",
                columns: new[] { "class_id", "instid", "cmp_num", "value" },
                unique: true,
                filter: "cmp_num IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_sys_roles_code",
                schema: "apps",
                table: "sys_roles");

            migrationBuilder.DropIndex(
                name: "ix_sys_roles_entity_code_company_code_code",
                schema: "apps",
                table: "sys_roles");

            migrationBuilder.DropIndex(
                name: "ix_sys_menu_groups_app_id_business_app_code",
                schema: "apps",
                table: "sys_menu_groups");

            migrationBuilder.DropIndex(
                name: "ix_sys_menu_app_id_business_app_code",
                schema: "apps",
                table: "sys_menu");

            migrationBuilder.DropIndex(
                name: "ix_sys_company_profile_entity_code_code",
                schema: "apps",
                table: "sys_company_profile");

            migrationBuilder.DropIndex(
                name: "ix_dd_domain_class_id_code",
                schema: "apps",
                table: "dd_domain");

            migrationBuilder.DropIndex(
                name: "ix_dd_domain_class_id_entity_code_company_code_code",
                schema: "apps",
                table: "dd_domain");

            migrationBuilder.AddColumn<List<string>>(
                name: "cmp_nums",
                schema: "apps",
                table: "dd_domain",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.Sql(LegacyKeysDataFix.Down);

            migrationBuilder.DropColumn(
                name: "cmp_num",
                schema: "apps",
                table: "sys_roles");

            migrationBuilder.DropColumn(
                name: "cmp_num",
                schema: "apps",
                table: "dd_domain");

            migrationBuilder.AlterColumn<string>(
                name: "instid",
                schema: "apps",
                table: "sys_company_profile",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);


            migrationBuilder.CreateIndex(
                name: "ix_sys_roles_code",
                schema: "apps",
                table: "sys_roles",
                column: "role_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_menu_groups_app_id",
                schema: "apps",
                table: "sys_menu_groups",
                column: "app_id");

            migrationBuilder.CreateIndex(
                name: "ix_sys_menu_groups_code",
                schema: "apps",
                table: "sys_menu_groups",
                column: "menu_group_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_menu_app_id",
                schema: "apps",
                table: "sys_menu",
                column: "app_id");

            migrationBuilder.CreateIndex(
                name: "ix_sys_menu_code",
                schema: "apps",
                table: "sys_menu",
                column: "menu_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sys_company_profile_code",
                schema: "apps",
                table: "sys_company_profile",
                column: "cmp_num",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dd_domain_class_id_code",
                schema: "apps",
                table: "dd_domain",
                columns: new[] { "class_id", "value" },
                unique: true);
        }
    }
}
