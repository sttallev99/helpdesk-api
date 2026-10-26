using System.Reflection;
using FluentValidation;
using HelpDesk.Application.Common.Behaviours;
using Microsoft.Extensions.DependencyInjection;

namespace HelpDesk.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly);

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);
            config.AddOpenBehavior(typeof(ValidationBehaviour<,>));
        });

        return services;
    }
}
