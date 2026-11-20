using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VenueFlow.Domain.Inventory;
using VenueFlow.Domain.Venues;

namespace VenueFlow.Infrastructure.Persistence.Configurations;

internal sealed class ScheduleExceptionConfiguration : IEntityTypeConfiguration<ScheduleException>
{
    public const int KindMaxLength = 16;

    public void Configure(EntityTypeBuilder<ScheduleException> builder)
    {
        builder.ToTable("schedule_exceptions", table =>
        {
            table.HasCheckConstraint("ck_schedule_exceptions_kind", "kind IN ('Closure', 'CapacityLimit')");
            table.HasCheckConstraint(
                "ck_schedule_exceptions_window",
                "(start_local IS NULL AND end_local IS NULL) OR (start_local IS NOT NULL AND end_local IS NOT NULL AND end_local > start_local)");
            table.HasCheckConstraint(
                "ck_schedule_exceptions_capacity",
                $"(kind = 'Closure' AND capacity IS NULL) OR (kind = 'CapacityLimit' AND capacity BETWEEN 1 AND {Room.MaxCapacity})");
        });

        builder.HasKey(exception => exception.Id).HasName("pk_schedule_exceptions");
        builder.Property(exception => exception.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(exception => exception.RoomId).HasColumnName("room_id");
        builder.Property(exception => exception.Date).HasColumnName("date");
        builder.Property(exception => exception.StartLocal).HasColumnName("start_local");
        builder.Property(exception => exception.EndLocal).HasColumnName("end_local");
        builder.Property(exception => exception.Kind)
            .HasColumnName("kind")
            .HasConversion<string>()
            .HasMaxLength(KindMaxLength)
            .IsRequired();
        builder.Property(exception => exception.Capacity).HasColumnName("capacity");
        builder.Property(exception => exception.Note).HasColumnName("note").HasMaxLength(ScheduleException.NoteMaxLength);

        builder.Ignore(exception => exception.IsWholeDay);

        // Slot expansion reads one room's exceptions for a date range.
        builder.HasIndex(exception => new { exception.RoomId, exception.Date }).HasDatabaseName("ix_schedule_exceptions_room_id_date");
    }
}
