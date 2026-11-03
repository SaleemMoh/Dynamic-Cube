using System.Text.Json.Nodes;
using DynamicCube.Modules.Workflow.Domain;
using DynamicCube.Modules.Workflow.Persistence;
using DynamicCube.Modules.Workflow.Runtime;
using DynamicCube.Platform.ChangeSets;
using DynamicCube.Platform.Expressions;

namespace DynamicCube.Modules.Workflow.Changes;

public sealed class AutomationRulePayload : ConfigPayload
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public List<string> Workflows { get; set; } = [];

    public List<string> Steps { get; set; } = [];

    /// <summary>JSONLogic over <c>task</c>, e.g. <c>{"&gt;=": [{"var": "task.unassignedHours"}, 4]}</c>.</summary>
    public JsonNode? When { get; set; }

    public AutomationAction Action { get; set; }

    public string? EscalateToRole { get; set; }

    public int? RepeatHours { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>Automation rules: a valid condition over the task's facts, the action and its role, how often it repeats.</summary>
internal sealed class AutomationRuleApplier(WorkflowDbContext db, JsonLogicEvaluator evaluator) : WorkflowApplierBase<AutomationRule, AutomationRulePayload>(db)
{
    public override string EntityType => WorkflowChangeTypes.AutomationRule;

    protected override IQueryable<AutomationRule> Query(WorkflowDbContext context) => context.AutomationRules;

    protected override IQueryable<AutomationRule> ListQuery(WorkflowDbContext context, string? parent) =>
        Query(context).OrderBy(r => r.DisplayOrder).ThenBy(r => r.Code);

    protected override Task ApplyAsync(AutomationRule entity, AutomationRulePayload payload, ConfigRefs<WorkflowDbContext> refs, ValidationErrors errors, AutomationRule? existing, CancellationToken cancellationToken)
    {
        errors.MaxLength("descriptionAr", payload.DescriptionAr, 2000);
        errors.MaxLength("descriptionEn", payload.DescriptionEn, 2000);
        if (payload.When is null)
        {
            errors.Add("when", "Say when the rule acts.");
        }
        else
        {
            try
            {
                evaluator.Evaluate(payload.When, AutomationFacts.Sample);
            }
            catch (JsonLogicException ex)
            {
                errors.Add("when", $"Invalid rule: {ex.Message}");
            }
        }

        if (payload.Action == AutomationAction.Escalate)
        {
            errors.Required("escalateToRole", payload.EscalateToRole);
        }

        if (payload.RepeatHours is < 1 or > 24 * 30)
        {
            errors.Add("repeatHours", "Must be between 1 and 720 hours.");
        }

        entity.DescriptionAr = Blank(payload.DescriptionAr);
        entity.DescriptionEn = Blank(payload.DescriptionEn);
        entity.Workflows = [.. payload.Workflows.Where(w => !string.IsNullOrWhiteSpace(w)).Select(w => w.Trim()).Distinct()];
        entity.Steps = [.. payload.Steps.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct()];
        entity.Condition = payload.When?.ToJsonString() ?? "true";
        entity.Action = payload.Action;
        entity.EscalateToRole = payload.Action == AutomationAction.Escalate ? Blank(payload.EscalateToRole) : null;
        entity.RepeatHours = payload.RepeatHours;
        entity.DisplayOrder = payload.DisplayOrder;
        return Task.CompletedTask;
    }

    protected override Task<AutomationRulePayload> ToPayloadAsync(WorkflowDbContext context, AutomationRule entity, CancellationToken cancellationToken) =>
        Task.FromResult(new AutomationRulePayload
        {
            DescriptionAr = entity.DescriptionAr,
            DescriptionEn = entity.DescriptionEn,
            Workflows = entity.Workflows,
            Steps = entity.Steps,
            When = JsonNode.Parse(entity.Condition),
            Action = entity.Action,
            EscalateToRole = entity.EscalateToRole,
            RepeatHours = entity.RepeatHours,
            DisplayOrder = entity.DisplayOrder,
        });

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
