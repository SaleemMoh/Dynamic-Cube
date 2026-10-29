using System.Text.Json.Nodes;
using DynamicCube.Platform.Lookups;

namespace DynamicCube.Modules.Workflow;

/// <summary>
/// The workflow's lists of options, kept in Class/Domain: the designer shows their names, icons and hints, and a value
/// made inactive is no longer offered. What each code does stays in the engine.
/// </summary>
internal static class WorkflowClasses
{
    public const string StepType = "WF_STEP_TYPE";
    public const string Assignee = "WF_ASSIGNEE_TYPE";
    public const string Dispatch = "WF_DISPATCH_MODE";
    public const string ActionKind = "WF_ACTION_KIND";
    public const string JoinMode = "WF_JOIN_MODE";
    public const string EndOutcome = "WF_END_OUTCOME";
    public const string UnitSource = "WF_UNIT_SOURCE";

    private static readonly CatalogClassField Icon = new("ICON", "الأيقونة", "Icon", LookupAttributeTypes.Text);
    private static readonly CatalogClassField HintAr = new("HINT_AR", "الوصف بالعربية", "Arabic hint", LookupAttributeTypes.Text);
    private static readonly CatalogClassField HintEn = new("HINT_EN", "الوصف بالإنجليزية", "English hint", LookupAttributeTypes.Text);

    private static CatalogValue Step(string code, string ar, string en, string icon, string hintAr, string hintEn) =>
        new(code, ar, en, new JsonObject { ["ICON"] = icon, ["HINT_AR"] = hintAr, ["HINT_EN"] = hintEn });

    private static CatalogValue Value(string code, string ar, string en) => new(code, ar, en);

    public static IReadOnlyList<CatalogClass> All { get; } =
    [
        new(StepType, "أنواع خطوات مسار العمل", "Workflow step types", [Icon, HintAr, HintEn],
        [
            Step("START", "البداية", "Start", "play_circle", "حيث يبدأ كل طلب", "Where every request starts"),
            Step("HUMAN_TASK", "مهمة موظف", "Human task", "person", "يعمل عليها موظف ويختار إجراءً", "An employee works on it and takes an action"),
            Step("PARALLEL_SPLIT", "تفرع متوازٍ", "Parallel split", "call_split", "تبدأ كل الفروع معًا", "Starts every branch at once"),
            Step("JOIN", "دمج الفروع", "Join", "merge", "تنتظر الفروع ثم تكمل", "Waits for the branches, then goes on"),
            Step("GATEWAY", "توجيه بشرط", "Gateway", "alt_route", "أول شرط يتحقق يحدد الطريق", "The first condition that holds picks the way"),
            Step("DECISION", "تقييم قواعد", "Decision", "rule", "يقيّم مجموعة قواعد (مثل الدرجة)", "Evaluates a rule set (e.g. the grade)"),
            Step("DOCUMENT", "مستند", "Document", "description", "يصدر مستندًا أو يعدّله", "Issues or amends a document"),
            Step("PAYMENT", "سداد", "Payment", "payments", "يصدر فاتورة وينتظر السداد", "Bills the fees and waits for payment"),
            Step("VISIT", "زيارة ميدانية", "Site visit", "location_on", "يطلب زيارة (ميدانية أو لدى جهة معاينة أو يدوية) وينتظر نتيجتها", "Requests a visit (field, external inspection or manual) and waits for its result"),
            Step("END", "نهاية", "End", "flag", "نتيجة الطلب للمستفيد", "The result for the applicant"),
        ],
        "الخطوات التي يعرضها مصمم مسارات العمل. يمكن تغيير أسمائها أو إيقاف أحدها فلا يظهر في المصمم.",
        "The steps the workflow designer offers. Rename them, or make one inactive to hide it from the designer."),
        new(Assignee, "من يعمل على المهمة", "Who works on a task", [],
        [
            Value("ROLE", "من يحمل دورًا", "Holders of a role"),
            Value("USER", "موظف محدد", "A named employee"),
            Value("PREVIOUS_ACTOR", "من أنجز خطوة سابقة", "Whoever completed an earlier step"),
            Value("ORG_UNIT", "حسب الهيكل التنظيمي", "By the organization structure"),
        ]),
        new(Dispatch, "طرق توزيع المهام", "Task dispatching methods", [],
        [
            Value("QUEUE", "قائمة مشتركة يستلم منها", "Shared queue (claim)"),
            Value("DISTRIBUTE", "يوزعها موزّع", "A distributor assigns"),
            Value("AUTO", "تلقائيًا للأقل مهامًا", "Automatically to the least busy"),
        ]),
        new(ActionKind, "أنواع إجراءات المهام", "Task action kinds", [],
        [
            Value("COMPLETE", "إحالة", "Send on"),
            Value("APPROVE", "اعتماد", "Approve"),
            Value("REJECT", "رفض", "Reject"),
            Value("RETURN", "إعادة للمستفيد", "Return to the applicant"),
        ]),
        new(JoinMode, "طرق دمج الفروع", "Join modes", [],
        [
            Value("ALL", "كل الفروع", "All branches"),
            Value("ANY", "أول فرع", "The first branch"),
            Value("COUNT", "عدد من الفروع", "A number of branches"),
        ]),
        new(EndOutcome, "نتائج الطلب", "Request outcomes", [],
        [
            Value("APPROVED", "معتمد", "Approved"),
            Value("REJECTED", "مرفوض", "Rejected"),
            Value("CLOSED", "مغلق", "Closed"),
        ]),
        new(UnitSource, "مصادر الوحدة التنظيمية", "Where an org unit comes from", [],
        [
            Value("FIXED", "وحدة محددة", "A given unit"),
            Value("RULE", "وحدة تحددها قاعدة", "A unit chosen by a rule"),
            Value("ACTOR", "وحدة من أنجز خطوة سابقة", "The unit of whoever completed a step"),
        ]),
    ];
}
