namespace DynamicCube.Platform.ChangeSets;

/// <summary>
/// A group of configuration edits that goes through draft → submitted → approved → published,
/// with optional maker-checker (the approver must differ from the maker).
/// </summary>
public sealed class ChangeSet
{
    private readonly List<ChangeItem> _items = [];

    private ChangeSet()
    {
    }

    public Guid Id { get; private set; } = Guid.CreateVersion7();

    /// <summary>Configuration area, e.g. <c>apps.class-domain</c>; drives the maker-checker policy.</summary>
    public string Area { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public ChangeSetStatus Status { get; private set; }

    public bool RequiresApproval { get; private set; }

    public string CreatedBy { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public string? SubmittedBy { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public string? ReviewedBy { get; private set; }

    public DateTimeOffset? ReviewedAt { get; private set; }

    public string? ReviewComment { get; private set; }

    public string? PublishedBy { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public IReadOnlyList<ChangeItem> Items => _items;

    public static ChangeSet Create(string area, string title, bool requiresApproval, string userId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(area);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        return new ChangeSet
        {
            Area = area,
            Title = title,
            RequiresApproval = requiresApproval,
            Status = ChangeSetStatus.Draft,
            CreatedBy = userId,
            CreatedAt = now,
        };
    }

    public ChangeItem AddItem(string entityType, string entityCode, ChangeOperation operation, string? before, string? after)
    {
        EnsureStatus(ChangeSetStatus.Draft, "add items");
        ArgumentException.ThrowIfNullOrWhiteSpace(entityType);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityCode);

        if (operation != ChangeOperation.Delete && after is null)
        {
            throw new ArgumentException("Create and update items need an 'after' payload.", nameof(after));
        }

        var item = new ChangeItem(Id, _items.Count + 1, entityType, entityCode, operation, before, after);
        _items.Add(item);
        return item;
    }

    public void Submit(string userId, DateTimeOffset now)
    {
        EnsureStatus(ChangeSetStatus.Draft, "submit");
        if (_items.Count == 0)
        {
            throw new ChangeSetException("A change set without items cannot be submitted.");
        }

        SubmittedBy = userId;
        SubmittedAt = now;
        Status = RequiresApproval ? ChangeSetStatus.Submitted : ChangeSetStatus.Approved;
    }

    public void Approve(string userId, DateTimeOffset now, string? comment = null)
    {
        EnsureStatus(ChangeSetStatus.Submitted, "approve");
        if (string.Equals(userId, CreatedBy, StringComparison.Ordinal) || string.Equals(userId, SubmittedBy, StringComparison.Ordinal))
        {
            throw new ChangeSetException("Maker-checker: a change set cannot be approved by the user who created or submitted it.");
        }

        ReviewedBy = userId;
        ReviewedAt = now;
        ReviewComment = comment;
        Status = ChangeSetStatus.Approved;
    }

    public void Reject(string userId, DateTimeOffset now, string comment)
    {
        EnsureStatus(ChangeSetStatus.Submitted, "reject");
        ArgumentException.ThrowIfNullOrWhiteSpace(comment);

        ReviewedBy = userId;
        ReviewedAt = now;
        ReviewComment = comment;
        Status = ChangeSetStatus.Rejected;
    }

    /// <summary>Moves a rejected change set back to draft so the maker can correct it.</summary>
    public void Reopen()
    {
        EnsureStatus(ChangeSetStatus.Rejected, "reopen");
        Status = ChangeSetStatus.Draft;
        SubmittedBy = null;
        SubmittedAt = null;
    }

    internal void MarkPublished(string userId, DateTimeOffset now)
    {
        EnsureStatus(ChangeSetStatus.Approved, "publish");
        PublishedBy = userId;
        PublishedAt = now;
        Status = ChangeSetStatus.Published;
    }

    private void EnsureStatus(ChangeSetStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new ChangeSetException($"Cannot {action} a change set in status {Status}; expected {expected}.");
        }
    }
}

public enum ChangeSetStatus
{
    Draft,
    Submitted,
    Approved,
    Rejected,
    Published,
}

public enum ChangeOperation
{
    Create,
    Update,
    Delete,
}
