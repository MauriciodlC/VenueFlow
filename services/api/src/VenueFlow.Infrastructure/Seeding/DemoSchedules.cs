using VenueFlow.Domain.Inventory;

namespace VenueFlow.Infrastructure.Seeding;

/// <summary>
/// Opening hours for the demo rooms, so the host calendar and slot listing
/// have something to show right after <c>seed</c>. Riverside (Europe/Madrid)
/// observes daylight-saving time; Harbor (America/Bogota) does not.
/// </summary>
public static class DemoSchedules
{
    public static readonly Guid WorkshopHallId = Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9e11");
    public static readonly Guid QuietRoomId = Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9e12");
    public static readonly Guid MainLoftId = Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9e21");
    public static readonly Guid MeetingPodId = Guid.Parse("5e0a1d7c-3b2f-4a8e-9c61-0d4f7b2a9e22");

    private static readonly DayOfWeek[] Weekdays =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    /// <summary>New schedules on every call, keyed by room id.</summary>
    public static IReadOnlyDictionary<Guid, RoomSchedule> Build()
    {
        var schedules = new Dictionary<Guid, RoomSchedule>
        {
            [WorkshopHallId] = Schedule(
                WorkshopHallId,
                [
                    .. Weekly(Weekdays, new TimeOnly(9, 0), new TimeOnly(18, 0), 60),
                    new OpeningRuleInput(DayOfWeek.Saturday, new TimeOnly(10, 0), new TimeOnly(14, 0), 120),
                ],
                [
                    new ScheduleExceptionInput(new DateOnly(2026, 12, 24), new TimeOnly(14, 0), new TimeOnly(18, 0), ScheduleExceptionKind.CapacityLimit, 10, "Reduced staff on Christmas Eve afternoon"),
                    new ScheduleExceptionInput(new DateOnly(2026, 12, 25), null, null, ScheduleExceptionKind.Closure, null, "Closed for Christmas"),
                ]),
            [QuietRoomId] = Schedule(QuietRoomId, Weekly(Weekdays, new TimeOnly(8, 0), new TimeOnly(20, 0), 30), []),
            [MainLoftId] = Schedule(
                MainLoftId,
                Weekly([.. Weekdays, DayOfWeek.Saturday], new TimeOnly(8, 0), new TimeOnly(20, 0), 120),
                [new ScheduleExceptionInput(new DateOnly(2026, 12, 8), null, null, ScheduleExceptionKind.Closure, null, "Public holiday")]),
            [MeetingPodId] = Schedule(MeetingPodId, Weekly(Weekdays, new TimeOnly(9, 0), new TimeOnly(17, 0), 60), []),
        };

        return schedules;
    }

    private static List<OpeningRuleInput> Weekly(IEnumerable<DayOfWeek> days, TimeOnly start, TimeOnly end, int slotMinutes) =>
        days.Select(day => new OpeningRuleInput(day, start, end, slotMinutes)).ToList();

    private static RoomSchedule Schedule(Guid roomId, IReadOnlyList<OpeningRuleInput> rules, IReadOnlyList<ScheduleExceptionInput> exceptions)
    {
        var schedule = RoomSchedule.Empty(roomId);
        schedule.Replace(rules, exceptions, Guid.CreateVersion7);
        return schedule;
    }
}
