namespace DynamicCube.Modules.Decisions.Changes;

/// <summary>Entity types, change-set area and guarding resources of decision rule sets.</summary>
public static class DecisionChangeTypes
{
    public const string RuleSet = "dec.rule-set";
    public const string Version = "dec.rule-set-version";

    public const string Area = "dec.decisions";
}

public static class DecisionResources
{
    /// <summary>Rule sets and their versions; VIEW also allows trying a version out.</summary>
    public const string Config = "decisions.config";

    /// <summary>Recorded evaluations with their traces.</summary>
    public const string Evaluations = "decisions.evaluations";
}
