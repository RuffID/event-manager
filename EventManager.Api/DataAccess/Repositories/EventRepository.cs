using EventManager.Api.Mappers;
using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EventManager.Api.DataAccess.Repositories
{
    /// <summary>Выполняет запросы и регистрирует изменения событий в общем scoped-контексте.</summary>
    /// <param name="context">Контекст текущей единицы работы.</param>
    public class EventRepository(AppDbContext context) : IEventRepository
    {
        /// <inheritdoc />
        public Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return context.Events.AsNoTracking()
                .SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        }

        /// <inheritdoc />
        public Task<Event?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            return context.Events.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<Event?> GetCurrentByIdForUpdateAsync(Guid id, CancellationToken cancellationToken)
        {
            Event? @event = await GetByIdForUpdateAsync(id, cancellationToken);
            if (@event is null)
            {
                return null;
            }

            // Обновляет состояние, если событие уже загружалось в текущем scope.
            await context.Entry(@event).ReloadAsync(cancellationToken);
            if (context.Entry(@event).State == EntityState.Detached)
            {
                return null;
            }

            return @event;
        }

        /// <inheritdoc />
        public async Task<PaginatedResult> GetPageAsync(
            string? title,
            DateTime? from,
            DateTime? to,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            IQueryable<Event> events = context.Events.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(title))
            {
                string normalizedTitle = title.Trim().ToUpperInvariant();
                events = events.Where(@event =>
                    @event.Title.ToUpper().Contains(normalizedTitle));
            }

            if (from is not null)
            {
                events = events.Where(@event => @event.StartAt >= from.Value);
            }

            if (to is not null)
            {
                events = events.Where(@event => @event.EndAt <= to.Value);
            }

            int totalCount = await events.CountAsync(cancellationToken);
            int offset = (page - 1) * pageSize;
            List<Event> pageEvents = await events
                .OrderBy(@event => @event.StartAt)
                .ThenBy(@event => @event.Id)
                .Skip(offset)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return new PaginatedResult
            {
                TotalCount = totalCount,
                Items = pageEvents.Select(@event => @event.ToDto()).ToList(),
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken)
        {
            return context.Events.AnyAsync(entity => entity.Id == id, cancellationToken);
        }

        /// <inheritdoc />
        public void Add(Event @event)
        {
            context.Events.Add(@event);
        }

        /// <inheritdoc />
        public void Remove(Event @event)
        {
            context.Events.Remove(@event);
        }
    }
}
