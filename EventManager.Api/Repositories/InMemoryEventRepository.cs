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
                new Event
                {
                    Id = Guid.NewGuid(),
                    Title = "C# Meetup: Clean Architecture",
                    Description = "Обсуждаем DTO, слои приложения и зависимости.",
                    StartAt = new DateTime(2026, 9, 12, 18, 30, 0),
                    EndAt = new DateTime(2026, 9, 12, 20, 30, 0)
                },
                new Event
                {
                    Id = Guid.NewGuid(),
                    Title = "Воркшоп по ASP.NET Core Web API",
                    Description = "Практика REST API, валидации DTO и обработки ошибок.",
                    StartAt = new DateTime(2026, 9, 19, 11, 0, 0),
                    EndAt = new DateTime(2026, 9, 19, 14, 0, 0)
                },
                new Event
                {
                    Id = Guid.NewGuid(),
                    Title = "Code review учебных проектов",
                    Description = "Разбираем архитектуру и типичные ошибки в C#-проектах.",
                    StartAt = new DateTime(2026, 9, 24, 19, 0, 0),
                    EndAt = new DateTime(2026, 9, 24, 21, 0, 0)
                },
                new Event
                {
                    Id = Guid.NewGuid(),
                    Title = "Лекция: конкурентность в .NET",
                    Description = "Async/await, потокобезопасные коллекции и CancellationToken.",
                    StartAt = new DateTime(2026, 10, 3, 18, 0, 0),
                    EndAt = new DateTime(2026, 10, 3, 20, 0, 0)
                },
                new Event
                {
                    Id = Guid.NewGuid(),
                    Title = "Демодень C# Middle",
                    Description = "Презентация учебных проектов и обратная связь.",
                    StartAt = new DateTime(2026, 10, 10, 15, 0, 0),
                    EndAt = new DateTime(2026, 10, 10, 18, 0, 0)
                }
            };

            foreach (Event item in events)
            {
                Events.TryAdd(item.Id, item);
            }
        }
    }
}
