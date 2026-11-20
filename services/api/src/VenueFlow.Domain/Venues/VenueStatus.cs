namespace VenueFlow.Domain.Venues;

/// <summary>
/// Publication lifecycle of a venue listing. Stored by name.
/// <code>
/// Draft --submit--> InReview --approve--> Published
///   ^                  |
///   +---return---------+
/// </code>
/// Only drafts can be edited; guests only ever see published venues.
/// </summary>
public enum VenueStatus
{
    Draft,
    InReview,
    Published,
}
