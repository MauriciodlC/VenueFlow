using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VenueFlow.Domain.Venues;

namespace VenueFlow.Infrastructure.Persistence.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms", table =>
            table.HasCheckConstraint("ck_rooms_capacity_range", $"capacity BETWEEN 1 AND {Room.MaxCapacity}"));

        builder.HasKey(room => room.Id).HasName("pk_rooms");
        builder.Property(room => room.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(room => room.VenueId).HasColumnName("venue_id");
        builder.Property(room => room.Name).HasColumnName("name").HasMaxLength(Room.NameMaxLength).IsRequired();
        builder.Property(room => room.Capacity).HasColumnName("capacity");

        builder.HasIndex(room => new { room.VenueId, room.Name })
            .IsUnique()
            .HasDatabaseName("ux_rooms_venue_id_name");
    }
}
