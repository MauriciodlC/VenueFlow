using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VenueFlow.Domain.Inventory;
using VenueFlow.Domain.Venues;

namespace VenueFlow.Infrastructure.Persistence.Configurations;

internal sealed class RoomScheduleConfiguration : IEntityTypeConfiguration<RoomSchedule>
{
    public void Configure(EntityTypeBuilder<RoomSchedule> builder)
    {
        builder.ToTable("room_schedules");

        builder.HasKey(schedule => schedule.RoomId).HasName("pk_room_schedules");
        builder.Property(schedule => schedule.RoomId).HasColumnName("room_id").ValueGeneratedNever();

        // A save that replaces rules or exceptions also bumps the revision, so two
        // editors of the same calendar cannot overwrite each other.
        builder.Property(schedule => schedule.Revision).HasColumnName("revision").IsConcurrencyToken();

        builder.HasOne<Room>()
            .WithOne()
            .HasForeignKey<RoomSchedule>(schedule => schedule.RoomId)
            .HasConstraintName("fk_room_schedules_rooms_room_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(schedule => schedule.Rules)
            .WithOne()
            .HasForeignKey(rule => rule.RoomId)
            .HasConstraintName("fk_opening_rules_room_schedules_room_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(schedule => schedule.Exceptions)
            .WithOne()
            .HasForeignKey(exception => exception.RoomId)
            .HasConstraintName("fk_schedule_exceptions_room_schedules_room_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(schedule => schedule.Rules).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(schedule => schedule.Exceptions).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
