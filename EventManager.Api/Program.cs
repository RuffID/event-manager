using EventManager.Api.Extensions;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Регистрирует Swagger и подключает XML-документацию API.
builder.Services.AddSwaggerGen(options =>
{
    string xmlFileName = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    string xmlFilePath = Path.Combine(AppContext.BaseDirectory, xmlFileName);

    options.IncludeXmlComments(xmlFilePath);
});

// Внедрение зависимостей для сервисов
builder.Services.AddServices();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.MapControllers();

app.Run();
