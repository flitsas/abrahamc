using Flit.Modules.Notifications.Adapters.SignalR;
using Flit.Modules.Notifications.Ports;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Flit.Modules.Notifications;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registra SignalR + Hub FLIT + publisher.
    /// Llamar desde Flit.Api/Program.cs durante bootstrap.
    /// </summary>
    public static IServiceCollection AddFlitNotifications(this IServiceCollection services)
    {
        services.AddSignalR(o =>
        {
            o.EnableDetailedErrors = false;
            o.KeepAliveInterval = TimeSpan.FromSeconds(15);
            o.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<INotificationsPublisher, SignalRNotificationsPublisher>();
        return services;
    }

    /// <summary>
    /// Mapea el Hub en /hubs/notifications.
    /// Flit.Gateway/YARP routea /hubs/* → core-api con WebSocket upgrade.
    /// </summary>
    public static IEndpointRouteBuilder MapFlitNotificationsHub(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHub<FlitNotificationsHub>("/hubs/notifications");
        return endpoints;
    }
}
