namespace DynamicCube.Modules.Decisions.Domain;

/// <summary>A decision (e.g. classification grading) computed by an engine from a versioned configuration.</summary>
public sealed class RuleSet : ConfigEntity
{
    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    /// <summary>Code of the <see cref="Platform.Decisions.IDecisionEngine"/> that evaluates it.</summary>
    public string Engine { get; set; } = string.Empty;

    /// <summary>Anyone may evaluate it without signing in (a public calculator on the portal).</summary>
    public bool IsPublic { get; set; }
}

/// <summary>One numbered configuration of a rule set (<c>CODE/N</c>); published versions never change.</summary>
public sealed class RuleSetVersion : ConfigEntity
{
    public Guid RuleSetId { get; set; }

    public RuleSet? RuleSet { get; set; }

    public int Number { get; set; }

    /// <summary>The engine's configuration (JSON).</summary>
    public string Configuration { get; set; } = "{}";

    public DateTimeOffset? PublishedAt { get; set; }

    public bool IsRetired { get; set; }
}

/// <summary>A recorded evaluation: inputs, results, trace and the version used, for reviewers, appeals and certificates.</summary>
public sealed class Evaluation
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string RuleSetCode { get; set; } = string.Empty;

    public int Version { get; set; }

    public string Engine { get; set; } = string.Empty;

    public string SubjectType { get; set; } = string.Empty;

    public Guid SubjectId { get; set; }

    public string? SubjectNumber { get; set; }

    public string Input { get; set; } = "{}";

    public string Output { get; set; } = "{}";

    public string Trace { get; set; } = "[]";

    public DateTimeOffset At { get; set; }

    public string By { get; set; } = string.Empty;

    public int DurationMs { get; set; }
}
