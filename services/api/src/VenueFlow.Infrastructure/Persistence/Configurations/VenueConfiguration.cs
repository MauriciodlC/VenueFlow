using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VenueFlow.Domain.Venues;

namespace VenueFlow.Infrastructure.Persistence.Configurations;

internal sealed class VenueConfiguration : IEntityTypeConfiguration<Venue>
{
    public void Configure(EntityTypeBuilder<Venue> builder)
    {
        builder.ToTable("venues");

        builder.HasKey(venue => venue.Id).HasName("pk_venues");
        builder.Property(venue => venue.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(venue => venue.Name).HasColumnName("name").HasMaxLength(Venue.NameMaxLength).IsRequired();
        builder.Property(venue => venue.TimeZoneId)
            .HasColumnName("time_zone_id")
            .HasMaxLength(Venue.TimeZoneIdMaxLength)
            .IsRequired();

        builder.HasMany(venue => venue.Rooms)
            .WithOne()
            .HasForeignKey(room => room.VenueId)
            .HasConstraintName("fk_rooms_venues_venue_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(venue => venue.Rooms).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
