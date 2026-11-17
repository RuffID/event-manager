using EventManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventManager.Infrastructure.DataAccess.Configurations
{
    /// <summary>Настраивает хранение пользователей и уникальность логина.</summary>
    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        /// <summary>Имя уникального индекса логина для распознавания конфликта при сохранении.</summary>
        public const string LOGIN_INDEX_NAME = "ix_users_login";

        /// <summary>Описывает таблицу пользователей и её свойства.</summary>
        /// <param name="builder">Построитель конфигурации пользователя.</param>
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.ToTable("users");
            builder.HasKey(user => user.Id);
            builder.Property(user => user.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(user => user.Login).HasColumnName("login").IsRequired();
            builder.HasIndex(user => user.Login).IsUnique().HasDatabaseName(LOGIN_INDEX_NAME);
            builder.Property(user => user.PasswordHash).HasColumnName("password_hash").IsRequired();
            builder.Property(user => user.Role)
                .HasColumnName("role")
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
        }
    }
}
