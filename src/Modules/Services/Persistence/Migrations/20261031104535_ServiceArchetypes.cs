using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DynamicCube.Modules.Services.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ServiceArchetypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "archetype",
                schema: "svc",
                table: "service",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.CreateTable(
                name: "archetype",
                schema: "svc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    description_ar = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    description_en = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    icon = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    template = table.Column<string>(type: "jsonb", nullable: true),
                    workflow = table.Column<string>(type: "jsonb", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name_ar = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    name_en = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    owner = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    entity_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    business_app = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    shared_apps = table.Column<List<string>>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_archetype", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_archetype_code",
                schema: "svc",
                table: "archetype",
                column: "code",
                unique: true);

            // The archetypes services had as fixed values, now declared: each with a service template and a default workflow
            // (a review by role SERVICE_REVIEWER; an attestation completes without review).
            migrationBuilder.Sql(@"INSERT INTO svc.archetype (id, code, name_ar, name_en, description_ar, description_en, icon, display_order, template, workflow, is_active, owner, shared_apps, created_at, created_by) VALUES
(gen_random_uuid(), 'ISSUANCE', 'إصدار', 'Issuance', 'إصدار وثيقة أو ترخيص جديد بعد مراجعة الطلب.', 'Issues a new document or licence after the request is reviewed.', 'workspace_premium', 1, '{""levels"": [], ""steps"": [{""code"": ""DETAILS"", ""type"": ""FORM"", ""titleAr"": ""بيانات الطلب"", ""titleEn"": ""Request details"", ""sections"": [{""code"": ""MAIN"", ""titleAr"": ""البيانات الأساسية"", ""titleEn"": ""Main data"", ""questions"": []}]}, {""code"": ""REVIEW"", ""type"": ""REVIEW"", ""titleAr"": ""المراجعة"", ""titleEn"": ""Review"", ""sections"": []}, {""code"": ""DECLARATION"", ""type"": ""DECLARATION"", ""titleAr"": ""الإقرار"", ""titleEn"": ""Declaration"", ""sections"": [], ""textAr"": ""أقر بأن جميع البيانات والمرفقات المقدمة صحيحة وأتحمل المسؤولية عن أي بيانات غير صحيحة."", ""textEn"": ""I confirm that all the data and attachments given are correct and I am responsible for any incorrect data.""}], ""eligibility"": [], ""validations"": [], ""fees"": []}'::jsonb, '{""nodes"": [{""code"": ""START"", ""type"": ""START"", ""nameAr"": ""تقديم الطلب"", ""nameEn"": ""Submitted"", ""next"": ""REVIEW"", ""layout"": {""x"": 340, ""y"": 40}}, {""code"": ""REVIEW"", ""type"": ""HUMAN_TASK"", ""nameAr"": ""مراجعة الطلب"", ""nameEn"": ""Review the request"", ""assignment"": {""target"": ""ROLE"", ""role"": ""SERVICE_REVIEWER"", ""mode"": ""QUEUE""}, ""actions"": [{""code"": ""APPROVE"", ""kind"": ""COMPLETE"", ""nameAr"": ""اعتماد"", ""nameEn"": ""Approve"", ""to"": ""APPROVED""}, {""code"": ""RETURN"", ""kind"": ""RETURN"", ""nameAr"": ""طلب بيانات إضافية"", ""nameEn"": ""Ask for more data""}, {""code"": ""REJECT"", ""kind"": ""REJECT"", ""nameAr"": ""رفض"", ""nameEn"": ""Reject"", ""to"": ""REJECTED""}], ""layout"": {""x"": 340, ""y"": 170}}, {""code"": ""APPROVED"", ""type"": ""END"", ""nameAr"": ""معتمد"", ""nameEn"": ""Approved"", ""outcome"": ""APPROVED"", ""layout"": {""x"": 200, ""y"": 320}}, {""code"": ""REJECTED"", ""type"": ""END"", ""nameAr"": ""مرفوض"", ""nameEn"": ""Rejected"", ""outcome"": ""REJECTED"", ""layout"": {""x"": 480, ""y"": 320}}]}'::jsonb, true, 'Entity', '{}', now(), 'system'),
(gen_random_uuid(), 'AMENDMENT', 'تعديل', 'Amendment', 'تعديل بيانات وثيقة قائمة بعد مراجعة الطلب.', 'Changes an existing document after the request is reviewed.', 'edit_document', 2, '{""levels"": [], ""steps"": [{""code"": ""DETAILS"", ""type"": ""FORM"", ""titleAr"": ""بيانات التعديل"", ""titleEn"": ""Amendment details"", ""sections"": [{""code"": ""MAIN"", ""titleAr"": ""البيانات الأساسية"", ""titleEn"": ""Main data"", ""questions"": []}]}, {""code"": ""REVIEW"", ""type"": ""REVIEW"", ""titleAr"": ""المراجعة"", ""titleEn"": ""Review"", ""sections"": []}, {""code"": ""DECLARATION"", ""type"": ""DECLARATION"", ""titleAr"": ""الإقرار"", ""titleEn"": ""Declaration"", ""sections"": [], ""textAr"": ""أقر بأن جميع البيانات والمرفقات المقدمة صحيحة وأتحمل المسؤولية عن أي بيانات غير صحيحة."", ""textEn"": ""I confirm that all the data and attachments given are correct and I am responsible for any incorrect data.""}], ""eligibility"": [], ""validations"": [], ""fees"": []}'::jsonb, '{""nodes"": [{""code"": ""START"", ""type"": ""START"", ""nameAr"": ""تقديم الطلب"", ""nameEn"": ""Submitted"", ""next"": ""REVIEW"", ""layout"": {""x"": 340, ""y"": 40}}, {""code"": ""REVIEW"", ""type"": ""HUMAN_TASK"", ""nameAr"": ""مراجعة التعديل"", ""nameEn"": ""Review the amendment"", ""assignment"": {""target"": ""ROLE"", ""role"": ""SERVICE_REVIEWER"", ""mode"": ""QUEUE""}, ""actions"": [{""code"": ""APPROVE"", ""kind"": ""COMPLETE"", ""nameAr"": ""اعتماد"", ""nameEn"": ""Approve"", ""to"": ""APPROVED""}, {""code"": ""RETURN"", ""kind"": ""RETURN"", ""nameAr"": ""طلب بيانات إضافية"", ""nameEn"": ""Ask for more data""}, {""code"": ""REJECT"", ""kind"": ""REJECT"", ""nameAr"": ""رفض"", ""nameEn"": ""Reject"", ""to"": ""REJECTED""}], ""layout"": {""x"": 340, ""y"": 170}}, {""code"": ""APPROVED"", ""type"": ""END"", ""nameAr"": ""معتمد"", ""nameEn"": ""Approved"", ""outcome"": ""APPROVED"", ""layout"": {""x"": 200, ""y"": 320}}, {""code"": ""REJECTED"", ""type"": ""END"", ""nameAr"": ""مرفوض"", ""nameEn"": ""Rejected"", ""outcome"": ""REJECTED"", ""layout"": {""x"": 480, ""y"": 320}}]}'::jsonb, true, 'Entity', '{}', now(), 'system'),
(gen_random_uuid(), 'VISIT_REQUEST', 'طلب زيارة', 'Visit request', 'طلب زيارة أو معاينة للموقع.', 'Asks for a site visit or inspection.', 'location_on', 3, '{""levels"": [], ""steps"": [{""code"": ""DETAILS"", ""type"": ""FORM"", ""titleAr"": ""بيانات الزيارة"", ""titleEn"": ""Visit details"", ""sections"": [{""code"": ""MAIN"", ""titleAr"": ""البيانات الأساسية"", ""titleEn"": ""Main data"", ""questions"": []}]}, {""code"": ""REVIEW"", ""type"": ""REVIEW"", ""titleAr"": ""المراجعة"", ""titleEn"": ""Review"", ""sections"": []}, {""code"": ""DECLARATION"", ""type"": ""DECLARATION"", ""titleAr"": ""الإقرار"", ""titleEn"": ""Declaration"", ""sections"": [], ""textAr"": ""أقر بأن جميع البيانات والمرفقات المقدمة صحيحة وأتحمل المسؤولية عن أي بيانات غير صحيحة."", ""textEn"": ""I confirm that all the data and attachments given are correct and I am responsible for any incorrect data.""}], ""eligibility"": [], ""validations"": [], ""fees"": []}'::jsonb, '{""nodes"": [{""code"": ""START"", ""type"": ""START"", ""nameAr"": ""تقديم الطلب"", ""nameEn"": ""Submitted"", ""next"": ""REVIEW"", ""layout"": {""x"": 340, ""y"": 40}}, {""code"": ""REVIEW"", ""type"": ""HUMAN_TASK"", ""nameAr"": ""مراجعة طلب الزيارة"", ""nameEn"": ""Review the visit request"", ""assignment"": {""target"": ""ROLE"", ""role"": ""SERVICE_REVIEWER"", ""mode"": ""QUEUE""}, ""actions"": [{""code"": ""APPROVE"", ""kind"": ""COMPLETE"", ""nameAr"": ""اعتماد"", ""nameEn"": ""Approve"", ""to"": ""APPROVED""}, {""code"": ""RETURN"", ""kind"": ""RETURN"", ""nameAr"": ""طلب بيانات إضافية"", ""nameEn"": ""Ask for more data""}, {""code"": ""REJECT"", ""kind"": ""REJECT"", ""nameAr"": ""رفض"", ""nameEn"": ""Reject"", ""to"": ""REJECTED""}], ""layout"": {""x"": 340, ""y"": 170}}, {""code"": ""APPROVED"", ""type"": ""END"", ""nameAr"": ""معتمد"", ""nameEn"": ""Approved"", ""outcome"": ""APPROVED"", ""layout"": {""x"": 200, ""y"": 320}}, {""code"": ""REJECTED"", ""type"": ""END"", ""nameAr"": ""مرفوض"", ""nameEn"": ""Rejected"", ""outcome"": ""REJECTED"", ""layout"": {""x"": 480, ""y"": 320}}]}'::jsonb, true, 'Entity', '{}', now(), 'system'),
(gen_random_uuid(), 'ATTESTATION', 'تصديق', 'Attestation', 'تصديق فوري دون مراجعة.', 'An immediate attestation without review.', 'verified', 4, '{""levels"": [], ""steps"": [{""code"": ""DETAILS"", ""type"": ""FORM"", ""titleAr"": ""بيانات التصديق"", ""titleEn"": ""Attestation details"", ""sections"": [{""code"": ""MAIN"", ""titleAr"": ""البيانات الأساسية"", ""titleEn"": ""Main data"", ""questions"": []}]}, {""code"": ""REVIEW"", ""type"": ""REVIEW"", ""titleAr"": ""المراجعة"", ""titleEn"": ""Review"", ""sections"": []}, {""code"": ""DECLARATION"", ""type"": ""DECLARATION"", ""titleAr"": ""الإقرار"", ""titleEn"": ""Declaration"", ""sections"": [], ""textAr"": ""أقر بأن جميع البيانات والمرفقات المقدمة صحيحة وأتحمل المسؤولية عن أي بيانات غير صحيحة."", ""textEn"": ""I confirm that all the data and attachments given are correct and I am responsible for any incorrect data.""}], ""eligibility"": [], ""validations"": [], ""fees"": []}'::jsonb, NULL, true, 'Entity', '{}', now(), 'system');");
            migrationBuilder.Sql(@"UPDATE svc.service SET archetype = CASE archetype WHEN 'Issuance' THEN 'ISSUANCE' WHEN 'Amendment' THEN 'AMENDMENT' WHEN 'VisitRequest' THEN 'VISIT_REQUEST' WHEN 'Attestation' THEN 'ATTESTATION' ELSE upper(archetype) END;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE svc.service SET archetype = CASE archetype WHEN 'ISSUANCE' THEN 'Issuance' WHEN 'AMENDMENT' THEN 'Amendment' WHEN 'VISIT_REQUEST' THEN 'VisitRequest' WHEN 'ATTESTATION' THEN 'Attestation' ELSE archetype END;");

            migrationBuilder.DropTable(
                name: "archetype",
                schema: "svc");

            migrationBuilder.AlterColumn<string>(
                name: "archetype",
                schema: "svc",
                table: "service",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);
        }
    }
}
