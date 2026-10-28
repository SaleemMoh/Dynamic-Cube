using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Workflow.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "wf");

            migrationBuilder.CreateTable(
                name: "delegation",
                schema: "wf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegator = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    @delegate = table.Column<string>(name: "delegate", type: "character varying(256)", maxLength: 256, nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    to_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_delegation", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow",
                schema: "wf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description_ar = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    description_en = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
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
                    table.PrimaryKey("pk_workflow", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_version",
                schema: "wf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    definition = table.Column<string>(type: "jsonb", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_retired = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_workflow_version", x => x.id);
                    table.ForeignKey(
                        name: "fk_workflow_version_workflow_workflow_id",
                        column: x => x.workflow_id,
                        principalSchema: "wf",
                        principalTable: "workflow",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "instance",
                schema: "wf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workflow_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    title_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    party_name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    party_name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    service_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    level_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sla_working_days = table.Column<int>(type: "integer", nullable: true),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sla = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    paused_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_instance", x => x.id);
                    table.ForeignKey(
                        name: "fk_instance_workflow_version_version_id",
                        column: x => x.version_id,
                        principalSchema: "wf",
                        principalTable: "workflow_version",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "event",
                schema: "wf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    on_behalf_of = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    node_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    action_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    target = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    visible_to_applicant = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_event_instance_instance_id",
                        column: x => x.instance_id,
                        principalSchema: "wf",
                        principalTable: "instance",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task",
                schema: "wf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    candidate_role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    candidate_org_unit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    candidate_position = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    candidate_user = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    mode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    distributor_role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    excluded_users = table.Column<List<string>>(type: "text[]", nullable: false),
                    assignee = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    assigned_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    warn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sla = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    escalated_to_role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    action_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    comment = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    title_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    party_name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    party_name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    service_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    level_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_instance_instance_id",
                        column: x => x.instance_id,
                        principalSchema: "wf",
                        principalTable: "instance",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "token",
                schema: "wf",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    instance_id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    forks = table.Column<string>(type: "jsonb", nullable: false),
                    resume_at = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_token", x => x.id);
                    table.ForeignKey(
                        name: "fk_token_instance_instance_id",
                        column: x => x.instance_id,
                        principalSchema: "wf",
                        principalTable: "instance",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_delegation_delegate_from_date_to_date",
                schema: "wf",
                table: "delegation",
                columns: new[] { "delegate", "from_date", "to_date" });

            migrationBuilder.CreateIndex(
                name: "ix_event_instance_id_at",
                schema: "wf",
                table: "event",
                columns: new[] { "instance_id", "at" });

            migrationBuilder.CreateIndex(
                name: "ix_instance_status_due_at",
                schema: "wf",
                table: "instance",
                columns: new[] { "status", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_instance_subject_type_subject_id",
                schema: "wf",
                table: "instance",
                columns: new[] { "subject_type", "subject_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_instance_version_id",
                schema: "wf",
                table: "instance",
                column: "version_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_instance_id",
                schema: "wf",
                table: "task",
                column: "instance_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_status_assignee",
                schema: "wf",
                table: "task",
                columns: new[] { "status", "assignee" });

            migrationBuilder.CreateIndex(
                name: "ix_task_status_candidate_role",
                schema: "wf",
                table: "task",
                columns: new[] { "status", "candidate_role" });

            migrationBuilder.CreateIndex(
                name: "ix_task_status_due_at",
                schema: "wf",
                table: "task",
                columns: new[] { "status", "due_at" });

            migrationBuilder.CreateIndex(
                name: "ix_token_instance_id_status",
                schema: "wf",
                table: "token",
                columns: new[] { "instance_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_workflow_code",
                schema: "wf",
                table: "workflow",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_version_code",
                schema: "wf",
                table: "workflow_version",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_version_workflow_id_number",
                schema: "wf",
                table: "workflow_version",
                columns: new[] { "workflow_id", "number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "delegation",
                schema: "wf");

            migrationBuilder.DropTable(
                name: "event",
                schema: "wf");

            migrationBuilder.DropTable(
                name: "task",
                schema: "wf");

            migrationBuilder.DropTable(
                name: "token",
                schema: "wf");

            migrationBuilder.DropTable(
                name: "instance",
                schema: "wf");

            migrationBuilder.DropTable(
                name: "workflow_version",
                schema: "wf");

            migrationBuilder.DropTable(
                name: "workflow",
                schema: "wf");
        }
    }
}
