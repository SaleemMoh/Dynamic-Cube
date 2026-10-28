using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.AppsControl.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAppsControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "apps");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:ltree", ",,");

            migrationBuilder.CreateTable(
                name: "app",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    app_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    base_url = table.Column<string>(type: "text", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "class",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description_ar = table.Column<string>(type: "text", nullable: true),
                    description_en = table.Column<string>(type: "text", nullable: true),
                    parent_class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_hierarchical = table.Column<bool>(type: "boolean", nullable: false),
                    allow_multi_assign = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class", x => x.id);
                    table.ForeignKey(
                        name: "fk_class_class_parent_class_id",
                        column: x => x.parent_class_id,
                        principalSchema: "apps",
                        principalTable: "class",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "data_object",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    db_schema = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    db_object = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    object_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    columns = table.Column<string>(type: "jsonb", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_object", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "entity",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_kind = table.Column<string>(type: "text", nullable: true),
                    unified_number = table.Column<string>(type: "text", nullable: true),
                    cr_number = table.Column<string>(type: "text", nullable: true),
                    logo_file_key = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_entity", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "installation",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    system_type = table.Column<string>(type: "text", nullable: false),
                    copyright = table.Column<string>(type: "text", nullable: true),
                    default_language = table.Column<string>(type: "text", nullable: false),
                    time_zone = table.Column<string>(type: "text", nullable: false),
                    weekend_days = table.Column<List<int>>(type: "integer[]", nullable: false),
                    calendar_display = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    logo_file_key = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_installation", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "org_unit_type",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    allowed_parent_types = table.Column<List<string>>(type: "text[]", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_unit_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "position",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_position", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_account",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    idp_subject = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    auth_source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    mobile = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    national_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    preferred_language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    last_login_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_account", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "menu_group",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    app_id = table.Column<Guid>(type: "uuid", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_menu_group", x => x.id);
                    table.ForeignKey(
                        name: "fk_menu_group_app_app_id",
                        column: x => x.app_id,
                        principalSchema: "apps",
                        principalTable: "app",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resource",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    app_id = table.Column<Guid>(type: "uuid", nullable: true),
                    resource_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    available_actions = table.Column<List<string>>(type: "text[]", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource", x => x.id);
                    table.ForeignKey(
                        name: "fk_resource_app_app_id",
                        column: x => x.app_id,
                        principalSchema: "apps",
                        principalTable: "app",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_resource_resource_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "apps",
                        principalTable: "resource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "role",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    app_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reference_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_app_app_id",
                        column: x => x.app_id,
                        principalSchema: "apps",
                        principalTable: "app",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "setting",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    app_id = table.Column<Guid>(type: "uuid", nullable: true),
                    data_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    value = table.Column<string>(type: "jsonb", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    is_secret = table.Column<bool>(type: "boolean", nullable: false),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_setting", x => x.id);
                    table.ForeignKey(
                        name: "fk_setting_app_app_id",
                        column: x => x.app_id,
                        principalSchema: "apps",
                        principalTable: "app",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ui_text",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    app_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    name_en = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ui_text", x => x.id);
                    table.ForeignKey(
                        name: "fk_ui_text_app_app_id",
                        column: x => x.app_id,
                        principalSchema: "apps",
                        principalTable: "app",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "class_attribute",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    data_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ref_class_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    attribute_group = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    validation = table.Column<string>(type: "jsonb", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_reportable = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_class_attribute", x => x.id);
                    table.ForeignKey(
                        name: "fk_class_attribute_class_class_id",
                        column: x => x.class_id,
                        principalSchema: "apps",
                        principalTable: "class",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "domain",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_domain_id = table.Column<Guid>(type: "uuid", nullable: true),
                    details_ar = table.Column<string>(type: "text", nullable: true),
                    details_en = table.Column<string>(type: "text", nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    attributes = table.Column<string>(type: "jsonb", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_domain", x => x.id);
                    table.ForeignKey(
                        name: "fk_domain_class_class_id",
                        column: x => x.class_id,
                        principalSchema: "apps",
                        principalTable: "class",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_domain_domain_parent_domain_id",
                        column: x => x.parent_domain_id,
                        principalSchema: "apps",
                        principalTable: "domain",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lov",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    class_id = table.Column<Guid>(type: "uuid", nullable: true),
                    data_object_id = table.Column<Guid>(type: "uuid", nullable: true),
                    integration_operation_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sql_text = table.Column<string>(type: "text", nullable: true),
                    value_column = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: true),
                    display_ar_column = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: true),
                    display_en_column = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: true),
                    parent_column = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: true),
                    static_filter = table.Column<string>(type: "jsonb", nullable: true),
                    default_selection_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    search_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    max_rows = table.Column<int>(type: "integer", nullable: false),
                    requires_grant = table.Column<bool>(type: "boolean", nullable: false),
                    is_public = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lov", x => x.id);
                    table.ForeignKey(
                        name: "fk_lov_class_class_id",
                        column: x => x.class_id,
                        principalSchema: "apps",
                        principalTable: "class",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lov_data_object_data_object_id",
                        column: x => x.data_object_id,
                        principalSchema: "apps",
                        principalTable: "data_object",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "org_unit",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_unit_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    path = table.Column<string>(type: "ltree", nullable: false),
                    attributes = table.Column<string>(type: "jsonb", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_unit", x => x.id);
                    table.ForeignKey(
                        name: "fk_org_unit_org_unit_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "apps",
                        principalTable: "org_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_org_unit_org_unit_type_org_unit_type_id",
                        column: x => x.org_unit_type_id,
                        principalSchema: "apps",
                        principalTable: "org_unit_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "menu",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    app_id = table.Column<Guid>(type: "uuid", nullable: false),
                    menu_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    target = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description_ar = table.Column<string>(type: "text", nullable: true),
                    description_en = table.Column<string>(type: "text", nullable: true),
                    icon = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_visible = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_menu", x => x.id);
                    table.ForeignKey(
                        name: "fk_menu_app_app_id",
                        column: x => x.app_id,
                        principalSchema: "apps",
                        principalTable: "app",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_menu_menu_group_menu_group_id",
                        column: x => x.menu_group_id,
                        principalSchema: "apps",
                        principalTable: "menu_group",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_menu_menu_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "apps",
                        principalTable: "menu",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_menu_resource_resource_id",
                        column: x => x.resource_id,
                        principalSchema: "apps",
                        principalTable: "resource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "privilege",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actions = table.Column<List<string>>(type: "text[]", nullable: false),
                    field_rules = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_privilege", x => x.id);
                    table.ForeignKey(
                        name: "fk_privilege_resource_resource_id",
                        column: x => x.resource_id,
                        principalSchema: "apps",
                        principalTable: "resource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_privilege_role_role_id",
                        column: x => x.role_id,
                        principalSchema: "apps",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "domain_external_code",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    domain_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    external_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    direction = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_domain_external_code", x => x.id);
                    table.ForeignKey(
                        name: "fk_domain_external_code_domain_domain_id",
                        column: x => x.domain_id,
                        principalSchema: "apps",
                        principalTable: "domain",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "data_scope_binding",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    data_object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    column_name = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                    lov_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applies_to = table.Column<List<string>>(type: "text[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_scope_binding", x => x.id);
                    table.ForeignKey(
                        name: "fk_data_scope_binding_data_object_data_object_id",
                        column: x => x.data_object_id,
                        principalSchema: "apps",
                        principalTable: "data_object",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_data_scope_binding_lovs_lov_id",
                        column: x => x.lov_id,
                        principalSchema: "apps",
                        principalTable: "lov",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lov_binding",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lov_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    selection_mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lov_binding", x => x.id);
                    table.ForeignKey(
                        name: "fk_lov_binding_lov_lov_id",
                        column: x => x.lov_id,
                        principalSchema: "apps",
                        principalTable: "lov",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lov_binding_resource_resource_id",
                        column: x => x.resource_id,
                        principalSchema: "apps",
                        principalTable: "resource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lov_grant",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lov_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    grant_type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    granted_values = table.Column<List<string>>(type: "text[]", nullable: false),
                    rule = table.Column<string>(type: "jsonb", nullable: true),
                    default_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lov_grant", x => x.id);
                    table.ForeignKey(
                        name: "fk_lov_grant_lov_lov_id",
                        column: x => x.lov_id,
                        principalSchema: "apps",
                        principalTable: "lov",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_lov_grant_resource_resource_id",
                        column: x => x.resource_id,
                        principalSchema: "apps",
                        principalTable: "resource",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_lov_grant_role_role_id",
                        column: x => x.role_id,
                        principalSchema: "apps",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_assignment",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_assignment", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_assignment_org_unit_org_unit_id",
                        column: x => x.org_unit_id,
                        principalSchema: "apps",
                        principalTable: "org_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_assignment_position_position_id",
                        column: x => x.position_id,
                        principalSchema: "apps",
                        principalTable: "position",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_assignment_user_account_user_id",
                        column: x => x.user_id,
                        principalSchema: "apps",
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_role",
                schema: "apps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    org_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_from = table.Column<DateOnly>(type: "date", nullable: true),
                    effective_to = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_role", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_role_org_unit_org_unit_id",
                        column: x => x.org_unit_id,
                        principalSchema: "apps",
                        principalTable: "org_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_role_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "apps",
                        principalTable: "role",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_role_user_account_user_id",
                        column: x => x.user_id,
                        principalSchema: "apps",
                        principalTable: "user_account",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_app_code",
                schema: "apps",
                table: "app",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_class_code",
                schema: "apps",
                table: "class",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_class_parent_class_id",
                schema: "apps",
                table: "class",
                column: "parent_class_id");

            migrationBuilder.CreateIndex(
                name: "ix_class_attribute_class_id_code",
                schema: "apps",
                table: "class_attribute",
                columns: new[] { "class_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_data_object_code",
                schema: "apps",
                table: "data_object",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_data_scope_binding_data_object_id",
                schema: "apps",
                table: "data_scope_binding",
                column: "data_object_id");

            migrationBuilder.CreateIndex(
                name: "ix_data_scope_binding_lov_id",
                schema: "apps",
                table: "data_scope_binding",
                column: "lov_id");

            migrationBuilder.CreateIndex(
                name: "ix_domain_attributes",
                schema: "apps",
                table: "domain",
                column: "attributes")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_domain_class_id_code",
                schema: "apps",
                table: "domain",
                columns: new[] { "class_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_domain_parent_domain_id",
                schema: "apps",
                table: "domain",
                column: "parent_domain_id");

            migrationBuilder.CreateIndex(
                name: "ix_domain_external_code_domain_id_provider_code_direction",
                schema: "apps",
                table: "domain_external_code",
                columns: new[] { "domain_id", "provider_code", "direction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_domain_external_code_provider_code_external_code_direction",
                schema: "apps",
                table: "domain_external_code",
                columns: new[] { "provider_code", "external_code", "direction" });

            migrationBuilder.CreateIndex(
                name: "ix_entity_code",
                schema: "apps",
                table: "entity",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_installation_code",
                schema: "apps",
                table: "installation",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lov_class_id",
                schema: "apps",
                table: "lov",
                column: "class_id");

            migrationBuilder.CreateIndex(
                name: "ix_lov_code",
                schema: "apps",
                table: "lov",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lov_data_object_id",
                schema: "apps",
                table: "lov",
                column: "data_object_id");

            migrationBuilder.CreateIndex(
                name: "ix_lov_binding_lov_id",
                schema: "apps",
                table: "lov_binding",
                column: "lov_id");

            migrationBuilder.CreateIndex(
                name: "ix_lov_binding_resource_id_field_code",
                schema: "apps",
                table: "lov_binding",
                columns: new[] { "resource_id", "field_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lov_grant_lov_id",
                schema: "apps",
                table: "lov_grant",
                column: "lov_id");

            migrationBuilder.CreateIndex(
                name: "ix_lov_grant_resource_id",
                schema: "apps",
                table: "lov_grant",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_lov_grant_role_id_lov_id_resource_id",
                schema: "apps",
                table: "lov_grant",
                columns: new[] { "role_id", "lov_id", "resource_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_menu_app_id",
                schema: "apps",
                table: "menu",
                column: "app_id");

            migrationBuilder.CreateIndex(
                name: "ix_menu_code",
                schema: "apps",
                table: "menu",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_menu_menu_group_id",
                schema: "apps",
                table: "menu",
                column: "menu_group_id");

            migrationBuilder.CreateIndex(
                name: "ix_menu_parent_id",
                schema: "apps",
                table: "menu",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_menu_resource_id",
                schema: "apps",
                table: "menu",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_menu_group_app_id",
                schema: "apps",
                table: "menu_group",
                column: "app_id");

            migrationBuilder.CreateIndex(
                name: "ix_menu_group_code",
                schema: "apps",
                table: "menu_group",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_unit_code",
                schema: "apps",
                table: "org_unit",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_unit_org_unit_type_id",
                schema: "apps",
                table: "org_unit",
                column: "org_unit_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_unit_parent_id",
                schema: "apps",
                table: "org_unit",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_unit_path",
                schema: "apps",
                table: "org_unit",
                column: "path")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_org_unit_type_code",
                schema: "apps",
                table: "org_unit_type",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_position_code",
                schema: "apps",
                table: "position",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_privilege_resource_id",
                schema: "apps",
                table: "privilege",
                column: "resource_id");

            migrationBuilder.CreateIndex(
                name: "ix_privilege_role_id_resource_id",
                schema: "apps",
                table: "privilege",
                columns: new[] { "role_id", "resource_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resource_app_id",
                schema: "apps",
                table: "resource",
                column: "app_id");

            migrationBuilder.CreateIndex(
                name: "ix_resource_code",
                schema: "apps",
                table: "resource",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resource_parent_id",
                schema: "apps",
                table: "resource",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_app_id",
                schema: "apps",
                table: "role",
                column: "app_id");

            migrationBuilder.CreateIndex(
                name: "ix_role_code",
                schema: "apps",
                table: "role",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_setting_app_id",
                schema: "apps",
                table: "setting",
                column: "app_id");

            migrationBuilder.CreateIndex(
                name: "ix_setting_code_app_id",
                schema: "apps",
                table: "setting",
                columns: new[] { "code", "app_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_ui_text_app_id",
                schema: "apps",
                table: "ui_text",
                column: "app_id");

            migrationBuilder.CreateIndex(
                name: "ix_ui_text_code_app_id",
                schema: "apps",
                table: "ui_text",
                columns: new[] { "code", "app_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_user_account_code",
                schema: "apps",
                table: "user_account",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_account_idp_subject",
                schema: "apps",
                table: "user_account",
                column: "idp_subject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_assignment_org_unit_id",
                schema: "apps",
                table: "user_assignment",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_assignment_position_id",
                schema: "apps",
                table: "user_assignment",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_assignment_user_id",
                schema: "apps",
                table: "user_assignment",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_role_org_unit_id",
                schema: "apps",
                table: "user_role",
                column: "org_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_role_role_id",
                schema: "apps",
                table: "user_role",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_role_user_id",
                schema: "apps",
                table: "user_role",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "class_attribute",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "data_scope_binding",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "domain_external_code",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "entity",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "installation",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "lov_binding",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "lov_grant",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "menu",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "privilege",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "setting",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "ui_text",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "user_assignment",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "user_role",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "domain",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "lov",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "menu_group",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "resource",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "position",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "org_unit",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "role",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "user_account",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "class",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "data_object",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "org_unit_type",
                schema: "apps");

            migrationBuilder.DropTable(
                name: "app",
                schema: "apps");
        }
    }
}
