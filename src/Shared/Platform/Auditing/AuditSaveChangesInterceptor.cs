using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using DynamicCube.Platform.Persistence;
using DynamicCube.Platform.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace DynamicCube.Platform.Auditing;

/// <summary>
/// Maintains audit columns and writes <see cref="AuditEntry"/> rows for <see cref="IAuditable"/> entities
/// in the same transaction as the change itself.
/// </summary>
public sealed class AuditSaveChangesInterceptor(ICurrentUser currentUser, TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            Capture(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            Capture(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Capture(DbContext context)
    {
        var now = timeProvider.GetUtcNow();
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        foreach (var entry in entries)
        {
            if (entry.Entity is IHasAuditColumns columns)
            {
                StampAuditColumns(entry, columns, now);
            }
        }

        foreach (var entry in entries)
        {
            if (entry.Entity is not IAuditable)
            {
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Created,
                EntityState.Deleted => AuditAction.Deleted,
                _ => AuditAction.Updated,
            };

            var changes = DescribeChanges(entry, action);
            if (action == AuditAction.Updated && changes.Count == 0)
            {
                continue;
            }

            context.Add(new AuditEntry
            {
                OccurredAt = now,
                UserId = currentUser.UserId,
                AppCode = currentUser.AppCode,
                EntityType = entry.Metadata.ClrType.Name,
                EntityId = DescribeKey(entry),
                Action = action,
                Changes = changes.ToJsonString(),
                CorrelationId = Activity.Current?.TraceId.ToString(),
            });
        }
    }

    private void StampAuditColumns(EntityEntry entry, IHasAuditColumns columns, DateTimeOffset now)
    {
        if (entry.State == EntityState.Added)
        {
            columns.CreatedAt = now;
            columns.CreatedBy = currentUser.UserId;
        }
        else if (entry.State == EntityState.Modified)
        {
            columns.UpdatedAt = now;
            columns.UpdatedBy = currentUser.UserId;
            entry.Property(nameof(IHasAuditColumns.CreatedAt)).IsModified = false;
            entry.Property(nameof(IHasAuditColumns.CreatedBy)).IsModified = false;
        }
    }

    private static JsonObject DescribeChanges(EntityEntry entry, AuditAction action)
    {
        var changes = new JsonObject();
        foreach (var property in entry.Properties)
        {
            if (property.Metadata.IsShadowProperty() && property.Metadata.IsConcurrencyToken)
            {
                continue;
            }

            var name = property.Metadata.Name;
            switch (action)
            {
                case AuditAction.Created:
                    changes[name] = new JsonObject { ["new"] = ToNode(property.CurrentValue) };
                    break;
                case AuditAction.Deleted:
                    changes[name] = new JsonObject { ["old"] = ToNode(property.OriginalValue) };
                    break;
                default:
                    if (property.IsModified && !Equals(property.OriginalValue, property.CurrentValue))
                    {
                        changes[name] = new JsonObject
                        {
                            ["old"] = ToNode(property.OriginalValue),
                            ["new"] = ToNode(property.CurrentValue),
                        };
                    }

                    break;
            }
        }

        return changes;
    }

    private static string DescribeKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return string.Empty;
        }

        return string.Join('|', key.Properties.Select(p => Convert.ToString(entry.Property(p.Name).CurrentValue, System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static JsonNode? ToNode(object? value) =>
        value is null ? null : JsonSerializer.SerializeToNode(value, value.GetType());
}
