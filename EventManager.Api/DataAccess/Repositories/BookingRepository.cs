using EventManager.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EventManager.Api.DataAccess.Repositories
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
        public Task<Booking?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            return context.Bookings.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
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
        public void Add(Booking booking)
        {
            context.Bookings.Add(booking);
        }
    }
}
