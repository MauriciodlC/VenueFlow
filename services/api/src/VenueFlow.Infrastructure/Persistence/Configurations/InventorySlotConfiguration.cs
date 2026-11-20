using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VenueFlow.Domain.Inventory;
using VenueFlow.Domain.Venues;

namespace VenueFlow.Infrastructure.Persistence.Configurations;

internal sealed class InventorySlotConfiguration : IEntityTypeConfiguration<InventorySlot>
{
    public void Configure(EntityTypeBuilder<InventorySlot> builder)
    {
        builder.ToTable("inventory_slots", table =>
        {
            table.HasCheckConstraint("ck_inventory_slots_interval", "end_utc > start_utc");
            table.HasCheckConstraint("ck_inventory_slots_total_capacity", "total_capacity >= 1");
        });

        builder.HasKey(slot => slot.Id).HasName("pk_inventory_slots");
        builder.Property(slot => slot.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(slot => slot.RoomId).HasColumnName("room_id");
        builder.Property(slot => slot.StartUtc).HasColumnName("start_utc");
        builder.Property(slot => slot.EndUtc).HasColumnName("end_utc");
        builder.Property(slot => slot.TotalCapacity).HasColumnName("total_capacity");

        // Holds update the slot row with `WHERE version = @seen`, so two racing
        // holds cannot both pass the capacity check.
        builder.Property(slot => slot.Version).HasColumnName("version").IsConcurrencyToken();

        // A room's start instant identifies its slot, so materialising the same
        // candidate twice fails instead of splitting capacity across two rows.
        // The index also serves range reads by room and start time.
        builder.HasIndex(slot => new { slot.RoomId, slot.StartUtc })
            .IsUnique()
            .HasDatabaseName("ux_inventory_slots_room_id_start_utc");

        // Slots with holds or bookings must not vanish with a room edit.
        builder.HasOne<Room>()
            .WithMany()
            .HasForeignKey(slot => slot.RoomId)
            .HasConstraintName("fk_inventory_slots_rooms_room_id")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
