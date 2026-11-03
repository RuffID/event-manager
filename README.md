# Event Manager

Учебный REST API на ASP.NET Core. Позволяет создавать события, искать их по названию и датам, бронировать места и проверять состояние брони.

Данные хранятся в PostgreSQL. Используются .NET 10, Entity Framework Core и Swagger, для тестов — xUnit, InMemory и Testcontainers.

## Структура проекта

- `EventManager.Domain` — события, брони и их основные правила.
- `EventManager.Application` — действия с событиями и бронями, данные для запросов и ответов, интерфейсы работы с хранилищем.
- `EventManager.Infrastructure` — работа с PostgreSQL. Контекст, настройки таблиц и миграции находятся в `DataAccess`.
- `EventManager.Api` — HTTP-запросы, ответы, обработка ошибок и запуск приложения.
- `EventManager.Api.Tests` — тесты без настоящей БД.
- `EventManager.Api.IntegrationTests` — тесты с PostgreSQL в Docker.

Application зависит от Domain, Infrastructure — от Application и Domain. API подключает остальные слои. Domain не зависит от других проектов.

Зависимости собираются в `EventManager.Api/Program.cs` через методы `AddApplication()`, `AddInfrastructure()`, `AddPresentation()` и `AddBookingProcessingHosting()`. Интерфейсы хранилища находятся в `Application/Abstractions/Persistence`.

Правила фоновой обработки брони находятся в Application, а её запуск и остановка — в API.

## Запуск

Понадобятся .NET SDK 10 и запущенный PostgreSQL. Docker нужен только для интеграционных тестов. Все команды выполняются из папки с `event-manager.slnx`.

Строка подключения задана в `EventManager.Api/appsettings.json`, в настройке `ConnectionStrings:DefaultConnection`. Её можно переопределить в PowerShell:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=<host>;Port=<port>;Database=<database>;Username=<user>;Password=<password>'
```

Замените значения в угловых скобках своими параметрами. Не сохраняйте реальные пароли в проекте.

Сборка и запуск:

```powershell
dotnet build .\event-manager.slnx
dotnet run --project .\EventManager.Api\EventManager.Api.csproj
```

При запуске приложение автоматически применяет миграции к выбранной БД. При ошибке запуск останавливается.

Swagger: [http://localhost:5239/swagger/index.html](http://localhost:5239/swagger/index.html).

### Миграции

Миграции находятся в Infrastructure, а запускаемый проект — API. Для создания новой миграции нужен совместимый `dotnet-ef 10.0.x`:

```powershell
$efConfigurationDirectory = (Resolve-Path .\EventManager.Api).Path
dotnet ef migrations add NextMigration --project .\EventManager.Infrastructure\EventManager.Infrastructure.csproj --startup-project .\EventManager.Api\EventManager.Api.csproj --context AppDbContext --output-dir DataAccess\Migrations -- --configuration-directory $efConfigurationDirectory
```

Замените `NextMigration` названием изменения. Путь к настройкам передаётся абсолютным, поскольку EF меняет рабочую папку.

В старых файлах миграций сохранены прежние имена сущностей. Проверка EF подтвердила, что текущая модель соответствует последней миграции. Применение всей цепочки на пустой БД ещё не проверялось.

## API

| Метод | Путь | Действие | Успешный ответ |
|---|---|---|---|
| GET | `/events` | Список событий | 200 |
| GET | `/events/{id}` | Получить событие | 200 |
| POST | `/events` | Создать событие | 201 |
| PUT | `/events/{id}` | Изменить событие | 200 |
| DELETE | `/events/{id}` | Удалить событие и его брони | 204 |
| POST | `/events/{id}/book` | Забронировать одно место | 202 |
| GET | `/bookings/{id}` | Получить бронь | 200 |

Список событий поддерживает фильтры `title`, `from`, `to` и параметры страниц `page` и `pageSize`. По умолчанию возвращается первая страница из 10 событий. В ответе есть `items`, `totalCount`, `page`, `pageSize` и `totalPages`.

### Создание и изменение события

Пример тела запроса `POST /events`:

```json
{
  "title": "Воркшоп по ASP.NET Core",
  "description": "Практика разработки Web API.",
  "startAt": "2030-01-01T10:00:00Z",
  "endAt": "2030-01-01T12:00:00Z",
  "totalSeats": 30
}
```

Название обязательно, максимум — 200 символов, описание — до 2000. Начало события не должно быть в прошлом, окончание должно быть позже начала, количество мест — больше нуля. Даты передаются в UTC с суффиксом `Z`.

Для `PUT /events/{id}` передаются те же поля, кроме `totalSeats`: число мест не меняется. У уже начавшегося события можно исправить название и описание, оставив прежнюю дату начала.

### Бронирование

`POST /events/{id}/book` не требует тела запроса. При создании брони число свободных мест уменьшается на одно. Ответ содержит бронь и заголовок `Location` со ссылкой на её получение.

Бронь подтверждается в фоне. Обработка включает задержку 2 секунды, между проходами служба ждёт 5 секунд. Проверить результат можно через `GET /bookings/{id}`:

- `0` — ожидает обработки;
- `1` — подтверждена;
- `2` — отклонена.

После обработки заполняется `processedAt`. При сбое сервер пытается отклонить ожидающую бронь и вернуть место. Если мест нет, новая бронь не создаётся.

### Ошибки

- `400` — неверные данные.
- `404` — событие или бронь не найдены.
- `409` — нет свободных мест.
- `500` — внутренняя ошибка сервера.

Причина ошибки передаётся в `detail`, ошибки отдельных полей — в `errors`.

## Как попробовать в Swagger

1. Создайте событие через `POST /Events` и скопируйте его `id`.
2. Выполните `POST /Events/{id}/book` и скопируйте `id` брони.
3. Получите событие через `GET /Events/{id}`: свободных мест должно стать на одно меньше.
4. Получите бронь через `GET /Bookings/{id}`: после обработки статус станет `1`.
5. Удалите тестовое событие через `DELETE /Events/{id}`.

Чтобы проверить ограничение мест, создайте событие с одним местом: первая бронь вернёт `202`, вторая — `409`.

## Тесты

Тесты без настоящей БД:

```powershell
dotnet test --project .\EventManager.Api.Tests\EventManager.Api.Tests.csproj
```

Они проверяют правила и обработку запросов в памяти, но не заменяют проверку PostgreSQL и запуска API.

Интеграционные тесты требуют работающего Docker. Они создают отдельный контейнер `postgres:16-alpine`, проверяют БД и миграции, затем удаляют контейнер. БД приложения не используется.

```powershell
dotnet test --project .\EventManager.Api.IntegrationTests\EventManager.Api.IntegrationTests.csproj
```
