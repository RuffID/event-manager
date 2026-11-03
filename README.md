# Event Manager

Учебный REST API для создания событий и управления их бронированиями на ASP.NET Core.

Можно создавать и редактировать события, искать их по названию и датам, бронировать свободные места и проверять состояние брони. Данные сохраняются в PostgreSQL, а бронирования подтверждаются в фоне.

Технологии: .NET 10, ASP.NET Core, Entity Framework Core, PostgreSQL и Swagger. Для тестов используются xUnit, EF Core InMemory и Testcontainers.

## Запуск

Команды выполняются из корня проекта, если не указано другое.

### Что понадобится

- .NET SDK 10.
- Запущенный PostgreSQL.
- Docker — только для интеграционных тестов.

Сборка решения:

```powershell
dotnet build .\event-manager.slnx
```

### Подключение к PostgreSQL

Подключение задаётся настройкой `ConnectionStrings:DefaultConnection`. Его можно указать в текущем окне PowerShell:

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=<host>;Port=<port>;Database=<database>;Username=<user>;Password=<password>'
```

Замените значения в угловых скобках параметрами своей PostgreSQL. Не сохраняйте реальные пароли в файлах проекта.

При запуске приложение автоматически применяет миграции и создаёт таблицы. Пользователю PostgreSQL нужны права на создание таблиц, а если БД ещё нет — и на создание БД.

```powershell
dotnet run --project .\EventManager.Api\EventManager.Api.csproj
```

После запуска Swagger UI будет доступен по адресу:

```text
http://localhost:5239/swagger/index.html
```

Если порт переопределён в настройках запуска, его актуальное значение отображается в консоли приложения.

### Миграции EF Core

Миграции создают и обновляют таблицы в БД. Для обычного запуска ничего выполнять вручную не нужно: приложение само применяет готовые миграции.

Проект контекста и миграций — `EventManager.Infrastructure`, startup project — `EventManager.Api`, контекст — `AppDbContext`. Новые миграции размещаются в `EventManager.Infrastructure/DataAccess/Migrations`. Для команд ниже используется совместимый `dotnet-ef` версии `10.0.12`.

Команды выполняются из корня репозитория. Явно выберите каталог конфигурации:

```powershell
$efConfigurationDirectory = (Resolve-Path .\EventManager.Api).Path
```

`AppDbContextFactory` находится в Infrastructure и создаёт контекст без запуска веб-приложения, фоновых служб и применения миграций. Параметр после `--` выбирает единственный каталог с обязательным `appsettings.json`; без него используется текущий рабочий каталог. Относительный путь считается от текущего рабочего каталога. Другие каталоги автоматически не проверяются.

Источники factory в порядке приоритета: выбранный `appsettings.json`, переменные окружения, аргументы после `--`. Файлы `appsettings.{Environment}.json`, launch profile и user secrets factory не загружает. Задайте `ConnectionStrings__DefaultConnection` во внешнем окружении, как показано выше; отсутствие или пустое значение останавливает создание контекста. Конфигурация и секреты не копируются в Infrastructure. Runtime и factory используют Npgsql и конфигурации из сборки самого `AppDbContext`.

`Microsoft.EntityFrameworkCore.Design` с `PrivateAssets="all"` остаётся в API, через который CLI загружает design-time services. Интерфейс factory доступен Infrastructure из runtime-пакета EF Core. Пакет `Microsoft.EntityFrameworkCore.Tools` не нужен: используется CLI, а не Package Manager Console. Отдельные пакеты конфигурации `10.0.12` заменяют доступ к тем же провайдерам конфигурации через ASP.NET Core; Infrastructure не ссылается на `Microsoft.AspNetCore.App`.

Если изменили модель данных, создайте новую миграцию, заменив `NextMigration` её названием:

```powershell
dotnet ef migrations add NextMigration --project .\EventManager.Infrastructure\EventManager.Infrastructure.csproj --startup-project .\EventManager.Api\EventManager.Api.csproj --context AppDbContext --output-dir DataAccess\Migrations -- --configuration-directory $efConfigurationDirectory
```

Применить миграции вручную:

```powershell
dotnet ef database update --project .\EventManager.Infrastructure\EventManager.Infrastructure.csproj --startup-project .\EventManager.Api\EventManager.Api.csproj --context AppDbContext -- --configuration-directory $efConfigurationDirectory
```

Обе существующие миграции (`20261014122528_InitialCreate`, `20261014145755_RenameColumnsToSnakeCase`), их designer-файлы и snapshot перенесены без изменения содержимого. Пространства имён `EventManager.Api.DataAccess` у контекста и `EventManager.Api.DataAccess.Migrations` у истории сохранены для совместимости generated-файлов; физически эти типы компилируются только в Infrastructure. Обратной ссылки на проект API нет. Контекст, конфигурации и миграции находятся в одной сборке, поэтому отдельный `MigrationsAssembly` не требуется.

Остаётся вопрос метаданных после выделения Domain: snapshot и designer-файлы содержат строковые имена `EventManager.Api.Models.Event` и `EventManager.Api.Models.Booking`, а runtime использует `EventManager.Domain.Entities.Event` и `EventManager.Domain.Entities.Booking`. По статическому сравнению таблицы, столбцы и связи сохранены, но одних строковых имён недостаточно, чтобы установить наличие pending model changes или необходимость новой миграции. Перенос Infrastructure не исправляет это расхождение. Проверка сравнением моделей через EF tooling ещё не выполнена; история вручную не исправлялась, предупреждения не подавлялись. Перед изменением истории отдельно проверьте результат сравнения; этот этап не подтверждает готовность миграций к применению.

Если БД создавалась старой версией проекта без миграций, для запуска используйте новую пустую БД.

## API

| Метод | Путь | Описание | Успешный ответ |
|---|---|---|---|
| `GET` | `/events` | Получить список событий | `200 OK` |
| `GET` | `/events/{id}` | Получить событие по идентификатору | `200 OK` |
| `POST` | `/events` | Создать событие | `201 Created` |
| `POST` | `/events/{id}/book` | Создать бронь для события | `202 Accepted` |
| `PUT` | `/events/{id}` | Обновить данные события | `200 OK` |
| `DELETE` | `/events/{id}` | Удалить событие | `204 No Content` |
| `GET` | `/bookings/{id}` | Получить состояние брони | `200 OK` |

### Модель события

Модель события содержит два поля вместимости:

| Поле | Тип | Описание |
|---|---|---|
| `totalSeats` | `integer` | Общее количество мест. Задаётся при создании события и должно быть больше нуля. |
| `availableSeats` | `integer` | Текущее количество свободных мест. Изначально равно `totalSeats`, уменьшается при бронировании и восстанавливается при отклонении брони. |

### Фильтрация и пагинация

`GET /events` поддерживает параметры строки запроса:

| Параметр | Тип | Значение по умолчанию | Описание |
|---|---|---|---|
| `title` | `string` | — | Часть названия события без учёта регистра |
| `from` | `date-time` | — | Минимальные дата и время начала события включительно |
| `to` | `date-time` | — | Максимальные дата и время окончания события включительно |
| `page` | `integer` | `1` | Номер страницы, начиная с 1 |
| `pageSize` | `integer` | `10` | Количество событий на странице, минимум 1 |

Параметры фильтрации можно комбинировать. Например:

```http
GET /events?title=workshop&from=2030-01-01T00:00:00Z&to=2030-01-31T23:59:59Z&page=1&pageSize=10
```

Ответ содержит события в `items`, общее количество — в `totalCount`, сведения о странице — в `page`, `pageSize` и `totalPages`.

### Создание события

Дата начала не может быть в прошлом. При обновлении эта проверка применяется только при изменении `startAt`, поэтому название и описание уже начавшегося события можно исправить.

Название обязательно и ограничено 200 символами, описание — 2000. Дата окончания должна быть позже начала, а количество мест — больше нуля. Нарушение этих правил возвращает `400 Bad Request`.

`POST /events`

```json
{
  "title": "Воркшоп по ASP.NET Core",
  "description": "Практика разработки Web API.",
  "startAt": "2030-01-01T10:00:00Z",
  "endAt": "2030-01-01T12:00:00Z",
  "totalSeats": 30
}
```

Передавайте даты в UTC с суффиксом `Z`.

Ответ `201 Created` содержит созданное событие и заголовок `Location` со ссылкой на него. Изначально `availableSeats` равно `totalSeats`.

### Обновление события

`PUT /events/{id}`

Тело запроса содержит `title`, `description`, `startAt` и `endAt`. Общее и доступное количество мест при обновлении события сохраняется.

## Бронирования

Чтобы забронировать одно место, выполните `POST /events/{id}/book` без тела запроса. Сервер сразу уменьшит количество свободных мест и вернёт `202 Accepted` с данными брони. Заголовок `Location` указывает адрес для проверки её состояния.

Бронь обрабатывается в фоне и обычно подтверждается через несколько секунд. Проверить результат можно запросом `GET /bookings/{id}`. Поле `status` в JSON имеет следующие значения:

- `0` (`Pending`) — ожидает обработки;
- `1` (`Confirmed`) — подтверждена;
- `2` (`Rejected`) — отклонена.

При завершении обработки заполняется `processedAt`. Если обработка заканчивается отклонением брони, место возвращается событию. При удалении события удаляются и его брони.

## Как попробовать в Swagger

Откройте [Swagger UI](http://localhost:5239/swagger/index.html). Для отправки запроса раскройте операцию, нажмите **Try it out**, заполните поля и нажмите **Execute**.

1. Создайте событие через `POST /Events`, используя пример выше. Скопируйте `id` из ответа.
2. Подставьте этот ID в `POST /Events/{id}/book` и отправьте запрос. Скопируйте ID созданной брони.
3. Через `GET /Events/{id}` проверьте, что свободных мест стало на одно меньше.
4. Через несколько секунд выполните `GET /Bookings/{id}` с ID брони: её статус должен стать `1` (`Confirmed`).
5. Удалите тестовое событие через `DELETE /Events/{id}`.

Для проверки ограничения мест создайте событие с `totalSeats = 3`. Первые три бронирования вернут `202`, четвёртое — `409`: свободных мест больше нет.

## Тесты

### Обычные тесты

Проверяют логику событий и бронирований, ограничение мест и фоновую обработку. PostgreSQL, Docker и запущенный API не нужны.

```powershell
dotnet test --project .\EventManager.Api.Tests\EventManager.Api.Tests.csproj
```

### Интеграционные тесты

Проверяют сохранение и чтение данных, фильтры, пагинацию, миграции и связи между событиями и бронями на настоящем PostgreSQL. Для запуска нужен работающий Docker.

```powershell
dotnet test --project .\EventManager.Api.IntegrationTests\EventManager.Api.IntegrationTests.csproj
```

Тесты автоматически создают отдельный контейнер `postgres:16-alpine` и удаляют его после прогона. Между тестами тестовая БД пересоздаётся. БД приложения не используется, запуск API не нужен.

Для проверки всего проекта выполните сначала обычные тесты, затем интеграционные.

## Ошибки API

- `400 Bad Request` — неверные данные запроса.
- `404 Not Found` — событие или бронь не найдены.
- `409 Conflict` — у события закончились свободные места.

Описание ошибки возвращается в JSON; причина указывается в поле `detail`, ошибки проверки полей — в `errors`.
