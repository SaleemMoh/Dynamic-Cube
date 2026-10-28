using DynamicCube.Platform.ChangeSets;
using Shouldly;

namespace DynamicCube.Platform.Tests;

public sealed class ChangeSetTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Approval_required_flow_reaches_published()
    {
        var changeSet = NewChangeSet(requiresApproval: true);
        changeSet.AddItem("apps.domain", "SECTOR.ICT", ChangeOperation.Create, null, "{}");

        changeSet.Submit("maker", Now);
        changeSet.Status.ShouldBe(ChangeSetStatus.Submitted);

        changeSet.Approve("checker", Now);
        changeSet.Status.ShouldBe(ChangeSetStatus.Approved);

        changeSet.MarkPublished("checker", Now);
        changeSet.Status.ShouldBe(ChangeSetStatus.Published);
    }

    [Fact]
    public void Submit_without_approval_policy_goes_straight_to_approved()
    {
        var changeSet = NewChangeSet(requiresApproval: false);
        changeSet.AddItem("apps.ui-text", "HOME.TITLE", ChangeOperation.Update, "{}", "{}");

        changeSet.Submit("maker", Now);

        changeSet.Status.ShouldBe(ChangeSetStatus.Approved);
    }

    [Theory]
    [InlineData("maker")]
    [InlineData("submitter")]
    public void Maker_or_submitter_cannot_approve(string approver)
    {
        var changeSet = NewChangeSet(requiresApproval: true);
        changeSet.AddItem("apps.domain", "SECTOR.ICT", ChangeOperation.Create, null, "{}");
        changeSet.Submit("submitter", Now);

        Should.Throw<ChangeSetException>(() => changeSet.Approve(approver, Now));
    }

    [Fact]
    public void Empty_change_set_cannot_be_submitted()
    {
        var changeSet = NewChangeSet(requiresApproval: true);

        Should.Throw<ChangeSetException>(() => changeSet.Submit("maker", Now));
    }

    [Fact]
    public void Items_cannot_be_added_after_submit()
    {
        var changeSet = NewChangeSet(requiresApproval: true);
        changeSet.AddItem("apps.domain", "A", ChangeOperation.Create, null, "{}");
        changeSet.Submit("maker", Now);

        Should.Throw<ChangeSetException>(() => changeSet.AddItem("apps.domain", "B", ChangeOperation.Create, null, "{}"));
    }

    [Fact]
    public void Rejected_change_set_can_be_reopened_and_resubmitted()
    {
        var changeSet = NewChangeSet(requiresApproval: true);
        changeSet.AddItem("apps.domain", "A", ChangeOperation.Create, null, "{}");
        changeSet.Submit("maker", Now);
        changeSet.Reject("checker", Now, "Wrong Arabic label");

        changeSet.Reopen();
        changeSet.Status.ShouldBe(ChangeSetStatus.Draft);
        changeSet.AddItem("apps.domain", "B", ChangeOperation.Create, null, "{}");
        changeSet.Submit("maker", Now);

        changeSet.Status.ShouldBe(ChangeSetStatus.Submitted);
        changeSet.Items.Select(i => i.Sequence).ShouldBe([1, 2]);
    }

    [Fact]
    public void Create_item_requires_after_payload()
    {
        var changeSet = NewChangeSet(requiresApproval: true);

        Should.Throw<ArgumentException>(() => changeSet.AddItem("apps.domain", "A", ChangeOperation.Create, null, null));
    }

    [Fact]
    public void Publish_requires_approved_status()
    {
        var changeSet = NewChangeSet(requiresApproval: true);
        changeSet.AddItem("apps.domain", "A", ChangeOperation.Create, null, "{}");
        changeSet.Submit("maker", Now);

        Should.Throw<ChangeSetException>(() => changeSet.MarkPublished("checker", Now));
    }

    [Fact]
    public void Options_resolve_area_policy_with_default()
    {
        var options = new ChangeSetOptions { RequireApprovalByDefault = true };
        options.Areas["apps.ui-text"] = false;

        options.RequiresApproval("apps.ui-text").ShouldBeFalse();
        options.RequiresApproval("APPS.UI-TEXT").ShouldBeFalse();
        options.RequiresApproval("apps.roles").ShouldBeTrue();
    }

    private static ChangeSet NewChangeSet(bool requiresApproval) =>
        ChangeSet.Create("apps.class-domain", "Add ICT sector", requiresApproval, "maker", Now);
}
