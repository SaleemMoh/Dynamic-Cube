using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Modules.Documents.Changes;
using DynamicCube.Modules.Documents.Persistence;
using DynamicCube.Platform.PublicApi;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Documents.Runtime;

/// <summary>
/// Checks a document by its number (ADR-0042): type, holder, status, dates and the fields its type makes public — what the
/// QR verification page shows. A document outside the caller's companies and branches is not found.
/// </summary>
internal sealed class DocumentVerifyFunction(DocumentsDbContext context) : IApiFunction
{
    public const string Code = "DOCUMENT_VERIFY";

    public ApiFunctionInfo Info { get; } = new(
        Code,
        "التحقق من وثيقة",
        "Verify a document",
        "بيانات الوثيقة برقمها كما تظهر في صفحة التحقق: النوع والحامل والحالة والتواريخ والحقول العامة.",
        "A document by its number, as the verification page shows it: type, holder, status, dates and its public fields.",
        [new("number", true, "رقم الوثيقة", "The document number")],
        new JsonObject
        {
            ["number"] = "CERT-2026-000045",
            ["type"] = new JsonObject { ["code"] = "CLASSIFICATION_CERT", ["nameAr"] = "شهادة تصنيف", ["nameEn"] = "Classification certificate" },
            ["holder"] = new JsonObject { ["nameAr"] = "شركة البناء المتقدم", ["nameEn"] = "Advanced Building Co." },
            ["status"] = "ACTIVE",
            ["issuedAt"] = "2026-10-05T10:00:00+03:00",
            ["expiresAt"] = "2027-10-05T10:00:00+03:00",
            ["version"] = 1,
            ["fields"] = new JsonObject { ["GRADE"] = "A" },
        });

    public async Task<ApiFunctionResult> InvokeAsync(IReadOnlyDictionary<string, string?> inputs, ApiCaller caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(caller);
        var number = inputs.GetValueOrDefault("number")?.Trim();
        if (string.IsNullOrEmpty(number))
        {
            throw new ApiInputException(new Dictionary<string, string[]> { ["number"] = ["The document number is required."] });
        }

        var document = await context.Documents.AsNoTracking().SingleOrDefaultAsync(d => d.Number == number, cancellationToken);
        if (document is null || !caller.Sees(document.CompanyCode, document.BranchCode))
        {
            return ApiFunctionResult.NotFound;
        }

        var type = await context.DocumentTypes.AsNoTracking().SingleAsync(t => t.Id == document.DocumentTypeId, cancellationToken);
        var data = await context.Versions.AsNoTracking().Where(v => v.DocumentId == document.Id && v.Version == document.CurrentVersion)
            .Select(v => v.Data).SingleOrDefaultAsync(cancellationToken);
        var values = (data is null ? null : JsonNode.Parse(data) as JsonObject) ?? [];
        return new ApiFunctionResult(new JsonObject
        {
            ["number"] = document.Number,
            ["type"] = new JsonObject { ["code"] = type.Code, ["nameAr"] = type.NameAr, ["nameEn"] = type.NameEn },
            ["holder"] = new JsonObject { ["nameAr"] = document.HolderNameAr, ["nameEn"] = document.HolderNameEn },
            ["status"] = JsonNamingPolicy.SnakeCaseUpper.ConvertName(document.Status.ToString()),
            ["issuedAt"] = document.IssuedAt,
            ["expiresAt"] = document.ExpiresAt,
            ["version"] = document.CurrentVersion,
            ["fields"] = new JsonObject(DocumentTypeApplier.Fields(type.Fields).Where(f => f.IsPublic)
                .Select(f => KeyValuePair.Create(f.Code, values[f.Code]?.DeepClone()))),
        });
    }
}
