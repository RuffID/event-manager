namespace EventManager.Api.Models.Dtos
{
    /// <summary>
    /// Представляет страницу событий и сведения о пагинации.
    /// </summary>
    public class PaginatedResult
    {
        /// <summary>Получает или задаёт общее количество отфильтрованных событий.</summary>
        public int TotalCount { get; set; }

        /// <summary>Получает или задаёт события текущей страницы.</summary>
        public List<EventDto> Events { get; set; } = [];

        /// <summary>Получает или задаёт номер текущей страницы.</summary>
        public int Page { get; set; }

        /// <summary>Получает или задаёт количество элементов на странице.</summary>
        public int PageSize { get; set; }
    }
}
