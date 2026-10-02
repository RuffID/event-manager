using System.Text.Json;
using EventManager.Api.Models.Dtos;
using Xunit;

namespace EventManager.Api.Tests.Models
{
    public class PaginatedResultTests
    {
        [Theory]
        [InlineData(0, 10, 0)]
        [InlineData(1, 10, 1)]
        [InlineData(10, 10, 1)]
        [InlineData(11, 10, 2)]
        [InlineData(int.MaxValue, 10, 214748365)]
        public void Serialize_ReturnsItemsAndTotalPages(int totalCount, int pageSize, int expectedPages)
        {
            // Arrange
            PaginatedResult result = new PaginatedResult
            {
                TotalCount = totalCount, Page = 1, PageSize = pageSize
            };

            // Act
            string json = JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web));

            // Assert
            using JsonDocument document = JsonDocument.Parse(json);
            Assert.Equal(expectedPages, document.RootElement.GetProperty("totalPages").GetInt32());
            Assert.Equal(JsonValueKind.Array, document.RootElement.GetProperty("items").ValueKind);
            Assert.False(document.RootElement.TryGetProperty("events", out _));
        }
    }
}
