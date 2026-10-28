using System.Globalization;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Platform.Communication;

namespace DynamicCube.Modules.Workflow.Runtime;

/// <summary>The events workflow tasks raise for staff notifications.</summary>
internal static class TaskNotifications
{
    public const string Created = "TASK_CREATED";
    public const string Assigned = "TASK_ASSIGNED";
    public const string SlaWarning = "TASK_SLA_WARNING";
    public const string SlaBreached = "TASK_SLA_BREACHED";

    public static CommunicationEventType[] Types { get; } =
    [
        Type(Created, "مهمة جديدة في قائمة الدور", "New task in a role's queue"),
        Type(Assigned, "إسناد مهمة إلى موظف", "Task assigned to an employee"),
        Type(SlaWarning, "اقتراب موعد إنجاز مهمة", "Task nearing its due time"),
        Type(SlaBreached, "تجاوز موعد إنجاز مهمة", "Task overdue"),
    ];

    public static void Raise(WorkflowDbContext context, TimeProvider clock, string code, WorkTask task) =>
        context.Raise(clock, code, task.SubjectType, task.SubjectId, task.SubjectNumber, null, new JsonObject
        {
            ["task"] = new JsonObject
            {
                ["id"] = task.Id.ToString(),
                ["node"] = task.NodeCode,
                ["nameAr"] = task.NameAr,
                ["nameEn"] = task.NameEn,
                ["role"] = task.CandidateRole,
                ["assignee"] = task.Assignee,
                ["assignedBy"] = task.AssignedBy,
                ["dueAt"] = task.DueAt?.ToString("O", CultureInfo.InvariantCulture),
                ["escalatedToRole"] = task.EscalatedToRole,
                ["titleAr"] = task.TitleAr,
                ["titleEn"] = task.TitleEn,
                ["partyNameAr"] = task.PartyNameAr,
                ["partyNameEn"] = task.PartyNameEn,
                ["service"] = task.ServiceCode,
            },
        });

    private static CommunicationEventType Type(string code, string nameAr, string nameEn) => new(code, nameAr, nameEn, "workflow", new JsonObject
    {
        ["task"] = new JsonObject
        {
            ["id"] = Guid.Empty.ToString(),
            ["node"] = "ANALYSIS",
            ["nameAr"] = "التحليل الفني",
            ["nameEn"] = "Technical analysis",
            ["role"] = "CLS_ANALYST",
            ["assignee"] = "analyst1",
            ["assignedBy"] = "manager",
            ["dueAt"] = "2026-01-05T09:00:00+03:00",
            ["escalatedToRole"] = "CLS_MANAGER",
            ["titleAr"] = "طلب تصنيف جديد",
            ["titleEn"] = "New classification",
            ["partyNameAr"] = "مؤسسة المثال",
            ["partyNameEn"] = "Sample Establishment",
            ["service"] = "CLASSIFICATION_NEW",
        },
    });
}
