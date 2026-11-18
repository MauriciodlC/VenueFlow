using Microsoft.EntityFrameworkCore;
using VenueFlow.Domain.Venues;

namespace VenueFlow.Infrastructure.Persistence;

public sealed class VenueFlowDbContext(DbContextOptions<VenueFlowDbContext> options) : DbContext(options)
{
    public DbSet<Venue> Venues => Set<Venue>();

    public DbSet<Room> Rooms => Set<Room>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VenueFlowDbContext).Assembly);
    }
}
