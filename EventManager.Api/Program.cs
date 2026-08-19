using EventManager.Api.Extensions;
using EventManager.Api.Middlewares;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    builder.Host.UseDefaultServiceProvider(options =>
    {
        options.ValidateScopes = true;
        options.ValidateOnBuild = true;
    });
}

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

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthorization();
app.MapControllers();

app.Run();
