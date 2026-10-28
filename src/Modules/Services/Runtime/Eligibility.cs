using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Platform.Expressions;
using DynamicCube.Platform.Parties;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>Why an applicant may not use a service.</summary>
public sealed record EligibilityFailure(string Code, string MessageAr, string MessageEn);

/// <summary>
/// Built-in checks (who may apply, relationship scope, blocking sanctions) followed by the version's eligibility rules.
/// A rule that cannot be evaluated counts as failed: eligibility never passes by accident.
/// </summary>
internal sealed class Eligibility(JsonLogicEvaluator evaluator)
{
    public IReadOnlyList<EligibilityFailure> Check(Service service, ResolvedVersion version, PartyFacts party, ActingParty acting, JsonObject context)
    {
        var failures = new List<EligibilityFailure>();
        if (!service.PartyKinds.Contains(acting.Kind))
        {
            failures.Add(acting.Kind == PartyKind.Individual
                ? new("PARTY_KIND", "هذه الخدمة متاحة للمنشآت فقط؛ اختر المنشأة التي تمثلها.", "This service is for establishments only; choose the establishment you act for.")
                : new("PARTY_KIND", "هذه الخدمة متاحة للأفراد فقط.", "This service is for individuals only."));
        }

        if (!acting.Covers(service.Code))
        {
            failures.Add(new("NOT_DELEGATED", "التفويض الممنوح لك لا يشمل هذه الخدمة.", "Your delegation does not cover this service."));
        }

        if (party.IsBlocked)
        {
            failures.Add(new("SANCTION", "يوجد جزاء ساري يمنع تقديم طلبات جديدة.", "A sanction in force bans new requests."));
        }

        foreach (var rule in version.Definition.Eligibility)
        {
            if (!Passes(rule.Rule, context))
            {
                failures.Add(new(rule.Code, rule.MessageAr, rule.MessageEn));
            }
        }

        return failures;
    }

    private bool Passes(JsonNode? rule, JsonObject context)
    {
        try
        {
            return evaluator.EvaluateTruthy(rule, context);
        }
        catch (JsonLogicException)
        {
            return false;
        }
    }
}
