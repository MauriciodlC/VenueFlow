using VenueFlow.Domain;
using VenueFlow.Domain.Venues;

namespace VenueFlow.Tests.Domain;

public sealed class VenueDraftTests
{
    private static readonly Guid VenueId = Guid.Parse("3f4c2b10-8a1e-4d7b-9c3a-1e2f3a4b5c01");
    private static readonly Guid HostId = Guid.Parse("3f4c2b10-8a1e-4d7b-9c3a-1e2f3a4b5c99");

    private static Venue NewDraft() => Venue.Create(VenueId, HostId, "Studio North", "Europe/Madrid");

    private static Venue CompleteDraft()
    {
        var venue = NewDraft();
        venue.UpdateDetails("Studio North", "Europe/Madrid", "Bright studio with long tables.", "Madrid", "Calle Falsa 1", "Step-free entrance");
        venue.AddRoom(Guid.Parse("3f4c2b10-8a1e-4d7b-9c3a-1e2f3a4b5c11"), "Main room", 16);
        return venue;
    }

    [Fact]
    public void Create_starts_a_draft_owned_by_the_host_at_revision_one()
    {
        var venue = NewDraft();

        Assert.Equal(VenueStatus.Draft, venue.Status);
        Assert.Equal(HostId, venue.HostId);
        Assert.Equal(1, venue.Revision);
        Assert.True(venue.IsOwnedBy(HostId));
        Assert.False(venue.IsOwnedBy(Guid.NewGuid()));
        Assert.False(venue.IsOwnedBy(Guid.Empty));
    }

    [Fact]
    public void Create_requires_a_host()
    {
        var error = Assert.Throws<DomainException>(() => Venue.Create(VenueId, Guid.Empty, "Studio", "UTC"));

        Assert.Equal("venue_host_required", error.Code);
    }

    [Fact]
    public void UpdateDetails_trims_blanks_to_null_and_bumps_the_revision()
    {
        var venue = NewDraft();

        venue.UpdateDetails(" Studio South ", "America/Bogota", "  ", " Bogota ", null, "Lift to all floors");

        Assert.Equal("Studio South", venue.Name);
        Assert.Equal("America/Bogota", venue.TimeZoneId);
        Assert.Null(venue.Description);
        Assert.Equal("Bogota", venue.City);
        Assert.Null(venue.AddressLine);
        Assert.Equal("Lift to all floors", venue.AccessibilityNotes);
        Assert.Equal(2, venue.Revision);
    }

    [Fact]
    public void UpdateDetails_changes_nothing_when_a_field_is_invalid()
    {
        var venue = NewDraft();

        var error = Assert.Throws<DomainException>(() =>
            venue.UpdateDetails("Renamed", "UTC", new string('x', Venue.DescriptionMaxLength + 1), null, null, null));

        Assert.Equal("description_too_long", error.Code);
        Assert.Equal("Studio North", venue.Name);
        Assert.Equal(1, venue.Revision);
    }

    [Fact]
    public void MissingForSubmission_lists_fields_in_form_order()
    {
        Assert.Equal(new[] { "description", "city", "address_line", "rooms" }, NewDraft().MissingForSubmission());
        Assert.Empty(CompleteDraft().MissingForSubmission());
    }

    [Fact]
    public void ReplaceRooms_updates_adds_and_removes_rooms()
    {
        var venue = NewDraft();
        var keepId = Guid.Parse("3f4c2b10-8a1e-4d7b-9c3a-1e2f3a4b5c21");
        var dropId = Guid.Parse("3f4c2b10-8a1e-4d7b-9c3a-1e2f3a4b5c22");
        var addedId = Guid.Parse("3f4c2b10-8a1e-4d7b-9c3a-1e2f3a4b5c23");
        venue.AddRoom(keepId, "Loft", 10);
        venue.AddRoom(dropId, "Cellar", 6);

        venue.ReplaceRooms([new RoomInput(keepId, "Big loft", 12), new RoomInput(null, "Garden", 20)], () => addedId);

        Assert.Collection(
            venue.Rooms.OrderBy(room => room.Name),
            room => Assert.Equal((keepId, "Big loft", 12), (room.Id, room.Name, room.Capacity)),
            room => Assert.Equal((addedId, "Garden", 20), (room.Id, room.Name, room.Capacity)));
        Assert.All(venue.Rooms, room => Assert.Equal(VenueId, room.VenueId));
    }

    [Theory]
    [InlineData("Loft", "loft", "room_name_taken")]
    [InlineData("Loft", "", "room_name_required")]
    public void ReplaceRooms_is_all_or_nothing(string first, string second, string code)
    {
        var venue = NewDraft();
        var existing = venue.AddRoom(Guid.NewGuid(), "Original", 8);
        var revision = venue.Revision;

        var error = Assert.Throws<DomainException>(() =>
            venue.ReplaceRooms([new RoomInput(existing.Id, first, 4), new RoomInput(null, second, 4)], Guid.NewGuid));

        Assert.Equal(code, error.Code);
        Assert.Equal("Original", Assert.Single(venue.Rooms).Name);
        Assert.Equal(revision, venue.Revision);
    }

    [Fact]
    public void ReplaceRooms_rejects_ids_from_another_venue()
    {
        var venue = NewDraft();

        var error = Assert.Throws<DomainException>(() =>
            venue.ReplaceRooms([new RoomInput(Guid.NewGuid(), "Loft", 4)], Guid.NewGuid));

        Assert.Equal("room_not_found", error.Code);
    }

    [Fact]
    public void Submit_requires_a_complete_draft()
    {
        var venue = NewDraft();

        var error = Assert.Throws<DomainException>(venue.SubmitForReview);

        Assert.Equal("venue_incomplete", error.Code);
        Assert.Equal(VenueStatus.Draft, venue.Status);
    }

    [Fact]
    public void Draft_moves_through_review_to_published()
    {
        var venue = CompleteDraft();

        venue.SubmitForReview();
        Assert.Equal(VenueStatus.InReview, venue.Status);

        venue.ReturnToDraft();
        Assert.Equal(VenueStatus.Draft, venue.Status);

        venue.SubmitForReview();
        venue.Approve();
        Assert.Equal(VenueStatus.Published, venue.Status);
    }

    [Fact]
    public void Only_drafts_can_be_edited()
    {
        var venue = CompleteDraft();
        venue.SubmitForReview();

        Assert.Equal("venue_not_editable",
            Assert.Throws<DomainException>(() => venue.UpdateDetails("X", "UTC", null, null, null, null)).Code);
        Assert.Equal("venue_not_editable",
            Assert.Throws<DomainException>(() => venue.AddRoom(Guid.NewGuid(), "Annex", 4)).Code);
        Assert.Equal("venue_not_editable",
            Assert.Throws<DomainException>(() => venue.ReplaceRooms([], Guid.NewGuid)).Code);
    }

    [Fact]
    public void A_draft_cannot_be_approved_directly()
    {
        var venue = CompleteDraft();

        var error = Assert.Throws<DomainException>(venue.Approve);

        Assert.Equal("venue_status_transition_invalid", error.Code);
        Assert.Equal(VenueStatus.Draft, venue.Status);
    }

    [Fact]
    public void EditDraft_changes_details_and_rooms_in_one_revision()
    {
        var venue = NewDraft();

        venue.EditDraft("Studio North", "Europe/Madrid", "Desc", "Madrid", "Street 1", null, [new RoomInput(null, "Hall", 30)], Guid.NewGuid);

        Assert.Equal(2, venue.Revision);
        Assert.Empty(venue.MissingForSubmission());
    }

    [Fact]
    public void EditDraft_with_a_bad_room_leaves_the_details_unchanged()
    {
        var venue = NewDraft();

        Assert.Throws<DomainException>(() =>
            venue.EditDraft("Renamed", "UTC", "Desc", null, null, null, [new RoomInput(null, "Hall", 0)], Guid.NewGuid));

        Assert.Equal("Studio North", venue.Name);
        Assert.Null(venue.Description);
        Assert.Equal(1, venue.Revision);
    }
}
