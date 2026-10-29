using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DynamicCube.Platform.Documents;
using DynamicCube.Platform.Expressions;

namespace DynamicCube.Modules.Workflow.Definitions;

/// <summary>Checks a workflow graph before it can be published: structure, references, assignments, actions and rules.</summary>
internal static partial class DefinitionValidator
{
    private const string Field = "definition.nodes";

    /// <summary>Largest coordinate a node may be drawn at.</summary>
    private const double MaxCanvas = 20_000;

    public static void Validate(WorkflowDefinition definition, JsonLogicEvaluator evaluator, ValidationErrors errors)
    {
        var nodes = definition.Nodes;
        if (definition.SlaWorkingDays is < 1 or > 1000)
        {
            errors.Add("definition.slaWorkingDays", "Must be between 1 and 1000.");
        }

        if (nodes.Count(n => n.Type == NodeType.Start) != 1)
        {
            errors.Add(Field, "A workflow needs exactly one START node.");
        }

        if (!nodes.Any(n => n.Type == NodeType.End))
        {
            errors.Add(Field, "A workflow needs at least one END node.");
        }

        foreach (var duplicate in nodes.GroupBy(n => n.Code).Where(g => g.Count() > 1))
        {
            errors.Add(Field, $"'{duplicate.Key}' is used more than once.");
        }

        var byCode = nodes.GroupBy(n => n.Code).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        for (var i = 0; i < nodes.Count; i++)
        {
            Node(nodes[i], $"{Field}[{i}]", byCode, evaluator, errors);
        }

        foreach (var (code, position) in definition.Layout ?? [])
        {
            if (!byCode.ContainsKey(code))
            {
                errors.Add("definition.layout", $"Node '{code}' does not exist.");
            }
            else if (position is null || !double.IsFinite(position.X) || !double.IsFinite(position.Y) || position.X is < 0 or > MaxCanvas || position.Y is < 0 or > MaxCanvas)
            {
                errors.Add("definition.layout", $"The position of '{code}' must be between 0 and {MaxCanvas}.");
            }
        }

        if (!errors.HasErrors)
        {
            Reachability(nodes, byCode, errors);
        }
    }

    private static void Node(NodeDefinition node, string field, Dictionary<string, NodeDefinition> nodes, JsonLogicEvaluator evaluator, ValidationErrors errors)
    {
        CheckCode(node.Code, field + ".code", errors);
        errors.Required(field + ".nameAr", node.NameAr);
        errors.Required(field + ".nameEn", node.NameEn);
        switch (node.Type)
        {
            case NodeType.Start:
                Target(node.Next, field + ".next", nodes, errors, required: true);
                break;
            case NodeType.HumanTask:
                HumanTask(node, field, nodes, errors);
                break;
            case NodeType.ParallelSplit:
                if (node.Branches.Count < 2)
                {
                    errors.Add(field + ".branches", "A parallel split needs at least two branches.");
                }

                for (var b = 0; b < node.Branches.Count; b++)
                {
                    Target(node.Branches[b], $"{field}.branches[{b}]", nodes, errors, required: true);
                }

                break;
            case NodeType.Join:
                Target(node.Next, field + ".next", nodes, errors, required: true);
                if (node.Mode == JoinMode.Count && node.Count is not >= 1)
                {
                    errors.Add(field + ".count", "Give how many branches must arrive.");
                }

                break;
            case NodeType.Gateway:
                if (node.Routes.Count == 0)
                {
                    errors.Add(field + ".routes", "A gateway needs at least one route.");
                }

                for (var r = 0; r < node.Routes.Count; r++)
                {
                    var route = node.Routes[r];
                    if (route.When is null)
                    {
                        errors.Add($"{field}.routes[{r}].when", "Required.");
                    }

                    CheckRule(route.When, $"{field}.routes[{r}].when", evaluator, errors);
                    Target(route.To, $"{field}.routes[{r}].to", nodes, errors, required: true);
                }

                Target(node.Default, field + ".default", nodes, errors, required: true);
                break;
            case NodeType.Decision:
                errors.Required(field + ".ruleSet", node.RuleSet);
                if (node.Result is not null && !ResultName().IsMatch(node.Result))
                {
                    errors.Add(field + ".result", "Use letters, digits and _.");
                }

                foreach (var (name, rule) in node.Input)
                {
                    CheckRule(rule, $"{field}.input.{name}", evaluator, errors);
                }

                Target(node.Next, field + ".next", nodes, errors, required: true);
                break;
            case NodeType.Payment:
                CheckRule(node.Amount, field + ".amount", evaluator, errors);
                if (node.VatRate is < 0 or > 1)
                {
                    errors.Add(field + ".vatRate", "Must be between 0 and 1.");
                }

                if (node.DueDays is < 1 or > 90)
                {
                    errors.Add(field + ".dueDays", "Must be between 1 and 90.");
                }

                Target(node.Next, field + ".next", nodes, errors, required: true);
                Target(node.Expired, field + ".expired", nodes, errors, required: true);
                break;
            case NodeType.Visit:
                errors.Required(field + ".visitType", node.VisitType);
                CheckRule(node.Site, field + ".site", evaluator, errors);
                CheckRule(node.Location, field + ".location", evaluator, errors);
                errors.MaxLength(field + ".instructions", node.Instructions, 4000);
                if (node.DueDays is < 1 or > 90)
                {
                    errors.Add(field + ".dueDays", "Must be between 1 and 90.");
                }

                foreach (var (outcome, to) in node.Outcomes)
                {
                    CheckCode(outcome, $"{field}.outcomes", errors);
                    Target(to, $"{field}.outcomes.{outcome}", nodes, errors, required: true);
                }

                Target(node.Next, field + ".next", nodes, errors, required: true);
                Target(node.Cancelled, field + ".cancelled", nodes, errors, required: true);
                break;
            case NodeType.Document:
                errors.Required(field + ".documentType", node.DocumentType);
                if (node.Validity == ValidityMode.Extend && node.ValidityMonths is null)
                {
                    errors.Add(field + ".validityMonths", "Give the months to extend by.");
                }

                if (node.ValidityMonths is < 1 or > 240)
                {
                    errors.Add(field + ".validityMonths", "Must be between 1 and 240.");
                }

                CheckRule(node.Holder, field + ".holder", evaluator, errors);
                foreach (var (name, rule) in node.Data)
                {
                    CheckRule(rule, $"{field}.data.{name}", evaluator, errors);
                }

                if (node.Lines is { } lines)
                {
                    if (lines.Source is null)
                    {
                        errors.Add(field + ".lines.source", "Give the rule that lists the lines.");
                    }

                    CheckRule(lines.Source, field + ".lines.source", evaluator, errors);
                    if (lines.Fields.Count == 0)
                    {
                        errors.Add(field + ".lines.fields", "Give the fields of a line.");
                    }

                    foreach (var (name, rule) in lines.Fields)
                    {
                        CheckRule(rule, $"{field}.lines.fields.{name}", evaluator, errors);
                    }
                }

                Target(node.Next, field + ".next", nodes, errors, required: true);
                break;
            case NodeType.End:
                if (node.Outcome is null)
                {
                    errors.Add(field + ".outcome", "Choose APPROVED, REJECTED or CLOSED.");
                }

                break;
        }
    }

    private static void HumanTask(NodeDefinition node, string field, Dictionary<string, NodeDefinition> nodes, ValidationErrors errors)
    {
        if (node.Assignment is not { } assignment)
        {
            errors.Add(field + ".assignment", "Say who works on the task.");
        }
        else
        {
            switch (assignment.Target)
            {
                case TargetKind.Role:
                    errors.Required(field + ".assignment.role", assignment.Role);
                    break;
                case TargetKind.User:
                    errors.Required(field + ".assignment.user", assignment.User);
                    break;
                case TargetKind.PreviousActor:
                    if (assignment.Step is null || !nodes.TryGetValue(assignment.Step, out var step) || step.Type != NodeType.HumanTask)
                    {
                        errors.Add(field + ".assignment.step", "Name a human task of this workflow.");
                    }

                    break;
            }

            if (assignment.Mode == AssignmentMode.Distribute)
            {
                errors.Required(field + ".assignment.distributorRole", assignment.DistributorRole);
            }
        }

        if (node.Actions.Count == 0)
        {
            errors.Add(field + ".actions", "A task needs at least one action.");
        }

        foreach (var duplicate in node.Actions.GroupBy(a => a.Code).Where(g => g.Count() > 1))
        {
            errors.Add(field + ".actions", $"'{duplicate.Key}' is used more than once.");
        }

        for (var a = 0; a < node.Actions.Count; a++)
        {
            var action = node.Actions[a];
            var actionField = $"{field}.actions[{a}]";
            CheckCode(action.Code, actionField + ".code", errors);
            errors.Required(actionField + ".nameAr", action.NameAr);
            errors.Required(actionField + ".nameEn", action.NameEn);
            if (action.Kind == ActionKind.Return)
            {
                if (action.To is not null)
                {
                    errors.Add(actionField + ".to", "A return goes back to the applicant; use resumeAt for where it continues.");
                }

                if (action.ResumeAt is not null && (!nodes.TryGetValue(action.ResumeAt, out var resume) || resume.Type != NodeType.HumanTask))
                {
                    errors.Add(actionField + ".resumeAt", "Name a human task of this workflow.");
                }
            }
            else
            {
                Target(action.To, actionField + ".to", nodes, errors, required: true);
            }
        }

        foreach (var step in node.ExcludeActorsOf.Where(s => !nodes.TryGetValue(s, out var other) || other.Type != NodeType.HumanTask))
        {
            errors.Add(field + ".excludeActorsOf", $"'{step}' is not a human task of this workflow.");
        }

        if (node.Sla is { } sla)
        {
            if (sla.WorkingDays is < 1 or > 1000 || sla.ByLevel.Values.Any(d => d is < 1 or > 1000))
            {
                errors.Add(field + ".sla", "Working days must be between 1 and 1000.");
            }

            if (sla.WarnPercent is < 1 or > 99)
            {
                errors.Add(field + ".sla.warnPercent", "Must be between 1 and 99.");
            }
        }
    }

    private static void Target(string? code, string field, Dictionary<string, NodeDefinition> nodes, ValidationErrors errors, bool required)
    {
        if (code is null)
        {
            if (required)
            {
                errors.Add(field, "Required.");
            }
        }
        else if (!nodes.TryGetValue(code, out var target))
        {
            errors.Add(field, $"Node '{code}' does not exist.");
        }
        else if (target.Type == NodeType.Start)
        {
            errors.Add(field, "Nothing may lead back to the START node.");
        }
    }

    /// <summary>Every node must be reachable from START.</summary>
    private static void Reachability(List<NodeDefinition> nodes, Dictionary<string, NodeDefinition> byCode, ValidationErrors errors)
    {
        var start = nodes.Single(n => n.Type == NodeType.Start);
        var seen = new HashSet<string>(StringComparer.Ordinal) { start.Code };
        var pending = new Stack<NodeDefinition>([start]);
        while (pending.TryPop(out var node))
        {
            foreach (var next in Targets(node).Where(c => byCode.ContainsKey(c) && seen.Add(c)))
            {
                pending.Push(byCode[next]);
            }
        }

        foreach (var node in nodes.Where(n => !seen.Contains(n.Code)))
        {
            errors.Add(Field, $"Node '{node.Code}' cannot be reached from START.");
        }
    }

    internal static IEnumerable<string> Targets(NodeDefinition node) =>
        new[] { node.Next, node.Default, node.Expired, node.Cancelled }
            .Concat(node.Branches)
            .Concat(node.Outcomes.Values)
            .Concat(node.Routes.Select(r => r.To))
            .Concat(node.Actions.Select(a => a.To ?? a.ResumeAt ?? (a.Kind == ActionKind.Return ? node.Code : null)))
            .Where(c => c is not null)
            .Select(c => c!);

    private static void CheckRule(JsonNode? rule, string field, JsonLogicEvaluator evaluator, ValidationErrors errors)
    {
        if (rule is null)
        {
            return;
        }

        try
        {
            evaluator.Evaluate(rule, SampleContext);
        }
        catch (JsonLogicException ex)
        {
            errors.Add(field, $"Invalid rule: {ex.Message}");
        }
    }

    private static void CheckCode(string? code, string field, ValidationErrors errors)
    {
        if (string.IsNullOrEmpty(code) || !Code().IsMatch(code))
        {
            errors.Add(field, "Use upper-case letters, digits, _ and -, starting with a letter.");
        }
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_\\-]{0,50}$")]
    private static partial Regex Code();

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,63}$")]
    private static partial Regex ResultName();

    /// <summary>What rules see: the subject's facts, the last action of each task, decisions and documents so far.</summary>
    private static readonly JsonObject SampleContext = new()
    {
        ["data"] = new JsonObject(),
        ["level"] = null,
        ["party"] = new JsonObject(),
        ["actions"] = new JsonObject(),
        ["decisions"] = new JsonObject(),
        ["documents"] = new JsonObject(),
        ["payments"] = new JsonObject(),
        ["visits"] = new JsonObject(),
    };
}
