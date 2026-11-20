using Microsoft.EntityFrameworkCore;
using VenueFlow.Domain.Accounts;
using VenueFlow.Domain.Inventory;
using VenueFlow.Domain.Venues;

namespace VenueFlow.Infrastructure.Persistence;

public sealed class VenueFlowDbContext(DbContextOptions<VenueFlowDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<Venue> Venues => Set<Venue>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<RoomSchedule> RoomSchedules => Set<RoomSchedule>();

    public DbSet<InventorySlot> InventorySlots => Set<InventorySlot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VenueFlowDbContext).Assembly);
    }
}
