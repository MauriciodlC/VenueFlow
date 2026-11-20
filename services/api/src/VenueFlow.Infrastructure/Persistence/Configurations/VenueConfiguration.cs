using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VenueFlow.Domain.Accounts;
using VenueFlow.Domain.Venues;

namespace VenueFlow.Infrastructure.Persistence.Configurations;

internal sealed class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public const int StatusMaxLength = 16;

    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.ToTable("venues", table =>
            table.HasCheckConstraint("ck_venues_status", "status IN ('Draft', 'InReview', 'Published')"));

        builder.HasKey(venue => venue.Id).HasName("pk_venues");
        builder.Property(venue => venue.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(venue => venue.HostId).HasColumnName("host_id");
        builder.Property(venue => venue.Name).HasColumnName("name").HasMaxLength(Venue.NameMaxLength).IsRequired();
        builder.Property(venue => venue.TimeZoneId)
            .HasColumnName("time_zone_id")
            .HasMaxLength(Venue.TimeZoneIdMaxLength)
            .IsRequired();
        builder.Property(venue => venue.Description).HasColumnName("description").HasMaxLength(Venue.DescriptionMaxLength);
        builder.Property(venue => venue.City).HasColumnName("city").HasMaxLength(Venue.CityMaxLength);
        builder.Property(venue => venue.AddressLine).HasColumnName("address_line").HasMaxLength(Venue.AddressLineMaxLength);
        builder.Property(venue => venue.AccessibilityNotes)
            .HasColumnName("accessibility_notes")
            .HasMaxLength(Venue.AccessibilityNotesMaxLength);
        builder.Property(venue => venue.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(StatusMaxLength)
            .IsRequired();

        // Every update checks the revision the editor started from; a stale
        // write fails with DbUpdateConcurrencyException instead of overwriting.
        builder.Property(venue => venue.Revision).HasColumnName("revision").IsConcurrencyToken();

        builder.HasOne<UserAccount>()
            .WithMany()
            .HasForeignKey(venue => venue.HostId)
            .HasConstraintName("fk_venues_users_host_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(venue => venue.HostId).HasDatabaseName("ix_venues_host_id");

        builder.HasMany(venue => venue.Rooms)
            .WithOne()
            .HasForeignKey(room => room.VenueId)
            .HasConstraintName("fk_rooms_venues_venue_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(venue => venue.Rooms).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
