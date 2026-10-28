using System.Text.Json;
using DynamicCube.Modules.Services.Definitions;
using DynamicCube.Modules.Services.Persistence;
using DynamicCube.Modules.Services.Runtime;
using DynamicCube.Platform.ChangeSets;
using Microsoft.EntityFrameworkCore;

namespace DynamicCube.Modules.Services.Changes;

/// <summary>
/// When a change set publishes new service versions, stores each one's resolved form: the definition plus snapshots of
/// the questions it uses (as published at that moment, including questions created in the same change set).
/// </summary>
internal sealed class VersionCompiler(TimeProvider clock, ServiceCatalogCache cache) : IChangeSetPublishHook<ServicesDbContext>
{
    public async Task OnPublishedAsync(ServicesDbContext context, ChangeSet changeSet, CancellationToken cancellationToken)
    {
        var created = changeSet.Items
            .Where(i => i.EntityType == ServiceChangeTypes.Version && i.Operation == ChangeOperation.Create)
            .Select(i => i.EntityCode)
            .ToList();
        if (created.Count > 0)
        {
            var versions = await context.Versions.Include(v => v.Service).Where(v => created.Contains(v.Code)).ToListAsync(cancellationToken);
            foreach (var version in versions)
            {
                var definition = JsonSerializer.Deserialize<VersionDefinition>(version.Definition, ChangeJson.Options)!;
                var codes = definition.Steps.SelectMany(s => s.Sections).SelectMany(s => s.Questions).Select(q => q.Code).Distinct().ToList();
                var questions = await context.Questions.AsNoTracking().Where(q => codes.Contains(q.Code)).ToListAsync(cancellationToken);
                var missing = codes.Except(questions.Select(q => q.Code)).ToList();
                if (missing.Count > 0)
                {
                    throw new ChangeSetException($"Version {version.Code} uses questions that do not exist: {string.Join(", ", missing)}.");
                }

                var resolved = new ResolvedVersion(version.Service!.Code, version.Number, definition, questions.ToDictionary(q => q.Code, QuestionSnapshot.Of));
                version.Resolved = JsonSerializer.Serialize(resolved, ChangeJson.Options);
                version.PublishedAt = clock.GetUtcNow();
            }

            await context.SaveChangesAsync(cancellationToken);
        }

        cache.Clear();
    }
}
