using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VenueFlow.Domain.Inventory;

namespace VenueFlow.Infrastructure.Persistence.Configurations;

internal sealed class OpeningRuleConfiguration : IEntityTypeConfiguration<OpeningRule>
{
    public void Configure(EntityTypeBuilder<OpeningRule> builder)
    {
        // Weekday uses .NET numbering: 0 = Sunday ... 6 = Saturday.
        builder.ToTable("opening_rules", table =>
        {
            table.HasCheckConstraint("ck_opening_rules_weekday", "weekday BETWEEN 0 AND 6");
            table.HasCheckConstraint("ck_opening_rules_window", "end_local > start_local");
            table.HasCheckConstraint(
                "ck_opening_rules_slot_minutes",
                $"slot_minutes BETWEEN {OpeningRule.MinSlotMinutes} AND {OpeningRule.MaxSlotMinutes}");
        });

        builder.HasKey(rule => rule.Id).HasName("pk_opening_rules");
        builder.Property(rule => rule.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(rule => rule.RoomId).HasColumnName("room_id");
        builder.Property(rule => rule.Weekday).HasColumnName("weekday").HasConversion<int>();
        builder.Property(rule => rule.StartLocal).HasColumnName("start_local");
        builder.Property(rule => rule.EndLocal).HasColumnName("end_local");
        builder.Property(rule => rule.SlotMinutes).HasColumnName("slot_minutes");

        // Slot expansion reads one room's rules for the weekdays in range.
        builder.HasIndex(rule => new { rule.RoomId, rule.Weekday }).HasDatabaseName("ix_opening_rules_room_id_weekday");
    }
}
