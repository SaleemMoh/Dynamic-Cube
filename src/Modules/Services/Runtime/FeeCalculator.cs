using System.Globalization;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Domain;
using DynamicCube.Platform.Expressions;

namespace DynamicCube.Modules.Services.Runtime;

/// <summary>
/// Fee lines for a level and the current answers. A bracket or formula whose input is not answered yet gives an
/// estimate (the lowest row, or zero) so the applicant sees the fee range early; submission recalculates.
/// </summary>
internal sealed class FeeCalculator(JsonLogicEvaluator evaluator)
{
    public IReadOnlyList<FeeLine> Compute(VersionDefinition definition, string? level, JsonObject context)
    {
        var lines = new List<FeeLine>();
        foreach (var fee in definition.Fees)
        {
            if (fee.Levels.Count > 0 && (level is null || !fee.Levels.Contains(level)))
            {
                continue;
            }

            if (fee.When is not null && !evaluator.EvaluateTruthy(fee.When, context))
            {
                continue;
            }

            var (amount, estimate) = Amount(fee, context);
            if (amount <= 0)
            {
                continue;
            }

            var (net, vat, total) = fee.VatIncluded
                ? (Round(amount / (1 + fee.VatRate)), Round(amount - Round(amount / (1 + fee.VatRate))), Round(amount))
                : (Round(amount), Round(amount * fee.VatRate), Round(amount + Round(amount * fee.VatRate)));
            lines.Add(new FeeLine(fee.Code, fee.NameAr, fee.NameEn, net, vat, total, estimate));
        }

        return lines;
    }

    private (decimal Amount, bool Estimate) Amount(FeeDefinition fee, JsonObject context)
    {
        if (fee.Fixed is { } fixedAmount)
        {
            return (fixedAmount, false);
        }

        if (fee.Formula is not null)
        {
            var value = Number(evaluator.Evaluate(fee.Formula, context));
            return value is null ? (0, true) : (value.Value, false);
        }

        var bracket = fee.Bracket!;
        var of = Number(evaluator.Evaluate(bracket.Of, context));
        if (of is null)
        {
            return (bracket.Rows.Count == 0 ? 0 : bracket.Rows.Min(r => r.Amount), true);
        }

        var row = bracket.Rows.FirstOrDefault(r => of >= r.From && (r.To is null || of < r.To));
        return (row?.Amount ?? 0, false);
    }

    private static decimal? Number(JsonNode? node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<decimal>(out var d))
        {
            return d;
        }

        if (value.TryGetValue<double>(out var f) && double.IsFinite(f))
        {
            return (decimal)f;
        }

        return value.TryGetValue<string>(out var s) && decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
