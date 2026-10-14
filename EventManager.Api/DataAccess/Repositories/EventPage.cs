using EventManager.Api.Models;

namespace EventManager.Api.DataAccess.Repositories
{
    /// <summary>Содержит материализованную страницу событий и количество записей до пагинации.</summary>
    /// <param name="Items">Сущности текущей страницы без отслеживания.</param>
    /// <param name="TotalCount">Общее количество отфильтрованных событий.</param>
    public record EventPage(IReadOnlyList<Event> Items, int TotalCount);
}
