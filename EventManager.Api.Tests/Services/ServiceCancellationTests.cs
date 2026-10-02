using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using EventManager.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EventManager.Api.Tests.Services
{
    public class ServiceCancellationTests
    {
        [Theory]
        [InlineData("list")]
        [InlineData("get-event")]
        [InlineData("create-event")]
        [InlineData("update-event")]
        [InlineData("delete-event")]
        [InlineData("create-booking")]
        [InlineData("get-booking")]
        public async Task Service_StopsWithoutChangingData_WhenTokenIsCancelled(string operation)
        {
            // Arrange
            using ServiceTestContext database = new ServiceTestContext();
            DateTime startAt = DateTime.UtcNow.AddDays(1);
            Event existing = Event.Create("Исходное событие", null, startAt, startAt.AddHours(2), 2);
            await database.SeedAsync(existing);
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            CancellationToken token = cancellation.Token;
            CreateEventDto create = new CreateEventDto
            {
                Title = "Новое событие", StartAt = startAt, EndAt = startAt.AddHours(2), TotalSeats = 2
            };
            UpdateEventDto update = new UpdateEventDto
            {
                Title = "Новое название", StartAt = startAt, EndAt = startAt.AddHours(2)
            };

            // Act
            Task action = operation switch
            {
                "list" => database.EventService.GetEventsAsync(cancellationToken: token),
                "get-event" => database.EventService.GetEventByIdAsync(existing.Id, token),
                "create-event" => database.EventService.CreateEventAsync(create, token),
                "update-event" => database.EventService.UpdateEventAsync(existing.Id, update, token),
                "delete-event" => database.EventService.DeleteEventAsync(existing.Id, token),
                "create-booking" => database.BookingService.CreateBookingAsync(existing.Id, token),
                "get-booking" => database.BookingService.GetBookingByIdAsync(Guid.NewGuid(), token),
                _ => throw new ArgumentOutOfRangeException(nameof(operation))
            };

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => action);
            database.Context.ChangeTracker.Clear();
            Event stored = await database.Context.Events.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal("Исходное событие", stored.Title);
            Assert.Equal(2, stored.AvailableSeats);
            Assert.Empty(database.Context.Bookings);
        }

        [Fact]
        public async Task CreateBooking_CancelsSemaphoreWait_WithoutInterferingWithOtherRequests()
        {
            // Arrange
            BlockingSaveInterceptor interceptor = new BlockingSaveInterceptor();
            using ServiceTestContext database = new ServiceTestContext(interceptor: interceptor);
            Event existing = Event.Create("Событие", null, DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 2);
            await database.SeedAsync(existing);
            using IServiceScope secondScope = database.ServiceProvider.CreateScope();
            IBookingService secondService = secondScope.ServiceProvider.GetRequiredService<IBookingService>();
            using CancellationTokenSource cancellation = new CancellationTokenSource();
            using CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Task<ServiceResult<BookingInfo>> firstRequest = database.BookingService.CreateBookingAsync(existing.Id, timeout.Token);

            // Act
            try
            {
                await interceptor.Started.WaitAsync(timeout.Token);
                Task<ServiceResult<BookingInfo>> secondRequest = secondService.CreateBookingAsync(existing.Id, cancellation.Token);
                cancellation.Cancel();

                // Assert
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => secondRequest.WaitAsync(timeout.Token));
                Assert.False(firstRequest.IsCompleted);
                Assert.Equal(timeout.Token, interceptor.ReceivedToken);
            }
            finally
            {
                interceptor.Release();
                await firstRequest;
            }

            // Arrange
            using IServiceScope thirdScope = database.ServiceProvider.CreateScope();
            IBookingService thirdService = thirdScope.ServiceProvider.GetRequiredService<IBookingService>();

            // Act
            ServiceResult<BookingInfo> thirdResult = await thirdService.CreateBookingAsync(existing.Id, timeout.Token);

            // Assert
            Assert.True((await firstRequest).Success);
            Assert.True(thirdResult.Success);
            database.Context.ChangeTracker.Clear();
            Assert.Equal(2, await database.Context.Bookings.CountAsync(TestContext.Current.CancellationToken));
            Assert.Equal(0, (await database.Context.Events.SingleAsync(TestContext.Current.CancellationToken)).AvailableSeats);
        }

        private class BlockingSaveInterceptor : SaveChangesInterceptor
        {
            private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task Started => _started.Task;

            public CancellationToken ReceivedToken { get; private set; }

            public void Release() => _released.TrySetResult(true);

            public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData,
                InterceptionResult<int> result,
                CancellationToken cancellationToken = default)
            {
                if (eventData.Context!.ChangeTracker.Entries<Booking>().Any(entry => entry.State == EntityState.Added))
                {
                    ReceivedToken = cancellationToken;
                    _started.TrySetResult(true);
                    await _released.Task.WaitAsync(cancellationToken);
                }

                return result;
            }
        }
    }
}
