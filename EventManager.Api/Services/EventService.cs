using EventManager.Api.Mappers;
using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using EventManager.Api.Repositories;

namespace EventManager.Api.Services
{
    /// <summary>
    /// Реализует операции создания, получения, обновления и удаления событий.
    /// </summary>
    /// <param name="eventRepository">Хранилище событий в памяти.</param>
    public class EventService(InMemoryEventRepository eventRepository) : IEventService
    {
        /// <inheritdoc />
        public ServiceResult<EventDto> GetEventById(Guid id)
        {
            if (eventRepository.Events.TryGetValue(id, out Event? @event))
                return ServiceResult<EventDto>.Succeed(@event.ToDto());

            return ServiceResult<EventDto>.Fail(404, "Event not found.");
        }

        /// <inheritdoc />
        public ServiceResult<List<EventDto>> GetEvents()
        {
            List<EventDto> events = [];

            foreach (Event @event in eventRepository.Events.Values)
            {
                events.Add(@event.ToDto());
            }

            return ServiceResult<List<EventDto>>.Succeed(events);
        }

        /// <inheritdoc />
        public ServiceResult<EventDto> CreateEvent(CreateEventDto dto)
        {
            if (dto.StartAt is not DateTime startAt || dto.EndAt is not DateTime endAt)
                return ServiceResult<EventDto>.Fail(400, "Start and end dates are required.");

            if (endAt <= startAt)
                return ServiceResult<EventDto>.Fail(400, "The end date must be later than the start date.");

            Event @event = dto.ToEvent();
            
            if (eventRepository.Events.TryAdd(@event.Id, @event))
                return ServiceResult<EventDto>.Succeed(@event.ToDto(), 201);

            return ServiceResult<EventDto>.Fail(500, "Failed to create event.");
        }

        /// <inheritdoc />
        public ServiceResult<EventDto> UpdateEvent(Guid id, UpdateEventDto dto)
        {
            if (dto.StartAt is not DateTime startAt || dto.EndAt is not DateTime endAt)
                return ServiceResult<EventDto>.Fail(400, "Start and end dates are required.");

            if (endAt <= startAt)
                return ServiceResult<EventDto>.Fail(400, "The end date must be later than the start date.");

            if (eventRepository.Events.TryGetValue(id, out Event? @event))
            {
                Event updatedEvent = dto.ToEvent(id);

                if (eventRepository.Events.TryUpdate(id, updatedEvent, @event))
                {
                    return ServiceResult<EventDto>.Succeed(updatedEvent.ToDto());
                }
                
                return ServiceResult<EventDto>.Fail(404, "Event not found.");
            }

            return ServiceResult<EventDto>.Fail(404, "Event not found.");            
        }

        /// <inheritdoc />
        public ServiceResult<bool> DeleteEvent(Guid id)
        {
            if (eventRepository.Events.TryRemove(id, out Event? @event))
                return ServiceResult<bool>.Succeed(true, 204);

            return ServiceResult<bool>.Fail(404, "Event not found.");            
        }
    }
}
