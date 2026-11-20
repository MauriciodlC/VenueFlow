namespace VenueFlow.Domain.Inventory;

/// <summary>
/// The bookable hours of one room: weekly <see cref="OpeningRule"/>s plus dated
/// <see cref="ScheduleException"/>s, all in the venue's local time. The host
/// edits it as a whole, and <see cref="Revision"/> guards each save the same
/// way a venue's revision does.
/// </summary>
public sealed class RoomSchedule
{
    public const int MaxRules = 28;
    public const int MaxExceptions = 366;

    private readonly List<OpeningRule> _rules = [];
    private readonly List<ScheduleException> _exceptions = [];

    private RoomSchedule()
    {
        // Used by EF Core.
    }

    /// <summary>The room this schedule belongs to; also the schedule's key.</summary>
    public Guid RoomId { get; private set; }

    /// <summary>0 for a schedule that has never been saved.</summary>
    public int Revision { get; private set; }

    public IReadOnlyCollection<OpeningRule> Rules => _rules.AsReadOnly();

    public IReadOnlyCollection<ScheduleException> Exceptions => _exceptions.AsReadOnly();

    /// <summary>An empty schedule: the room offers no slots until rules are added.</summary>
    public static RoomSchedule Empty(Guid roomId)
    {
        if (roomId == Guid.Empty)
        {
            throw new DomainException("room_id_required", "A schedule needs a room.");
        }

        return new RoomSchedule { RoomId = roomId, Revision = 0 };
    }

    /// <summary>
    /// Replaces every rule and exception. All input is checked first; if
    /// anything is invalid the schedule is left exactly as it was.
    /// </summary>
    public void Replace(IReadOnlyList<OpeningRuleInput> rules, IReadOnlyList<ScheduleExceptionInput> exceptions, Func<Guid> newId)
    {
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(exceptions);
        ArgumentNullException.ThrowIfNull(newId);

        if (rules.Count > MaxRules)
        {
            throw new DomainException("too_many_rules", $"A room can have at most {MaxRules} opening rules.");
        }

        if (exceptions.Count > MaxExceptions)
        {
            throw new DomainException("too_many_exceptions", $"A room can have at most {MaxExceptions} exceptions.");
        }

        var newRules = rules.Select(input => new OpeningRule(newId(), RoomId, input)).ToList();
        var newExceptions = exceptions.Select(input => new ScheduleException(newId(), RoomId, input)).ToList();

        for (var i = 0; i < newRules.Count; i++)
        {
            for (var j = i + 1; j < newRules.Count; j++)
            {
                if (newRules[i].Overlaps(newRules[j]))
                {
                    throw new DomainException(
                        "opening_rules_overlap",
                        $"Two {newRules[i].Weekday} opening windows overlap ({newRules[i].StartLocal:HH:mm}-{newRules[i].EndLocal:HH:mm} and {newRules[j].StartLocal:HH:mm}-{newRules[j].EndLocal:HH:mm}).");
                }
            }
        }

        _rules.Clear();
        _rules.AddRange(newRules.OrderBy(rule => rule.Weekday).ThenBy(rule => rule.StartLocal));
        _exceptions.Clear();
        _exceptions.AddRange(newExceptions.OrderBy(exception => exception.Date).ThenBy(exception => exception.StartLocal));
        Revision++;
    }
}
