using EventManager.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Внедрение зависимостей для сервисов
builder.Services.AddServices();

var app = builder.Build();

app.UseAuthorization();
app.MapControllers();

app.Run();
