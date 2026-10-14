using EventManager.Api.Mappers;
using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using EventManager.Api.DataAccess;
using EventManager.Api.DataAccess.Repositories;

namespace EventManager.Api.Services
{
    /// <summary>
    /// Реализует операции создания, получения, обновления и удаления событий.
    /// </summary>
    /// <param name="eventRepository">Репозиторий событий.</param>
    /// <param name="unitOfWork">Общая граница сохранения изменений.</param>
    public class EventService(IEventRepository eventRepository, IUnitOfWork unitOfWork) : IEventService
    {
        /// <inheritdoc />
        public async Task<ServiceResult<EventDto>> GetEventByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Event? @event = await eventRepository.GetByIdAsync(id, cancellationToken);
            if (@event is not null)
            {
                return ServiceResult<EventDto>.Succeed(@event.ToDto());
            }

            return ServiceResult<EventDto>.Fail(ServiceErrorType.NotFound, "Event not found.");
        }

        /// <inheritdoc />
        public async Task<PaginatedResult> GetEventsAsync(
            string? title = null,
            DateTime? from = null,
            DateTime? to = null,
            int page = 1,
            int pageSize = 10,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

            EventPage eventPage = await eventRepository.GetPageAsync(title, from, to, page, pageSize, cancellationToken);

            return new PaginatedResult
            {
                Items = eventPage.Items.Select(@event => @event.ToDto()).ToList(),
                TotalCount = eventPage.TotalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        /// <inheritdoc />
        public async Task<ServiceResult<EventDto>> CreateEventAsync(CreateEventDto dto, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "Event title must not be empty.");
            }

            if (dto.Title.Length > Event.MAX_TITLE_LENGTH)
            {
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "Event title must not exceed 200 characters.");
            }

            if (dto.Description?.Length > Event.MAX_DESCRIPTION_LENGTH)
            {
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "Event description must not exceed 2000 characters.");
            }

            if (dto.StartAt is not DateTime startAt || dto.EndAt is not DateTime endAt)
            {
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "Start and end dates are required.");
            }

            if (endAt <= startAt)
            {
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "The end date must be later than the start date.");
            }

            if (startAt < DateTime.UtcNow)
            {
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "Event cannot start in the past.");
            }

            if (dto.TotalSeats is not int totalSeats || totalSeats <= 0)
            {
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "The total number of seats must be greater than zero.");
            }

            Event @event = dto.ToEvent();

            eventRepository.Add(@event);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return ServiceResult<EventDto>.Succeed(@event.ToDto());
        }

        /// <inheritdoc />
        public async Task<ServiceResult<EventDto>> UpdateEventAsync(Guid id, UpdateEventDto dto, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "Event title must not be empty.");
            }

            if (dto.Title.Length > Event.MAX_TITLE_LENGTH)
            {
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "Event title must not exceed 200 characters.");
            }

            if (dto.Description?.Length > Event.MAX_DESCRIPTION_LENGTH)
            {
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "Event description must not exceed 2000 characters.");
            }

            if (dto.StartAt is not DateTime startAt || dto.EndAt is not DateTime endAt)
            {
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "Start and end dates are required.");
            }

            if (endAt <= startAt)
            {
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "The end date must be later than the start date.");
            }

            Event? @event = await eventRepository.GetByIdForUpdateAsync(id, cancellationToken);
            if (@event is null)
            {
                return ServiceResult<EventDto>.Fail(ServiceErrorType.NotFound, "Event not found.");
            }

            if (startAt != @event.StartAt && startAt < DateTime.UtcNow)
            {
                return ServiceResult<EventDto>.Fail(ServiceErrorType.Validation, "Event cannot start in the past.");
            }

            Event updatedEvent = dto.ToEvent(@event);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return ServiceResult<EventDto>.Succeed(updatedEvent.ToDto());
        }

        /// <inheritdoc />
        public async Task<ServiceResult> DeleteEventAsync(Guid id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Event? @event = await eventRepository.GetByIdForUpdateAsync(id, cancellationToken);
            if (@event is null)
            {
                return ServiceResult.Fail(ServiceErrorType.NotFound, "Event not found.");
            }

            eventRepository.Remove(@event);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return ServiceResult.Succeed();
        }
    }
}
