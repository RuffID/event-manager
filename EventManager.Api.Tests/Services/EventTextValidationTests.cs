using System.ComponentModel.DataAnnotations;
using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;
using EventManager.Api.Models.Results;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EventManager.Api.Tests.Services
{
    public class EventTextValidationTests
    {
        [Theory]
        [InlineData(200, 2000, true)]
        [InlineData(201, 2000, false)]
        [InlineData(200, 2001, false)]
        public async Task CreateAndUpdate_EnforceTextLimits(int titleLength, int descriptionLength, bool valid)
        {
            // Arrange
            using ServiceTestContext database = new ServiceTestContext();
            string title = new string('A', titleLength);
            string description = new string('B', descriptionLength);
            DateTime startAt = DateTime.UtcNow.AddDays(1);
            CreateEventDto create = new CreateEventDto
            {
                Title = title, Description = description, StartAt = startAt,
                EndAt = startAt.AddHours(2), TotalSeats = 10
            };
            List<ValidationResult> errors = new List<ValidationResult>();

            // Act
            bool dtoValid = Validator.TryValidateObject(create, new ValidationContext(create), errors, true);
            ServiceResult<EventDto> createResult = await database.EventService.CreateEventAsync(create, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(valid, dtoValid);
            Assert.Equal(valid, createResult.Success);
            if (!valid)
            {
                Assert.Equal(ServiceErrorType.Validation, createResult.Error!.Type);
                Assert.Empty(database.Context.Events);
                Assert.Throws<ValidationException>(() => Event.Create(title, description, startAt, startAt.AddHours(2), 10));
            }

            // Arrange
            Event existing = Event.Create("Исходное название", null, startAt, startAt.AddHours(2), 10);
            await database.SeedAsync(existing);
            UpdateEventDto update = new UpdateEventDto
            {
                Title = title, Description = description, StartAt = startAt, EndAt = startAt.AddHours(2)
            };
            errors.Clear();

            // Act
            dtoValid = Validator.TryValidateObject(update, new ValidationContext(update), errors, true);
            ServiceResult<EventDto> updateResult = await database.EventService.UpdateEventAsync(existing.Id, update, cancellationToken: TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal(valid, dtoValid);
            Assert.Equal(valid, updateResult.Success);
            database.Context.ChangeTracker.Clear();
            Event stored = await database.Context.Events.SingleAsync(entity => entity.Id == existing.Id,
                TestContext.Current.CancellationToken);
            Assert.Equal(valid ? title : "Исходное название", stored.Title);
            Assert.Equal(valid ? description : null, stored.Description);
            if (!valid)
            {
                Assert.Equal(ServiceErrorType.Validation, updateResult.Error!.Type);
                Assert.Throws<ValidationException>(() => existing.UpdateDetails(title, description, startAt, startAt.AddHours(2)));
            }
        }
    }
}
