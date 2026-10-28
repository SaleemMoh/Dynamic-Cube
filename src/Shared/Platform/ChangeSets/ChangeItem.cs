namespace DynamicCube.Platform.ChangeSets;

/// <summary>One create / update / delete of a configuration entity within a <see cref="ChangeSet"/>.</summary>
public sealed class ChangeItem
{
    internal ChangeItem(Guid changeSetId, int sequence, string entityType, string entityCode, ChangeOperation operation, string? before, string? after)
    {
        ChangeSetId = changeSetId;
        Sequence = sequence;
        EntityType = entityType;
        EntityCode = entityCode;
        Operation = operation;
        Before = before;
        After = after;
    }

    private ChangeItem()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid ChangeSetId { get; private set; }

    /// <summary>Order in which items are applied on publish.</summary>
    public int Sequence { get; private set; }

    /// <summary>Applier key, e.g. <c>apps.domain</c>.</summary>
    public string EntityType { get; private set; } = string.Empty;

    /// <summary>Business code of the entity (configuration is identified by code, not id).</summary>
    public string EntityCode { get; private set; } = string.Empty;

    public ChangeOperation Operation { get; private set; }

    /// <summary>JSON snapshot before the change (null for create).</summary>
    public string? Before { get; private set; }

    /// <summary>JSON snapshot after the change (null for delete).</summary>
    public string? After { get; private set; }

    internal void Resequence(int sequence) => Sequence = sequence;
}
