using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using VenueFlow.Domain.Accounts;
using VenueFlow.Domain.Inventory;
using VenueFlow.Domain.Venues;
using VenueFlow.Infrastructure.Persistence;

namespace VenueFlow.Tests.Persistence;

/// <summary>
/// Checks the EF Core model and the migration snapshot carry the database-level
/// guarantees the domain relies on. No database connection is opened.
/// </summary>
public sealed class SchemaIntegrityTests : IDisposable
{
    private readonly VenueFlowDbContext _db = new(
        new DbContextOptionsBuilder<VenueFlowDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=venueflow_test;Username=test")
            .Options);

    private IModel Model => _db.GetService<IDesignTimeModel>().Model;

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Venues_and_rooms_map_to_named_tables_and_keys()
    {
        var venue = Model.FindEntityType(typeof(Venue))!;
        var room = Model.FindEntityType(typeof(Room))!;

        Assert.Equal("venues", venue.GetTableName());
        Assert.Equal("pk_venues", venue.FindPrimaryKey()!.GetName());
        Assert.Equal("rooms", room.GetTableName());
        Assert.Equal("pk_rooms", room.FindPrimaryKey()!.GetName());
    }

    [Fact]
    public void Column_lengths_match_domain_limits()
    {
        var venue = Model.FindEntityType(typeof(Venue))!;
        var room = Model.FindEntityType(typeof(Room))!;

        Assert.Equal(Venue.NameMaxLength, venue.FindProperty(nameof(Venue.Name))!.GetMaxLength());
        Assert.Equal(Venue.TimeZoneIdMaxLength, venue.FindProperty(nameof(Venue.TimeZoneId))!.GetMaxLength());
        Assert.Equal(Room.NameMaxLength, room.FindProperty(nameof(Room.Name))!.GetMaxLength());
        Assert.False(venue.FindProperty(nameof(Venue.TimeZoneId))!.IsNullable);
    }

    [Fact]
    public void Room_names_are_unique_per_venue()
    {
        var room = Model.FindEntityType(typeof(Room))!;

        var index = Assert.Single(room.GetIndexes(), candidate => candidate.GetDatabaseName() == "ux_rooms_venue_id_name");
        Assert.True(index.IsUnique);
        Assert.Equal(new[] { nameof(Room.VenueId), nameof(Room.Name) }, index.Properties.Select(property => property.Name));
    }

    [Fact]
    public void Room_capacity_is_checked_by_the_database()
    {
        var room = Model.FindEntityType(typeof(Room))!;

        var check = Assert.Single(room.GetCheckConstraints());
        Assert.Equal("ck_rooms_capacity_range", check.Name);
        Assert.Equal($"capacity BETWEEN 1 AND {Room.MaxCapacity}", check.Sql);
    }

    [Fact]
    public void Deleting_a_venue_cascades_to_its_rooms()
    {
        var room = Model.FindEntityType(typeof(Room))!;

        var foreignKey = Assert.Single(room.GetForeignKeys());
        Assert.Equal(typeof(Venue), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        Assert.Equal("fk_rooms_venues_venue_id", foreignKey.GetConstraintName());
    }

    [Fact]
    public void Migration_snapshot_keeps_the_capacity_check_and_unique_room_index()
    {
        Assert.Contains("20261118160000_InitialVenuesAndRooms", _db.Database.GetMigrations());

        var snapshot = _db.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);

        var room = snapshot.Model.FindEntityType(typeof(Room).FullName!)!;
        Assert.Contains(room.GetCheckConstraints(), check => check.Name == "ck_rooms_capacity_range");
        Assert.Contains(room.GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "ux_rooms_venue_id_name");
    }

    [Fact]
    public void Users_have_unique_emails_and_roles_stored_by_name()
    {
        var user = Model.FindEntityType(typeof(UserAccount))!;

        Assert.Equal("users", user.GetTableName());
        var email = Assert.Single(user.GetIndexes(), index => index.GetDatabaseName() == "ux_users_email");
        Assert.True(email.IsUnique);
        Assert.Equal(typeof(string), user.FindProperty(nameof(UserAccount.Role))!.GetProviderClrType());
        Assert.Equal("ck_users_role", Assert.Single(user.GetCheckConstraints()).Name);
        Assert.Null(user.FindProperty(nameof(UserAccount.IsHost)));
    }

    [Fact]
    public void Venues_belong_to_a_host_and_cannot_orphan_on_user_delete()
    {
        var venue = Model.FindEntityType(typeof(Venue))!;

        var host = Assert.Single(venue.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(UserAccount));
        Assert.Equal("fk_venues_users_host_id", host.GetConstraintName());
        Assert.Equal(DeleteBehavior.Restrict, host.DeleteBehavior);
        Assert.True(host.IsRequired);
        Assert.Contains(venue.GetIndexes(), index => index.GetDatabaseName() == "ix_venues_host_id");
    }

    [Fact]
    public void Venue_revision_is_the_concurrency_token_and_status_is_checked()
    {
        var venue = Model.FindEntityType(typeof(Venue))!;

        Assert.True(venue.FindProperty(nameof(Venue.Revision))!.IsConcurrencyToken);
        Assert.Equal(typeof(string), venue.FindProperty(nameof(Venue.Status))!.GetProviderClrType());
        Assert.Equal("ck_venues_status", Assert.Single(venue.GetCheckConstraints()).Name);
        Assert.True(venue.FindProperty(nameof(Venue.Description))!.IsNullable);
    }

    [Fact]
    public void Check_constraints_list_every_enum_value()
    {
        var userCheck = Assert.Single(Model.FindEntityType(typeof(UserAccount))!.GetCheckConstraints()).Sql;
        var venueCheck = Assert.Single(Model.FindEntityType(typeof(Venue))!.GetCheckConstraints()).Sql;

        Assert.All(Enum.GetNames<UserRole>(), name => Assert.Contains($"'{name}'", userCheck, StringComparison.Ordinal));
        Assert.All(Enum.GetNames<VenueStatus>(), name => Assert.Contains($"'{name}'", venueCheck, StringComparison.Ordinal));
    }

    [Fact]
    public void Migration_snapshot_includes_host_accounts_and_venue_drafts()
    {
        Assert.Contains("20261120143000_HostAccountsAndVenueDrafts", _db.Database.GetMigrations());

        var snapshot = _db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        var venue = snapshot.FindEntityType(typeof(Venue).FullName!)!;
        Assert.NotNull(snapshot.FindEntityType(typeof(UserAccount).FullName!));
        Assert.True(venue.FindProperty(nameof(Venue.Revision))!.IsConcurrencyToken);
        Assert.Contains(venue.GetCheckConstraints(), check => check.Name == "ck_venues_status");
    }

    [Fact]
    public void Inventory_tables_have_named_keys()
    {
        Assert.Equal(("room_schedules", "pk_room_schedules"), TableAndKey<RoomSchedule>());
        Assert.Equal(("opening_rules", "pk_opening_rules"), TableAndKey<OpeningRule>());
        Assert.Equal(("schedule_exceptions", "pk_schedule_exceptions"), TableAndKey<ScheduleException>());
        Assert.Equal(("inventory_slots", "pk_inventory_slots"), TableAndKey<InventorySlot>());
    }

    [Fact]
    public void A_room_has_at_most_one_schedule_and_it_goes_with_the_room()
    {
        var schedule = Model.FindEntityType(typeof(RoomSchedule))!;

        var room = Assert.Single(schedule.GetForeignKeys());
        Assert.Equal(typeof(Room), room.PrincipalEntityType.ClrType);
        Assert.True(room.IsUnique);
        Assert.Equal(DeleteBehavior.Cascade, room.DeleteBehavior);
        Assert.Equal(new[] { nameof(RoomSchedule.RoomId) }, schedule.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.True(schedule.FindProperty(nameof(RoomSchedule.Revision))!.IsConcurrencyToken);
    }

    [Fact]
    public void Rules_and_exceptions_are_indexed_for_expansion_and_cascade_with_the_schedule()
    {
        var rule = Model.FindEntityType(typeof(OpeningRule))!;
        var exception = Model.FindEntityType(typeof(ScheduleException))!;

        Assert.Equal(
            new[] { nameof(OpeningRule.RoomId), nameof(OpeningRule.Weekday) },
            Assert.Single(rule.GetIndexes(), index => index.GetDatabaseName() == "ix_opening_rules_room_id_weekday").Properties.Select(property => property.Name));
        Assert.Equal(
            new[] { nameof(ScheduleException.RoomId), nameof(ScheduleException.Date) },
            Assert.Single(exception.GetIndexes(), index => index.GetDatabaseName() == "ix_schedule_exceptions_room_id_date").Properties.Select(property => property.Name));

        foreach (var entity in new[] { rule, exception })
        {
            var parent = Assert.Single(entity.GetForeignKeys());
            Assert.Equal(typeof(RoomSchedule), parent.PrincipalEntityType.ClrType);
            Assert.Equal(DeleteBehavior.Cascade, parent.DeleteBehavior);
        }
    }

    [Fact]
    public void Rule_and_exception_columns_use_local_time_and_date_types()
    {
        var rule = Model.FindEntityType(typeof(OpeningRule))!;
        var exception = Model.FindEntityType(typeof(ScheduleException))!;

        Assert.Equal("time without time zone", rule.FindProperty(nameof(OpeningRule.StartLocal))!.GetRelationalTypeMapping().StoreType);
        Assert.Equal("integer", rule.FindProperty(nameof(OpeningRule.Weekday))!.GetRelationalTypeMapping().StoreType);
        Assert.Equal("date", exception.FindProperty(nameof(ScheduleException.Date))!.GetRelationalTypeMapping().StoreType);
        Assert.True(exception.FindProperty(nameof(ScheduleException.StartLocal))!.IsNullable);
        Assert.Equal(typeof(string), exception.FindProperty(nameof(ScheduleException.Kind))!.GetProviderClrType());
        Assert.Null(exception.FindProperty(nameof(ScheduleException.IsWholeDay)));
    }

    [Fact]
    public void Schedule_check_constraints_cover_windows_weekdays_kinds_and_capacity()
    {
        var ruleChecks = Model.FindEntityType(typeof(OpeningRule))!.GetCheckConstraints().ToDictionary(check => check.Name!, check => check.Sql);
        var exceptionChecks = Model.FindEntityType(typeof(ScheduleException))!.GetCheckConstraints().ToDictionary(check => check.Name!, check => check.Sql);

        Assert.Equal("weekday BETWEEN 0 AND 6", ruleChecks["ck_opening_rules_weekday"]);
        Assert.Equal("end_local > start_local", ruleChecks["ck_opening_rules_window"]);
        Assert.Equal(
            $"slot_minutes BETWEEN {OpeningRule.MinSlotMinutes} AND {OpeningRule.MaxSlotMinutes}",
            ruleChecks["ck_opening_rules_slot_minutes"]);
        Assert.All(Enum.GetNames<ScheduleExceptionKind>(), name => Assert.Contains($"'{name}'", exceptionChecks["ck_schedule_exceptions_kind"], StringComparison.Ordinal));
        Assert.Contains("end_local > start_local", exceptionChecks["ck_schedule_exceptions_window"], StringComparison.Ordinal);
        Assert.Contains($"capacity BETWEEN 1 AND {Room.MaxCapacity}", exceptionChecks["ck_schedule_exceptions_capacity"], StringComparison.Ordinal);
    }

    [Fact]
    public void Inventory_slots_are_unique_per_room_and_start_and_versioned()
    {
        var slot = Model.FindEntityType(typeof(InventorySlot))!;

        var unique = Assert.Single(slot.GetIndexes(), index => index.GetDatabaseName() == "ux_inventory_slots_room_id_start_utc");
        Assert.True(unique.IsUnique);
        Assert.Equal(new[] { nameof(InventorySlot.RoomId), nameof(InventorySlot.StartUtc) }, unique.Properties.Select(property => property.Name));
        Assert.True(slot.FindProperty(nameof(InventorySlot.Version))!.IsConcurrencyToken);
        Assert.Equal("timestamp with time zone", slot.FindProperty(nameof(InventorySlot.StartUtc))!.GetRelationalTypeMapping().StoreType);

        var room = Assert.Single(slot.GetForeignKeys());
        Assert.Equal(typeof(Room), room.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, room.DeleteBehavior);
        Assert.Equal(
            new[] { "ck_inventory_slots_interval", "ck_inventory_slots_total_capacity" },
            slot.GetCheckConstraints().Select(check => check.Name).Order());
    }

    [Fact]
    public void Migration_snapshot_includes_schedules_and_slots()
    {
        Assert.Equal("20261120170000_RoomSchedulesAndInventorySlots", _db.Database.GetMigrations().Last());

        var snapshot = _db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        foreach (var type in new[] { typeof(RoomSchedule), typeof(OpeningRule), typeof(ScheduleException), typeof(InventorySlot) })
        {
            Assert.NotNull(snapshot.FindEntityType(type.FullName!));
        }

        var slot = snapshot.FindEntityType(typeof(InventorySlot).FullName!)!;
        Assert.Contains(slot.GetIndexes(), index => index.IsUnique && index.GetDatabaseName() == "ux_inventory_slots_room_id_start_utc");
        var exception = snapshot.FindEntityType(typeof(ScheduleException).FullName!)!;
        Assert.Equal(3, exception.GetCheckConstraints().Count());
    }

    private (string? Table, string? Key) TableAndKey<T>()
    {
        var entity = Model.FindEntityType(typeof(T))!;
        return (entity.GetTableName(), entity.FindPrimaryKey()!.GetName());
    }
}
