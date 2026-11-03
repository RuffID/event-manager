using EventManager.Api.Extensions;
using EventManager.Api.Middlewares;
using EventManager.Application.Extensions;
using EventManager.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes = true;
        options.ValidateOnBuild = true;
    });
}

builder.Services.AddPresentation();

// Внедрение зависимостей для сервисов
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddBookingProcessingHosting();

var app = builder.Build();

app.Services.ApplyDatabaseMigrations();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();
