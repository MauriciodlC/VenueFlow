namespace VenueFlow.Domain.Inventory;

/// <summary>One weekly opening window as sent by the schedule editor.</summary>
public sealed record OpeningRuleInput(DayOfWeek Weekday, TimeOnly StartLocal, TimeOnly EndLocal, int SlotMinutes);

/// <summary>One dated closure or capacity limit as sent by the schedule editor.</summary>
public sealed record ScheduleExceptionInput(
    DateOnly Date,
    TimeOnly? StartLocal,
    TimeOnly? EndLocal,
    ScheduleExceptionKind Kind,
    int? Capacity,
    string? Note);
