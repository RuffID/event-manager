using EventManager.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventManager.Api.DataAccess.Configurations
{
    /// <summary>Настраивает хранение бронирований и связь с событием.</summary>
    public class BookingConfiguration : IEntityTypeConfiguration<Booking>
    {
        /// <summary>Описывает таблицу, свойства и навигации бронирования.</summary>
        /// <param name="builder">Построитель конфигурации бронирования.</param>
        public void Configure(EntityTypeBuilder<Booking> builder)
        {
            builder.ToTable("bookings");
            builder.HasKey(entity => entity.Id);
            builder.Property(entity => entity.Id).ValueGeneratedNever();

            builder.Property(entity => entity.EventId).IsRequired();
            builder.Property(entity => entity.Status)
                .HasField("_status")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .HasConversion<string>()
                .HasMaxLength(nameof(BookingStatus.Confirmed).Length)
                .IsRequired();
            builder.Property(entity => entity.CreatedAt).IsRequired();
            builder.Property(entity => entity.ProcessedAt)
                .HasField("_processedAt")
                .UsePropertyAccessMode(PropertyAccessMode.Field)
                .IsRequired(false);

            builder.HasOne(entity => entity.Event)
                .WithMany(entity => entity.Bookings)
                .HasForeignKey(entity => entity.EventId)
                .IsRequired();
        }
    }
}
