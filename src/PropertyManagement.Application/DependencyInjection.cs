using Microsoft.Extensions.DependencyInjection;
using PropertyManagement.Application.Applications;
using PropertyManagement.Application.Catalog;

namespace PropertyManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ICatalogService, CatalogService>();
        services.AddScoped<IRentalApplicationService, RentalApplicationService>();
        services.AddScoped<IApplicationQueryService, ApplicationQueryService>();
        return services;
    }
}
