using System.Globalization;
using VenueFlow.Domain;
using VenueFlow.Domain.Inventory;
using VenueFlow.Domain.Venues;

namespace VenueFlow.Api.Inventory;

/// <summary>A weekly opening window. Weekday is "monday" … "sunday"; times are local "HH:mm".</summary>
public sealed record OpeningRuleRequest(string? Weekday, string? StartLocal, string? EndLocal, int SlotMinutes);

/// <summary>
/// A dated closure or capacity limit. Kind is "closure" or "capacity_limit";
/// leave both times out for the whole day.
/// </summary>
public sealed record ScheduleExceptionRequest(
    string? Date,
    string? StartLocal,
    string? EndLocal,
    string? Kind,
    int? Capacity,
    string? Note);

/// <summary>Full replacement of a room's schedule. A missing list counts as empty.</summary>
public sealed record UpdateRoomScheduleRequest(
    IReadOnlyList<OpeningRuleRequest>? Rules,
    IReadOnlyList<ScheduleExceptionRequest>? Exceptions);

public sealed record OpeningRuleResponse(Guid Id, string Weekday, string StartLocal, string EndLocal, int SlotMinutes);

public sealed record ScheduleExceptionResponse(
    Guid Id,
    string Date,
    string? StartLocal,
    string? EndLocal,
    string Kind,
    int? Capacity,
    string? Note);

public sealed record RoomScheduleResponse(
    Guid VenueId,
    Guid RoomId,
    string RoomName,
    int RoomCapacity,
    string TimeZoneId,
    int Revision,
    IReadOnlyList<OpeningRuleResponse> Rules,
    IReadOnlyList<ScheduleExceptionResponse> Exceptions);

/// <summary>A room (bookable resource) with a summary of its schedule.</summary>
public sealed record HostRoomResponse(Guid Id, string Name, int Capacity, int RuleCount, int ExceptionCount, int ScheduleRevision);

/// <summary>
/// One slot candidate. Local fields are wall-clock times at the venue;
/// <c>startUtc</c>/<c>endUtc</c> are the instants. Not a reservation.
/// </summary>
public sealed record SlotResponse(
    string LocalDate,
    string LocalStart,
    string LocalEnd,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string UtcOffset,
    int Capacity,
    bool CapacityLimited);

public sealed record SkippedSlotResponse(string LocalDate, string LocalStart, string LocalEnd, string Reason);

public sealed record RoomSlotsResponse(
    Guid RoomId,
    string TimeZoneId,
    string From,
    string To,
    IReadOnlyList<SlotResponse> Slots,
    IReadOnlyList<SkippedSlotResponse> Skipped);

/// <summary>
/// Converts between the wire format (lower-case names, ISO dates, "HH:mm")
/// and domain values. Bad input raises a <see cref="DomainException"/> so it
/// gets the same 422 problem response as any other rule violation.
/// </summary>
internal static class ScheduleWire
{
    private const string DateFormat = "yyyy-MM-dd";
    private const string TimeFormat = "HH:mm";

    public static OpeningRuleInput ToInput(OpeningRuleRequest request) =>
        new(ParseWeekday(request.Weekday), ParseTime(request.StartLocal, "start_local"), ParseTime(request.EndLocal, "end_local"), request.SlotMinutes);

    public static ScheduleExceptionInput ToInput(ScheduleExceptionRequest request) =>
        new(
            ParseDate(request.Date, "date"),
            ParseOptionalTime(request.StartLocal, "start_local"),
            ParseOptionalTime(request.EndLocal, "end_local"),
            ParseKind(request.Kind),
            request.Capacity,
            request.Note);

    public static RoomScheduleResponse ToResponse(Venue venue, Room room, RoomSchedule schedule) =>
        new(
            venue.Id,
            room.Id,
            room.Name,
            room.Capacity,
            venue.TimeZoneId,
            schedule.Revision,
            schedule.Rules
                .OrderBy(rule => WeekdayOrder(rule.Weekday))
                .ThenBy(rule => rule.StartLocal)
                .Select(rule => new OpeningRuleResponse(rule.Id, FormatWeekday(rule.Weekday), FormatTime(rule.StartLocal), FormatTime(rule.EndLocal), rule.SlotMinutes))
                .ToList(),
            schedule.Exceptions
                .OrderBy(exception => exception.Date)
                .ThenBy(exception => exception.StartLocal)
                .Select(exception => new ScheduleExceptionResponse(
                    exception.Id,
                    FormatDate(exception.Date),
                    exception.StartLocal is { } start ? FormatTime(start) : null,
                    exception.EndLocal is { } end ? FormatTime(end) : null,
                    FormatKind(exception.Kind),
                    exception.Capacity,
                    exception.Note))
                .ToList());

    public static RoomSlotsResponse ToResponse(Guid roomId, string timeZoneId, DateOnly from, DateOnly to, ScheduleExpansion expansion) =>
        new(
            roomId,
            timeZoneId,
            FormatDate(from),
            FormatDate(to),
            expansion.Slots
                .Select(slot => new SlotResponse(
                    FormatDate(slot.LocalDate),
                    FormatTime(slot.LocalStart),
                    FormatTime(slot.LocalEnd),
                    slot.StartUtc,
                    slot.EndUtc,
                    FormatOffset(slot.UtcOffset),
                    slot.Capacity,
                    slot.CapacityLimited))
                .ToList(),
            expansion.Skipped
                .Select(skipped => new SkippedSlotResponse(FormatDate(skipped.LocalDate), FormatTime(skipped.LocalStart), FormatTime(skipped.LocalEnd), skipped.Reason))
                .ToList());

    public static DateOnly? ParseOptionalDate(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseDate(value, field);

    public static string FormatDate(DateOnly date) => date.ToString(DateFormat, CultureInfo.InvariantCulture);

    public static string FormatTime(TimeOnly time) => time.ToString(TimeFormat, CultureInfo.InvariantCulture);

    /// <summary>"+02:00", "-05:00", "+05:30".</summary>
    public static string FormatOffset(TimeSpan offset) =>
        (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    public static string FormatWeekday(DayOfWeek weekday) => weekday.ToString().ToLowerInvariant();

    public static string FormatKind(ScheduleExceptionKind kind) => kind switch
    {
        ScheduleExceptionKind.Closure => "closure",
        ScheduleExceptionKind.CapacityLimit => "capacity_limit",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    private static DayOfWeek ParseWeekday(string? value)
    {
        var name = value?.Trim().ToLowerInvariant();
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            if (FormatWeekday(day) == name)
            {
                return day;
            }
        }

        throw new DomainException("weekday_invalid", "Weekday must be one of monday to sunday.");
    }

    private static ScheduleExceptionKind ParseKind(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "closure" => ScheduleExceptionKind.Closure,
        "capacity_limit" => ScheduleExceptionKind.CapacityLimit,
        _ => throw new DomainException("exception_kind_invalid", "Exception kind must be closure or capacity_limit."),
    };

    private static DateOnly ParseDate(string? value, string field) =>
        DateOnly.TryParseExact(value?.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new DomainException($"{field}_invalid", $"{field} must be a date like 2026-03-29.");

    private static TimeOnly ParseTime(string? value, string field) =>
        TimeOnly.TryParseExact(value?.Trim(), TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? time
            : throw new DomainException($"{field}_invalid", $"{field} must be a local time like 09:30.");

    private static TimeOnly? ParseOptionalTime(string? value, string field) =>
        string.IsNullOrWhiteSpace(value) ? null : ParseTime(value, field);

    // Monday first, as the host calendar shows the week.
    private static int WeekdayOrder(DayOfWeek weekday) => ((int)weekday + 6) % 7;
}
