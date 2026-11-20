using VenueFlow.Domain;
using VenueFlow.Domain.Venues;

namespace VenueFlow.Tests.Domain;

/// <summary>
/// Every (status, action) pair of the publication lifecycle. Only three moves
/// are allowed; everything else must fail with a stable code and leave the
/// venue exactly as it was.
/// </summary>
public sealed class VenueStatusTransitionTests
{
    private static readonly Guid VenueId = Guid.Parse("7c2d9e40-1b6a-4c8f-a3e5-2d7f0b9c4e01");
    private static readonly Guid HostId = Guid.Parse("7c2d9e40-1b6a-4c8f-a3e5-2d7f0b9c4e99");

    public enum LifecycleAction
    {
        Submit,
        Approve,
        ReturnToDraft,
    }

    private static Venue InStatus(VenueStatus status)
    {
        var venue = Venue.Create(VenueId, HostId, "Studio North", "Europe/Madrid");
        venue.EditDraft(
            "Studio North",
            "Europe/Madrid",
            "Bright studio with long tables.",
            "Madrid",
            "Calle Falsa 1 (synthetic)",
            "Step-free entrance.",
            [new RoomInput(null, "Main room", 16)],
            Guid.NewGuid);

        if (status is VenueStatus.InReview or VenueStatus.Published)
        {
            venue.SubmitForReview();
        }

        if (status is VenueStatus.Published)
        {
            venue.Approve();
        }

        Assert.Equal(status, venue.Status);
        return venue;
    }

    private static void Apply(Venue venue, LifecycleAction action)
    {
        switch (action)
        {
            case LifecycleAction.Submit:
                venue.SubmitForReview();
                break;
            case LifecycleAction.Approve:
                venue.Approve();
                break;
            case LifecycleAction.ReturnToDraft:
                venue.ReturnToDraft();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(action), action, null);
        }
    }

    [Theory]
    [InlineData(VenueStatus.Draft, LifecycleAction.Submit, VenueStatus.InReview)]
    [InlineData(VenueStatus.InReview, LifecycleAction.Approve, VenueStatus.Published)]
    [InlineData(VenueStatus.InReview, LifecycleAction.ReturnToDraft, VenueStatus.Draft)]
    public void Allowed_transitions_change_the_status_and_bump_the_revision(VenueStatus from, LifecycleAction action, VenueStatus to)
    {
        var venue = InStatus(from);
        var revision = venue.Revision;

        Apply(venue, action);

        Assert.Equal(to, venue.Status);
        Assert.Equal(revision + 1, venue.Revision);
    }

    [Theory]
    [InlineData(VenueStatus.Draft, LifecycleAction.Approve)]
    [InlineData(VenueStatus.Draft, LifecycleAction.ReturnToDraft)]
    [InlineData(VenueStatus.InReview, LifecycleAction.Submit)]
    [InlineData(VenueStatus.Published, LifecycleAction.Submit)]
    [InlineData(VenueStatus.Published, LifecycleAction.Approve)]
    [InlineData(VenueStatus.Published, LifecycleAction.ReturnToDraft)]
    public void Invalid_transitions_are_rejected_and_change_nothing(VenueStatus from, LifecycleAction action)
    {
        var venue = InStatus(from);
        var revision = venue.Revision;

        var error = Assert.Throws<DomainException>(() => Apply(venue, action));

        Assert.Equal("venue_status_transition_invalid", error.Code);
        Assert.Equal(from, venue.Status);
        Assert.Equal(revision, venue.Revision);
    }

    [Theory]
    [InlineData(VenueStatus.InReview)]
    [InlineData(VenueStatus.Published)]
    public void Venues_past_draft_cannot_be_edited(VenueStatus status)
    {
        var venue = InStatus(status);
        var revision = venue.Revision;

        var error = Assert.Throws<DomainException>(() =>
            venue.EditDraft("Renamed", "UTC", null, null, null, null, rooms: null, Guid.NewGuid));

        Assert.Equal("venue_not_editable", error.Code);
        Assert.Equal("Studio North", venue.Name);
        Assert.Equal(revision, venue.Revision);
    }

    [Fact]
    public void A_returned_draft_can_be_edited_and_submitted_again()
    {
        var venue = InStatus(VenueStatus.InReview);
        venue.ReturnToDraft();

        venue.EditDraft("Studio North v2", "Europe/Madrid", "Updated.", "Madrid", "Calle Falsa 1", null, rooms: null, Guid.NewGuid);
        venue.SubmitForReview();

        Assert.Equal(VenueStatus.InReview, venue.Status);
        Assert.Equal("Studio North v2", venue.Name);
    }
}
