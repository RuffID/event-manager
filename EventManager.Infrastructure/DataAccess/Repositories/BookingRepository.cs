using EventManager.Application.Abstractions.Persistence;
using EventManager.Domain.Entities;
using EventManager.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EventManager.Infrastructure.DataAccess.Repositories
{
    /// <summary>Выполняет запросы и регистрирует изменения бронирований в общем scoped-контексте.</summary>
    /// <param name="context">Контекст текущей единицы работы.</param>
    public class BookingRepository(AppDbContext context) : IBookingRepository
    {
        /// <inheritdoc />
        public Task<Booking?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return context.Bookings.AsNoTracking()
                .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Booking?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            Booking? booking = await context.Bookings.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
            if (booking is null)
            {
                return null;
            }

            await context.Entry(booking).ReloadAsync(cancellationToken);
            if (context.Entry(booking).State == EntityState.Detached)
            {
                return null;
            }

            return booking;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Guid>> GetPendingIdsAsync(CancellationToken cancellationToken)
        {
            List<Guid> ids = await context.Bookings.AsNoTracking()
                .Where(booking => booking.Status == BookingStatus.Pending)
                .Select(booking => booking.Id)
                .ToListAsync(cancellationToken);

            return ids;
        }

        /// <inheritdoc />
        public Task<int> CountActiveByUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            return context.Bookings.CountAsync(booking =>
                booking.UserId == userId &&
                (booking.Status == BookingStatus.Pending || booking.Status == BookingStatus.Confirmed),
                cancellationToken);
        }

        /// <inheritdoc />
        public void Add(Booking booking)
        {
            context.Bookings.Add(booking);
        }
    }
}
