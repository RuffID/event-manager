using EventManager.Api.Mappers;
using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using EventManager.Api.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace EventManager.Api.Services
{
    /// <summary>
    /// Реализует операции создания, получения, обновления и удаления событий.
    /// </summary>
    /// <param name="context">Контекст базы данных.</param>
    public class EventService(AppDbContext context) : IEventService
    {
        /// <inheritdoc />
        public async Task<ServiceResult<EventDto>> GetEventByIdAsync(Guid id)
        {
            Event? @event = await context.Events.AsNoTracking().SingleOrDefaultAsync(entity => entity.Id == id);

            if (@event is not null)
                return ServiceResult<EventDto>.Succeed(@event.ToDto());

            return ServiceResult<EventDto>.Fail(ServiceErrorType.NotFound, "Event not found.");
        }

        /// <inheritdoc />
        public async Task<PaginatedResult> GetEventsAsync(
            string? title = null,
            DateTime? from = null,
            DateTime? to = null,
            int page = 1,
            int pageSize = 10)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

            IQueryable<Event> events = context.Events.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(title))
            {
                string normalizedTitle = title.Trim().ToUpperInvariant();
                events = events.Where(@event =>
                    @event.Title.ToUpper().Contains(normalizedTitle));
            }

            if (from is not null)
                events = events.Where(@event => @event.StartAt >= from.Value);

            if (to is not null)
                events = events.Where(@event => @event.EndAt <= to.Value);

            int totalCount = await events.CountAsync();
            int offset = (page - 1) * pageSize;

            List<Event> pageEvents = await events
                .OrderBy(@event => @event.StartAt)
                .ThenBy(@event => @event.Id)
                .Skip(offset)
                .Take(pageSize)
                .ToListAsync();

            return new PaginatedResult
            {
                TotalCount = totalCount,
                Events = pageEvents.Select(@event => @event.ToDto()).ToList(),
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<ServiceResult<EventDto>> CreateEventAsync(CreateEventDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "Event title must not be empty.");

            if (dto.StartAt is not DateTime startAt || dto.EndAt is not DateTime endAt)
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "Start and end dates are required.");

            if (endAt <= startAt)
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "The end date must be later than the start date.");

            if (dto.TotalSeats is not int totalSeats || totalSeats <= 0)
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "The total number of seats must be greater than zero.");

            Event @event = dto.ToEvent();

            context.Events.Add(@event);
            await context.SaveChangesAsync();

            return ServiceResult<EventDto>.Succeed(@event.ToDto());
        }

        /// <inheritdoc />
        public async Task<ServiceResult<EventDto>> UpdateEventAsync(Guid id, UpdateEventDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Title))
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "Event title must not be empty.");

            if (dto.StartAt is not DateTime startAt || dto.EndAt is not DateTime endAt)
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "Start and end dates are required.");

            if (endAt <= startAt)
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "The end date must be later than the start date.");

            Event? @event = await context.Events.SingleOrDefaultAsync(entity => entity.Id == id);

            if (@event is null)
                return ServiceResult<EventDto>.Fail(ServiceErrorType.NotFound, "Event not found.");

            Event updatedEvent = dto.ToEvent(@event);
            await context.SaveChangesAsync();

            return ServiceResult<EventDto>.Succeed(updatedEvent.ToDto());
        }

        /// <inheritdoc />
        public async Task<ServiceResult> DeleteEventAsync(Guid id)
        {
            Event? @event = await context.Events.SingleOrDefaultAsync(entity => entity.Id == id);

            if (@event is null)
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Event not found.");

            context.Events.Remove(@event);
            await context.SaveChangesAsync();

            return ServiceResult.Succeed();
        }
    }
}
