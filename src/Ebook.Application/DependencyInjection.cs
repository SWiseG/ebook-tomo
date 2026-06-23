using Ebook.Application.Common.Events;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Messaging;
using Ebook.Application.Common.Realtime;
using Ebook.Application.Knowledge;
using Ebook.Application.Optimization;
using Microsoft.Extensions.DependencyInjection;

namespace Ebook.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registra o dispatcher e, por varredura do assembly, todos os
    /// ICommandHandler/IQueryHandler/IDomainEventHandler/IJobHandler.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<Content.Images.IPaletteResolver, Content.Images.PaletteResolver>();
        services.AddScoped<IKnowledgeService, KnowledgeService>();
        services.AddScoped<IOptimizationService, OptimizationService>();
        services.AddScoped<IOptimizationExecutor, OptimizationExecutor>();

        // notificações em tempo real: no-op por padrão; a Api sobrescreve com o SignalR
        services.AddSingleton<IRealtimeNotifier, NullRealtimeNotifier>();

        var assembly = typeof(DependencyInjection).Assembly;
        Type[] handlerOpenTypes =
            [typeof(ICommandHandler<,>), typeof(IQueryHandler<,>), typeof(IDomainEventHandler<>)];

        foreach (var type in assembly.GetTypes().Where(t => t is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var iface in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                if (handlerOpenTypes.Contains(iface.GetGenericTypeDefinition()))
                {
                    services.AddScoped(iface, type);
                }
            }

            if (typeof(IJobHandler).IsAssignableFrom(type))
            {
                services.AddScoped(typeof(IJobHandler), type);
            }
        }

        return services;
    }
}
