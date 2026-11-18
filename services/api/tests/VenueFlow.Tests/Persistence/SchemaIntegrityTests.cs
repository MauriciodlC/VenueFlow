using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
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
}
