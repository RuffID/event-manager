using EventManager.Api.Models;
using System.Collections.Concurrent;

namespace EventManager.Api.Repositories
{
    /// <summary>
    /// Хранит события в памяти приложения, сделан для учебной версии API.
    /// </summary>
    public class InMemoryEventRepository
    {
        /// <summary>
        /// Получает потокобезопасную коллекцию событий, где ключом является идентификатор события.
        /// </summary>
        public ConcurrentDictionary<Guid, Event> Events { get; } = new();

        /// <summary>
        /// Инициализирует хранилище тестовыми событиями.
        /// </summary>
        public InMemoryEventRepository()
        {
            var events = new[]
            {
                new Event(
                    Guid.NewGuid(),
                    "C# Meetup: Clean Architecture",
                    "Обсуждаем DTO, слои приложения и зависимости.",
                    new DateTime(2026, 9, 12, 18, 30, 0),
                    new DateTime(2026, 9, 12, 20, 30, 0)),
                new Event(
                    Guid.NewGuid(),
                    "Воркшоп по ASP.NET Core Web API",
                    "Практика REST API, валидации DTO и обработки ошибок.",
                    new DateTime(2026, 9, 19, 11, 0, 0),
                    new DateTime(2026, 9, 19, 14, 0, 0)),
                new Event(
                    Guid.NewGuid(),
                    "Code review учебных проектов",
                    "Разбираем архитектуру и типичные ошибки в C#-проектах.",
                    new DateTime(2026, 9, 24, 19, 0, 0),
                    new DateTime(2026, 9, 24, 21, 0, 0)),
                new Event(
                    Guid.NewGuid(),
                    "Лекция: конкурентность в .NET",
                    "Async/await, потокобезопасные коллекции и CancellationToken.",
                    new DateTime(2026, 10, 3, 18, 0, 0),
                    new DateTime(2026, 10, 3, 20, 0, 0)),
                new Event(
                    Guid.NewGuid(),
                    "Демодень C# Middle",
                    "Презентация учебных проектов и обратная связь.",
                    new DateTime(2026, 10, 10, 15, 0, 0),
                    new DateTime(2026, 10, 10, 18, 0, 0))
            };

            foreach (Event item in events)
            {
                Events.TryAdd(item.Id, item);
            }
        }
    }
}
