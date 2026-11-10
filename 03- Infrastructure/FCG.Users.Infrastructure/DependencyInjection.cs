using FCG.Users.Application.Abstractions;
using FCG.Users.Application.Services;
using FCG.Users.Application.Services.Interfaces;
using FCG.Users.Application.Validators;
using FCG.Users.Domain.Repositories;
using FCG.Users.Domain.Security;
using FCG.Users.Infrastructure.DbContexts;
using FCG.Users.Infrastructure.Messaging;
using FCG.Users.Infrastructure.Repositories;
using FCG.Users.Infrastructure.Security;
using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FCG.Users.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<UsersDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("DbConnection"), sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(UsersDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 10, maxRetryDelay: TimeSpan.FromSeconds(30), errorCodesToAdd: null);
            });
        });

        services.AddHealthChecks().AddDbContextCheck<UsersDbContext>();

        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserRoleService, UserRoleService>();

        services.AddValidatorsFromAssemblyContaining<RegisterUserDtoValidator>();

        services.AddMessaging(configuration);

        return services;
    }

    private static void AddMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(RabbitMqSettings.SectionName).Get<RabbitMqSettings>() ?? new RabbitMqSettings();

        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        services.AddMassTransit(bus =>
        {
            bus.SetKebabCaseEndpointNameFormatter();

            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(settings.Host, settings.VirtualHost, host =>
                {
                    host.Username(settings.Username);
                    host.Password(settings.Password);
                });

                cfg.UseMessageRetry(retry => retry.Interval(3, TimeSpan.FromSeconds(5)));
                cfg.ConfigureEndpoints(context);
            });
        });
    }
}
