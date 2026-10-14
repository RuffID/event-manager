using EventManager.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventManager.Api.DataAccess.Configurations
{
    /// <summary>Настраивает хранение событий и связь с бронированиями.</summary>
    public class EventConfiguration : IEntityTypeConfiguration<Event>
    {
        /// <summary>Описывает таблицу, свойства и навигации события.</summary>
        /// <param name="builder">Построитель конфигурации события.</param>
        public void Configure(EntityTypeBuilder<Event> builder)
        {
            builder.ToTable("events");
            builder.HasKey(entity => entity.Id);
            builder.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();

            builder.Property(entity => entity.Title).HasColumnName("title").HasMaxLength(Event.MAX_TITLE_LENGTH).IsRequired();
            builder.Property(entity => entity.Description).HasColumnName("description").HasMaxLength(Event.MAX_DESCRIPTION_LENGTH).IsRequired(false);
            builder.Property(entity => entity.StartAt).HasColumnName("start_at").IsRequired();
            builder.Property(entity => entity.EndAt).HasColumnName("end_at").IsRequired();
            builder.Property(entity => entity.TotalSeats).HasColumnName("total_seats").IsRequired();
            builder.Property(entity => entity.AvailableSeats)
                .HasColumnName("available_seats")
                .HasField("_availableSeats")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .IsRequired();

            builder.HasMany(entity => entity.Bookings)
                .WithOne(entity => entity.Event)
                .HasForeignKey(entity => entity.EventId)
                .IsRequired();

            builder.Navigation(entity => entity.Bookings)
                .HasField("_bookings")
                .UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
