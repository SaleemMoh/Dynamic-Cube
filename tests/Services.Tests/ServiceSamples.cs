using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Changes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Parties;
using DynamicCube.Tests.Infrastructure;

namespace DynamicCube.Modules.Services.Tests;

/// <summary>A small classification-like service: sectors choose sections, a shared question, levels with bracket fees.</summary>
internal sealed class ServiceSamples
{
    public string Service { get; } = TestHost.Unique("CLS");

    public string Sectors { get; } = TestHost.Unique("SECTORS");

    public string Employees { get; } = TestHost.Unique("EMPLOYEES");

    public string Revenue { get; } = TestHost.Unique("REVENUE");

    public string IctCategory { get; } = TestHost.Unique("ICT_CAT");

    public string BuildingProjects { get; } = TestHost.Unique("PROJECTS");

    public string CrNumber { get; } = TestHost.Unique("CR");

    public Change[] Questions() =>
    [
        Question(Sectors, "القطاعات", "Sectors", new QuestionPayload
        {
            Type = QuestionType.Select,
            Multiple = true,
            Options = [Option("ICT", "الاتصالات وتقنية المعلومات", "ICT"), Option("BUILDING", "التشييد والبناء", "Construction & building")],
        }),
        Question(Employees, "عدد الموظفين", "Employees", new QuestionPayload { Type = QuestionType.Number, Validation = new QuestionValidation { Min = 1, Max = 100_000 } }),
        Question(Revenue, "الإيرادات السنوية", "Annual revenue", new QuestionPayload { Type = QuestionType.Number, Validation = new QuestionValidation { Min = 0 } }),
        Question(IctCategory, "فئة التصنيف الرقمي", "Digital classification category", new QuestionPayload
        {
            Type = QuestionType.Select,
            Options = [Option("PLATINUM", "بلاتيني", "Platinum"), Option("GOLD", "ذهبي", "Gold")],
        }),
        Question(BuildingProjects, "عدد المشاريع", "Number of projects", new QuestionPayload { Type = QuestionType.Number }),
        Question(CrNumber, "رقم السجل التجاري", "CR number", new QuestionPayload { Type = QuestionType.Text, Prefill = "party.identifiers.CR_NUMBER", Validation = new QuestionValidation { Pattern = "^[0-9]{10}$" } }),
    ];

    public Change ServiceChange(params PartyKind[] kinds) =>
        new(ServiceChangeTypes.Service, Service, ChangeOperation.Create, new ServicePayload
        {
            NameAr = "تصنيف مقدمي الخدمات",
            NameEn = "Service provider classification",
            Archetype = "ATTESTATION",
            PartyKinds = kinds.Length == 0 ? [PartyKind.Establishment] : [.. kinds],
            NumberPrefix = "CLS",
        });

    public VersionDefinition Definition() => new()
    {
        Levels =
        [
            new() { Code = "REGULAR", NameAr = "عادي", NameEn = "Regular", SlaWorkingDays = 60 },
            new() { Code = "PREMIUM", NameAr = "مميز", NameEn = "Premium", SlaWorkingDays = 30 },
        ],
        Steps =
        [
            new() { Code = "LEVEL", Type = StepType.Level, TitleAr = "مستوى الخدمة", TitleEn = "Service level" },
            new()
            {
                Code = "SECTORS",
                Type = StepType.Form,
                TitleAr = "القطاعات",
                TitleEn = "Sectors",
                Sections = [new() { Code = "SECTOR_CHOICE", TitleAr = "اختيار القطاعات", TitleEn = "Choose sectors", Questions = [new() { Code = Sectors, Required = true }, new() { Code = CrNumber, Required = true, Readonly = true }] }],
            },
            new()
            {
                Code = "DETAILS",
                Type = StepType.Form,
                TitleAr = "البيانات",
                TitleEn = "Details",
                Sections =
                [
                    new()
                    {
                        Code = "ICT",
                        TitleAr = "الاتصالات وتقنية المعلومات",
                        TitleEn = "ICT",
                        VisibleWhen = Rule("""{"in": ["ICT", {"var": "SECTORS"}]}""".Replace("SECTORS", Sectors, StringComparison.Ordinal)),
                        Questions = [new() { Code = IctCategory, Required = true }, new() { Code = Employees, Required = true }],
                    },
                    new()
                    {
                        Code = "BUILDING",
                        TitleAr = "التشييد والبناء",
                        TitleEn = "Construction",
                        VisibleWhen = Rule("""{"in": ["BUILDING", {"var": "SECTORS"}]}""".Replace("SECTORS", Sectors, StringComparison.Ordinal)),
                        Questions = [new() { Code = Employees, Required = true }, new() { Code = BuildingProjects, Required = true }],
                    },
                    new() { Code = "FINANCE", TitleAr = "المالية", TitleEn = "Finance", Questions = [new() { Code = Revenue, Required = true }] },
                ],
            },
            new() { Code = "FEES", Type = StepType.Fees, TitleAr = "الرسوم", TitleEn = "Fees" },
            new() { Code = "DECLARATION", Type = StepType.Declaration, TitleAr = "الإقرار", TitleEn = "Declaration", TextAr = "أقر بصحة البيانات", TextEn = "I confirm the data is correct" },
        ],
        Eligibility =
        [
            new() { Code = "NOT_BANNED", Rule = Rule("""{"!": {"var": "party.blocked"}}"""), MessageAr = "يوجد جزاء يمنع التقديم.", MessageEn = "A sanction bans new requests." },
        ],
        Validations =
        [
            new()
            {
                Code = "PROJECTS_FOR_EMPLOYEES",
                Step = "DETAILS",
                Rule = Rule("""{"or": [{"!": {"var": "data.PROJECTS"}}, {"<=": [{"var": "data.PROJECTS"}, {"*": [{"var": "data.EMPLOYEES"}, 10]}]}]}""".Replace("PROJECTS", BuildingProjects, StringComparison.Ordinal).Replace("EMPLOYEES", Employees, StringComparison.Ordinal)),
                MessageAr = "عدد المشاريع لا يتناسب مع عدد الموظفين.",
                MessageEn = "Too many projects for the number of employees.",
            },
        ],
        Fees =
        [
            new()
            {
                Code = "PREMIUM_FEE",
                NameAr = "رسوم الخدمة المميزة",
                NameEn = "Premium service fee",
                Levels = ["PREMIUM"],
                Bracket = new BracketDefinition
                {
                    Of = Rule("""{"var": "data.REVENUE"}""".Replace("REVENUE", Revenue, StringComparison.Ordinal)),
                    Rows = [new() { From = 0, To = 10_000_000, Amount = 1000 }, new() { From = 10_000_000, To = 50_000_000, Amount = 5000 }, new() { From = 50_000_000, Amount = 15000 }],
                },
            },
        ],
    };

    public Change Version(int number, VersionDefinition? definition = null) =>
        new(ServiceChangeTypes.Version, $"{Service}/{number}", ChangeOperation.Create, new ServiceVersionPayload { NameAr = $"الإصدار {number}", NameEn = $"Version {number}", Definition = definition ?? Definition() });

    public Change[] All() => [.. Questions(), ServiceChange(), Version(1)];

    public static JsonNode Rule(string json) => JsonNode.Parse(json)!;

    private static Change Question(string code, string ar, string en, QuestionPayload payload)
    {
        payload.NameAr = ar;
        payload.NameEn = en;
        return new Change(ServiceChangeTypes.Question, code, ChangeOperation.Create, payload);
    }

    private static QuestionOption Option(string value, string ar, string en) => new() { Value = value, LabelAr = ar, LabelEn = en };
}
