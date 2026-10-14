using EventManager.Api.DataAccess;
using EventManager.Api.DataAccess.Repositories;
using EventManager.Api.IntegrationTests.Infrastructure;
using EventManager.Api.Models;
using EventManager.Api.Models.Dtos;
using Xunit;

namespace EventManager.Api.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.NAME)]
public class EventPageTests(PostgresFixture database)
{
    [Theory]
    [InlineData(null, "1,0,2,3,4,5,6")]
    [InlineData("", "1,0,2,3,4,5,6")]
    [InlineData("   ", "1,0,2,3,4,5,6")]
    [InlineData("alpha", "1,0,2")]
    [InlineData(" ALpHa ", "1,0,2")]
    [InlineData("conference", "0")]
    [InlineData("missing", "")]
    [InlineData("%", "4")]
    [InlineData("_", "4")]
    [InlineData("100%_", "4")]
    [InlineData("O'Reilly", "6")]
    public async Task GetPageAsync_FiltersByTitle(string? title, string expectedIndexes)
    {
        // Arrange
        await database.ResetAsync();
        Event[] events = CreateEvents(DateTime.UtcNow.Date.AddDays(10));
        await TestData.SeedAsync(database, events);
        Guid[] expectedIds = ParseExpectedIds(events, expectedIndexes);
        await using AppDbContext read = database.CreateContext();

        // Act
        PaginatedResult result = await new EventRepository(read)
            .GetPageAsync(title, null, null, 1, 20, CancellationToken.None);

        // Assert
        AssertFilteredPage(result, expectedIds, read);
    }

    [Theory]
    [InlineData(-1, "1,0,2,3,4,5,6")]
    [InlineData(0, "1,0,2,3,4,5,6")]
    [InlineData(1, "2,3,4,5,6")]
    [InlineData(4, "2,3,4,5,6")]
    [InlineData(5, "3,4,5,6")]
    [InlineData(13, "")]
    public async Task GetPageAsync_FiltersByInclusiveStartBoundary(int fromHours, string expectedIndexes)
    {
        // Arrange
        await database.ResetAsync();
        DateTime start = DateTime.UtcNow.Date.AddDays(10);
        Event[] events = CreateEvents(start);
        await TestData.SeedAsync(database, events);
        Guid[] expectedIds = ParseExpectedIds(events, expectedIndexes);
        DateTime from = start.AddHours(fromHours);
        await using AppDbContext read = database.CreateContext();

        // Act
        PaginatedResult result = await new EventRepository(read)
            .GetPageAsync(null, from, null, 1, 20, CancellationToken.None);

        // Assert
        AssertFilteredPage(result, expectedIds, read);
    }

    [Theory]
    [InlineData(1, "")]
    [InlineData(2, "0")]
    [InlineData(3, "1,0")]
    [InlineData(5, "1,0")]
    [InlineData(6, "1,0,2")]
    [InlineData(14, "1,0,2,3,4,5,6")]
    public async Task GetPageAsync_FiltersByInclusiveEndBoundary(int toHours, string expectedIndexes)
    {
        // Arrange
        await database.ResetAsync();
        DateTime start = DateTime.UtcNow.Date.AddDays(10);
        Event[] events = CreateEvents(start);
        await TestData.SeedAsync(database, events);
        Guid[] expectedIds = ParseExpectedIds(events, expectedIndexes);
        DateTime to = start.AddHours(toHours);
        await using AppDbContext read = database.CreateContext();

        // Act
        PaginatedResult result = await new EventRepository(read)
            .GetPageAsync(null, null, to, 1, 20, CancellationToken.None);

        // Assert
        AssertFilteredPage(result, expectedIds, read);
    }

    [Theory]
    [InlineData(null, 4, 6, "2")]
    [InlineData(null, 1, 8, "2,3")]
    [InlineData(null, 5, 5, "")]
    [InlineData(null, 8, 4, "")]
    [InlineData("alpha", 4, null, "2")]
    [InlineData("alpha", null, 3, "1,0")]
    [InlineData("alpha", 0, 3, "1,0")]
    [InlineData("alpha", 4, 6, "2")]
    [InlineData("alpha", 4, 5, "")]
    [InlineData("Beta", 0, 8, "2,3")]
    public async Task GetPageAsync_AppliesCombinedFilters(
        string? title, int? fromHours, int? toHours, string expectedIndexes)
    {
        // Arrange
        await database.ResetAsync();
        DateTime start = DateTime.UtcNow.Date.AddDays(10);
        Event[] events = CreateEvents(start);
        await TestData.SeedAsync(database, events);
        Guid[] expectedIds = ParseExpectedIds(events, expectedIndexes);
        DateTime? from = null;
        DateTime? to = null;
        if (fromHours.HasValue)
        {
            from = start.AddHours(fromHours.Value);
        }

        if (toHours.HasValue)
        {
            to = start.AddHours(toHours.Value);
        }

        await using AppDbContext read = database.CreateContext();

        // Act
        PaginatedResult result = await new EventRepository(read)
            .GetPageAsync(title, from, to, 1, 20, CancellationToken.None);

        // Assert
        AssertFilteredPage(result, expectedIds, read);
    }

    [Theory]
    [InlineData(1, 2, "1,0")]
    [InlineData(2, 2, "2,3")]
    [InlineData(3, 2, "4,5")]
    [InlineData(4, 2, "6")]
    [InlineData(5, 2, "")]
    [InlineData(1, 1, "1")]
    [InlineData(2, 1, "0")]
    [InlineData(1, 7, "1,0,2,3,4,5,6")]
    [InlineData(1, 20, "1,0,2,3,4,5,6")]
    public async Task GetPageAsync_ReturnsRequestedPageWithStableStartAtAndIdOrder(
        int page, int pageSize, string expectedIndexes)
    {
        // Arrange
        await database.ResetAsync();
        Event[] events = CreateEvents(DateTime.UtcNow.Date.AddDays(10));
        await TestData.SeedAsync(database, events);
        Guid[] expectedIds = ParseExpectedIds(events, expectedIndexes);
        await using AppDbContext read = database.CreateContext();

        // Act
        PaginatedResult result = await new EventRepository(read)
            .GetPageAsync(null, null, null, page, pageSize, CancellationToken.None);

        // Assert
        Assert.Equal(expectedIds, result.Items.Select(item => item.Id).ToArray());
        Assert.Equal(7, result.TotalCount);
        Assert.Equal((7 + pageSize - 1) / pageSize, result.TotalPages);
        Assert.Equal(page, result.Page);
        Assert.Equal(pageSize, result.PageSize);
        Assert.Empty(read.ChangeTracker.Entries());
        foreach (EventDto item in result.Items)
        {
            Event expected = Assert.Single(events, candidate => candidate.Id == item.Id);
            Assert.Equal(expected.Title, item.Title);
            Assert.Equal(expected.Description, item.Description);
            Assert.Equal(expected.StartAt, item.StartAt);
            Assert.Equal(expected.EndAt, item.EndAt);
            Assert.Equal(expected.TotalSeats, item.TotalSeats);
            Assert.Equal(expected.AvailableSeats, item.AvailableSeats);
        }
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(2, "0")]
    [InlineData(3, "2")]
    [InlineData(4, "")]
    public async Task GetPageAsync_PaginatesFilteredRowsAndCountsBeforePagination(int page, string expectedIndexes)
    {
        // Arrange
        await database.ResetAsync();
        DateTime start = DateTime.UtcNow.Date.AddDays(10);
        Event[] events = CreateEvents(start);
        await TestData.SeedAsync(database, events);
        Guid[] expectedIds = ParseExpectedIds(events, expectedIndexes);
        await using AppDbContext read = database.CreateContext();

        // Act
        PaginatedResult result = await new EventRepository(read)
            .GetPageAsync("alpha", start, start.AddHours(6), page, 1, CancellationToken.None);

        // Assert
        Assert.Equal(expectedIds, result.Items.Select(item => item.Id).ToArray());
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(page, result.Page);
        Assert.Equal(1, result.PageSize);
    }

    [Fact]
    public async Task GetPageAsync_ReturnsEmptyPageForEmptyDatabase()
    {
        // Arrange
        await database.ResetAsync();
        await using AppDbContext read = database.CreateContext();

        // Act
        PaginatedResult result = await new EventRepository(read)
            .GetPageAsync(null, null, null, 1, 2, CancellationToken.None);

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    private static void AssertFilteredPage(PaginatedResult result, Guid[] expectedIds, AppDbContext context)
    {
        Assert.Equal(expectedIds, result.Items.Select(item => item.Id).ToArray());
        Assert.Equal(expectedIds.Length, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    private static Event[] CreateEvents(DateTime start)
    {
        // Явные ключи принадлежат тесту; порядок вставки отличается от порядка страницы.
        return
        [
            new Event(Guid.Parse("00000000-0000-0000-0000-000000000003"), "Alpha conference", "First", start, start.AddHours(2), 10),
            new Event(Guid.Parse("00000000-0000-0000-0000-000000000001"), "aLPHa meetup", null, start, start.AddHours(3), 11),
            new Event(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Beta alpha", "Third", start.AddHours(4), start.AddHours(6), 12),
            TestData.CreateEvent("Beta", start.AddHours(6), start.AddHours(8)),
            TestData.CreateEvent("100%_literal", start.AddHours(8), start.AddHours(10)),
            TestData.CreateEvent("100XYliteral", start.AddHours(10), start.AddHours(12)),
            TestData.CreateEvent("O'Reilly", start.AddHours(12), start.AddHours(14))
        ];
    }

    private static Guid[] ParseExpectedIds(Event[] events, string indexes)
    {
        if (indexes.Length == 0)
        {
            return [];
        }

        return indexes.Split(',').Select(index => events[int.Parse(index)].Id).ToArray();
    }
}
