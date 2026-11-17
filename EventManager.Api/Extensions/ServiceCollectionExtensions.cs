using EventManager.Api.BackgroundServices;
using EventManager.Api.Swagger;
using EventManager.Application.Models.Dtos;
using EventManager.Domain.Enums;
using Microsoft.OpenApi;
using System.Reflection;
using System.Text.Json.Serialization;

namespace EventManager.Api.Extensions
{
    /// <summary>
    /// Содержит методы расширения для регистрации Presentation и фонового хостинга.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>Регистрирует контроллеры, Swagger и XML-документацию API и прикладных DTO.</summary>
        /// <param name="services">Коллекция сервисов приложения.</param>
        /// <returns>Коллекция сервисов с регистрациями Presentation.</returns>
        public static IServiceCollection AddPresentation(this IServiceCollection services)
        {
            services.AddControllers().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter<UserRole>());
            });

            // Регистрирует Swagger и подключает XML-документацию API и Application.
            services.AddSwaggerGen(options =>
            {
                string apiXmlFileName = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                string applicationXmlFileName = $"{typeof(EventDto).Assembly.GetName().Name}.xml";

                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, apiXmlFileName));
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, applicationXmlFileName));
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Вставьте JWT из ответа POST /auth/login без префикса Bearer."
                });
                options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                });
                options.OperationFilter<AuthorizationOperationFilter>();
            });

            return services;
        }

        /// <summary>Регистрирует задержку, координатор и хостинг фоновой обработки бронирований.</summary>
        /// <param name="services">Коллекция сервисов приложения.</param>
        /// <returns>Коллекция сервисов с добавленными регистрациями.</returns>
        public static IServiceCollection AddBookingProcessingHosting(this IServiceCollection services)
        {
            services.AddSingleton<IBookingProcessingDelay, BookingProcessingDelay>();
            services.AddSingleton<BookingProcessor>();
            services.AddHostedService<BookingProcessingService>();

            return services;
        }
    }
}
