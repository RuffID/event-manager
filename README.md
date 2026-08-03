# Event Manager

Учебный REST API для создания и управления событиями на ASP.NET Core.

## Запуск

Для запуска нужен .NET SDK 10.

```powershell
dotnet run --project .\EventManager.Api\EventManager.Api.csproj
```

После запуска Swagger UI будет доступен по адресу:

```text
http://localhost:5239/swagger
```

Если порт переопределён в настройках запуска, его актуальное значение отображается в консоли приложения.

## API

Базовый путь: `/api/events`.

| Метод | Путь | Описание | Успешный ответ |
|---|---|---|---|
| `GET` | `/api/events` | Получить список событий | `200 OK` |
| `GET` | `/api/events/{id}` | Получить событие по идентификатору | `200 OK` |
| `POST` | `/api/events` | Создать событие | `201 Created` |
| `PUT` | `/api/events/{id}` | Полностью обновить событие | `200 OK` |
| `DELETE` | `/api/events/{id}` | Удалить событие | `204 No Content` |

Если событие с указанным идентификатором не найдено, `GET`, `PUT` и `DELETE` вернут `404 Not Found`.

### Создание события

`POST /api/events`

```json
{
  "title": "Воркшоп по ASP.NET Core",
  "description": "Практика разработки Web API.",
  "startAt": "2026-09-19T11:00:00",
  "endAt": "2026-09-19T14:00:00"
}
```

Успешный ответ содержит созданное событие и заголовок `Location` со ссылкой на него:

```text
Location: /api/events/{id}
```

### Обновление события

`PUT /api/events/{id}`

Тело запроса имеет тот же формат, что и при создании события.

## Валидация

- `title`, `startAt` и `endAt` обязательны;
- `title` не может быть пустым или состоять только из пробелов;
- дата окончания должна быть позже даты начала.

При невалидных данных API возвращает `400 Bad Request`. Если событие не найдено, API возвращает `404 Not Found`. Ошибки сервисного слоя представлены в формате `ProblemDetails`:

```json
{
  "title": "Request processing failed",
  "status": 404,
  "detail": "Event not found."
}
```