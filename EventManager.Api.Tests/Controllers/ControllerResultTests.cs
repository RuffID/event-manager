using EventManager.Api.Controllers;
using EventManager.Application.Models.Dtos;
using EventManager.Application.Models.Results;
using EventManager.Application.Services;
using EventManager.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace EventManager.Api.Tests.Controllers;

public class ControllerResultTests
{
    [Theory]
    [InlineData(ServiceErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ServiceErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ServiceErrorType.Internal, StatusCodes.Status500InternalServerError)]
    public async Task Actions_MapServiceErrorsToProblemDetails(ServiceErrorType error, int status)
    {
        // Arrange
        const string MESSAGE = "Operation refused.";
        EventServiceStub events = new EventServiceStub
        {
            Result = ServiceResult<EventDto>.Fail(error, MESSAGE),
            DeleteResult = ServiceResult.Fail(error, MESSAGE)
        };
        BookingServiceStub bookings = new BookingServiceStub
        {
            Result = ServiceResult<BookingInfo>.Fail(error, MESSAGE)
        };
        EventsController eventController = new EventsController(events, bookings);
        BookingsController bookingController = new BookingsController(bookings);
        Guid id = Guid.NewGuid();
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        // Act
        IActionResult[] results =
        [
            await eventController.GetEvent(id, cancellation.Token),
            await eventController.CreateEvent(new CreateEventDto(), cancellation.Token),
            await eventController.UpdateEvent(id, new UpdateEventDto(), cancellation.Token),
            await eventController.DeleteEvent(id, cancellation.Token),
            await eventController.CreateBooking(id, cancellation.Token),
            await bookingController.GetBooking(id, cancellation.Token)
        ];

        // Assert
        Assert.All(results, result =>
        {
            ObjectResult response = Assert.IsType<ObjectResult>(result);
            Assert.Equal(status, response.StatusCode);
            ProblemDetails problem = Assert.IsType<ProblemDetails>(response.Value);
            Assert.Equal(status, problem.Status);
            Assert.Equal("Request processing failed", problem.Title);
            Assert.Equal(MESSAGE, problem.Detail);
        });
        Assert.Equal(4, events.Tokens.Count);
        Assert.Equal(2, bookings.Tokens.Count);
        Assert.All(events.Tokens.Concat(bookings.Tokens), token => Assert.Equal(cancellation.Token, token));
    }

    [Fact]
    public async Task CreateEvent_ReturnsCreatedAtActionWithServiceDataAndForwardsInput()
    {
        // Arrange
        EventDto data = new EventDto { Id = Guid.NewGuid(), Title = "Created" };
        EventServiceStub events = new EventServiceStub { Result = ServiceResult<EventDto>.Succeed(data) };
        EventsController controller = new EventsController(events, new BookingServiceStub());
        CreateEventDto input = new CreateEventDto { Title = "Input" };
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        // Act
        IActionResult result = await controller.CreateEvent(input, cancellation.Token);

        // Assert
        CreatedAtActionResult created = Assert.IsType<CreatedAtActionResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(nameof(EventsController.GetEvent), created.ActionName);
        Assert.Equal(data.Id, created.RouteValues!["id"]);
        Assert.Same(data, created.Value);
        Assert.Same(input, events.CreatedInput);
        Assert.Equal(cancellation.Token, Assert.Single(events.Tokens));
    }

    [Fact]
    public async Task CreateBooking_ReturnsAcceptedAtBookingActionAndForwardsEventId()
    {
        // Arrange
        Guid eventId = Guid.NewGuid();
        BookingInfo data = new BookingInfo { Id = Guid.NewGuid(), EventId = eventId, Status = BookingStatus.Pending };
        BookingServiceStub bookings = new BookingServiceStub { Result = ServiceResult<BookingInfo>.Succeed(data) };
        EventsController controller = new EventsController(new EventServiceStub(), bookings);
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        // Act
        IActionResult result = await controller.CreateBooking(eventId, cancellation.Token);

        // Assert
        AcceptedAtActionResult accepted = Assert.IsType<AcceptedAtActionResult>(result);
        Assert.Equal(StatusCodes.Status202Accepted, accepted.StatusCode);
        Assert.Equal(nameof(BookingsController.GetBooking), accepted.ActionName);
        Assert.Equal("Bookings", accepted.ControllerName);
        Assert.Equal(data.Id, accepted.RouteValues!["id"]);
        Assert.Same(data, accepted.Value);
        Assert.Equal(eventId, bookings.ReceivedId);
        Assert.Equal(cancellation.Token, Assert.Single(bookings.Tokens));
    }

    [Fact]
    public async Task ReadUpdateAndDelete_ReturnExpectedDataAndForwardArguments()
    {
        // Arrange
        EventDto data = new EventDto { Id = Guid.NewGuid(), Title = "Event" };
        PaginatedResult page = new PaginatedResult { Items = [data], TotalCount = 1, Page = 2, PageSize = 3 };
        EventServiceStub events = new EventServiceStub
        {
            Result = ServiceResult<EventDto>.Succeed(data), Page = page, DeleteResult = ServiceResult.Succeed()
        };
        BookingInfo booking = new BookingInfo { Id = Guid.NewGuid(), EventId = data.Id };
        BookingServiceStub bookings = new BookingServiceStub { Result = ServiceResult<BookingInfo>.Succeed(booking) };
        EventsController controller = new EventsController(events, bookings);
        UpdateEventDto input = new UpdateEventDto { Title = "Updated" };
        DateTime from = DateTime.UtcNow.AddDays(1);
        DateTime to = from.AddDays(1);
        using CancellationTokenSource cancellation = new CancellationTokenSource();

        // Act
        IActionResult list = await controller.GetEvents("Filter", from, to, 2, 3, cancellation.Token);
        IActionResult single = await controller.GetEvent(data.Id, cancellation.Token);
        IActionResult updated = await controller.UpdateEvent(data.Id, input, cancellation.Token);
        IActionResult deleted = await controller.DeleteEvent(data.Id, cancellation.Token);
        IActionResult readBooking = await new BookingsController(bookings).GetBooking(booking.Id, cancellation.Token);

        // Assert
        Assert.Same(page, Assert.IsType<OkObjectResult>(list).Value);
        Assert.Same(data, Assert.IsType<OkObjectResult>(single).Value);
        Assert.Same(data, Assert.IsType<OkObjectResult>(updated).Value);
        Assert.IsType<NoContentResult>(deleted);
        Assert.Same(booking, Assert.IsType<OkObjectResult>(readBooking).Value);
        Assert.Equal(("Filter", from, to, 2, 3), events.Filters);
        Assert.Equal(new[] { data.Id, data.Id, data.Id }, events.Ids);
        Assert.Same(input, events.UpdatedInput);
        Assert.Equal(booking.Id, bookings.ReceivedId);
        Assert.Equal(4, events.Tokens.Count);
        Assert.Equal(cancellation.Token, Assert.Single(bookings.Tokens));
        Assert.All(events.Tokens, token => Assert.Equal(cancellation.Token, token));
    }

    private class EventServiceStub : IEventService
    {
        public ServiceResult<EventDto> Result { get; set; } = ServiceResult<EventDto>.Fail(ServiceErrorType.NotFound, "Missing.");
        public ServiceResult DeleteResult { get; set; } = ServiceResult.Fail(ServiceErrorType.NotFound, "Missing.");
        public PaginatedResult Page { get; set; } = new PaginatedResult();
        public List<CancellationToken> Tokens { get; } = [];
        public List<Guid> Ids { get; } = [];
        public CreateEventDto? CreatedInput { get; private set; }
        public UpdateEventDto? UpdatedInput { get; private set; }
        public (string? Title, DateTime? From, DateTime? To, int Page, int PageSize)? Filters { get; private set; }

        public Task<ServiceResult<EventDto>> GetEventByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Ids.Add(id);
            Tokens.Add(cancellationToken);

            return Task.FromResult(Result);
        }

        public Task<PaginatedResult> GetEventsAsync(string? title = null, DateTime? from = null, DateTime? to = null,
            int page = 1, int pageSize = 10, CancellationToken cancellationToken = default)
        {
            Filters = (title, from, to, page, pageSize);
            Tokens.Add(cancellationToken);

            return Task.FromResult(Page);
        }

        public Task<ServiceResult<EventDto>> CreateEventAsync(CreateEventDto input, CancellationToken cancellationToken = default)
        {
            CreatedInput = input;
            Tokens.Add(cancellationToken);

            return Task.FromResult(Result);
        }

        public Task<ServiceResult<EventDto>> UpdateEventAsync(Guid id, UpdateEventDto input, CancellationToken cancellationToken = default)
        {
            Ids.Add(id);
            UpdatedInput = input;
            Tokens.Add(cancellationToken);

            return Task.FromResult(Result);
        }

        public Task<ServiceResult> DeleteEventAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Ids.Add(id);
            Tokens.Add(cancellationToken);

            return Task.FromResult(DeleteResult);
        }
    }

    private class BookingServiceStub : IBookingService
    {
        public ServiceResult<BookingInfo> Result { get; set; } = ServiceResult<BookingInfo>.Fail(ServiceErrorType.NotFound, "Missing.");
        public List<CancellationToken> Tokens { get; } = [];
        public Guid ReceivedId { get; private set; }

        public Task<ServiceResult<BookingInfo>> CreateBookingAsync(Guid eventId, CancellationToken cancellationToken = default)
        {
            ReceivedId = eventId;
            Tokens.Add(cancellationToken);

            return Task.FromResult(Result);
        }

        public Task<ServiceResult<BookingInfo>> GetBookingByIdAsync(Guid bookingId, CancellationToken cancellationToken = default)
        {
            ReceivedId = bookingId;
            Tokens.Add(cancellationToken);

            return Task.FromResult(Result);
        }
    }
}
