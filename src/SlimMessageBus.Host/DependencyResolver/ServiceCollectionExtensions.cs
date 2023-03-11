﻿namespace SlimMessageBus.Host;

using System.Reflection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers SlimMessageBus (<see cref="IMessageBus">) master bus instance and required mbb. This can be called multiple times and the result will be additive.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="configure">Action to configure the master (root) message bus</param>
    /// <returns></returns>
    public static IServiceCollection AddSlimMessageBus(this IServiceCollection services, Action<MessageBusBuilder> configure = null)
    {
        if (configure is not null)
        {
            // Register the configure action as LambdaMessageBusConfigurator
            services.AddTransient<IMessageBusConfigurator>(svp => new LambdaMessageBusConfigurator(busName: null, configure));

            // Execute the mbb setup for services registration
            var mbb = MessageBusBuilder.Create();
            mbb.Services = services;
            configure(mbb);
        }
        return services.AddSlimMessageBus();
    }

    /// <summary>
    /// Registers SlimMessageBus (<see cref="IMessageBus">) master bus instance and required mbb. This can be called multiple times and the result will be additive.
    /// Ensure an implementation of <see cref="IMessageBusConfigurator"/> is configured in the DI to configure the bus.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddSlimMessageBus(this IServiceCollection services)
    {
        // Register MessageBusBuilder for the root bus
        services.TryAddSingleton(svp =>
        {
            var configurators = svp.GetServices<IMessageBusConfigurator>();

            var mbb = MessageBusBuilder.Create();
            mbb.WithDependencyResolver(svp);

            // Let the configurators visit the root bus
            foreach (var configurator in configurators)
            {
                configurator.Configure(mbb, mbb.Settings.Name);
            }

            // Let the configurators visit every child bus
            foreach (var child in mbb.Children.Values)
            {
                foreach (var configurator in configurators)
                {
                    configurator.Configure(child, child.Settings.Name);
                }
            }

            return mbb;
        });

        // Single master bus that holds the defined consumers and message processing pipelines
        services.TryAddSingleton(svp =>
        {
            var mbb = svp.GetRequiredService<MessageBusBuilder>();

            // Set the MessageBus.Current
            var currentBusProvider = svp.GetRequiredService<ICurrentMessageBusProvider>();
            MessageBus.SetProvider(currentBusProvider.GetCurrent);

            return (IMasterMessageBus)mbb.Build();
        });

        services.TryAddTransient<IConsumerControl>(svp => svp.GetRequiredService<IMasterMessageBus>());
        services.TryAddTransient<ITopologyControl>(svp => svp.GetRequiredService<IMasterMessageBus>());

        // Register transient message bus - this is a lightweight proxy that just introduces the current DI scope
        services.TryAddTransient(svp => new MessageBusProxy(svp.GetRequiredService<IMasterMessageBus>(), svp));

        services.TryAddTransient<IMessageBus>(svp => svp.GetRequiredService<MessageBusProxy>());
        services.TryAddTransient<IPublishBus>(svp => svp.GetRequiredService<MessageBusProxy>());
        services.TryAddTransient<IRequestResponseBus>(svp => svp.GetRequiredService<MessageBusProxy>());

        services.TryAddSingleton<ICurrentMessageBusProvider, CurrentMessageBusProvider>();

        services.AddHostedService<MessageBusHostedService>();

        return services;
    }

    /// <summary>
    /// Scans the specified assemblies (using reflection) for types that implement any consumer/handler interface, any interceptor interface or message bus configurator interface (<see cref="IMessageBusConfigurator"/>).
    /// The found types are registered in the DI as Transient service (both the consumer type and its interface are registered).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="mbb"></param>
    /// <param name="assembly"></param>
    /// <param name="filterPredicate"></param>
    /// <returns></returns>
    public static MessageBusBuilder AddServicesFromAssembly(this MessageBusBuilder mbb, Assembly assembly, Func<Type, bool> filterPredicate = null)
    {
        var services = mbb.Services;
        if (services is null)
        {
            return mbb;
        }
        
        var scan = ReflectionDiscoveryScanner.From(assembly);

        var foundConsumerTypes = scan.GetConsumerTypes(filterPredicate);
        foreach (var (foundType, interfaceTypes) in foundConsumerTypes.GroupBy(x => x.ConsumerType, x => x.InterfaceType).ToDictionary(x => x.Key, x => x))
        {
            // Register the consumer/handler type
            services.TryAddTransient(foundType);

            foreach (var interfaceType in interfaceTypes)
            {
                // Register the interface of the consumer / handler
                services.TryAddTransient(interfaceType, foundType);
            }
        }

        var foundInterceptorTypes = scan.GetInterceptorTypes();
        foreach (var foundType in foundInterceptorTypes)
        {
            services.AddTransient(foundType.InterfaceType, foundType.Type);
        }

        var foundConfiguratorTypes = scan.GetMessageBusConfiguratorTypes();
        foreach (var foundType in foundConfiguratorTypes)
        {
            services.AddTransient(typeof(IMessageBusConfigurator), foundType);
        }

        return mbb;
    }

    /// <summary>
    /// Scans the specified assemblies (using reflection) for types that implement any consumer/handler interface, any interceptor interface or message bus configurator interface (<see cref="IMessageBusConfigurator"/>).
    /// The found types are registered in the DI as Transient service (both the consumer type and its interface are registered).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="mbb"></param>
    /// <param name="filterPredicate"></param>
    /// <returns></returns>
    public static MessageBusBuilder AddServicesFromAssemblyContaining<T>(this MessageBusBuilder mbb, Func<Type, bool> filterPredicate = null) =>
        mbb.AddServicesFromAssembly(typeof(T).Assembly, filterPredicate);

    #region Obsolete

    /// <summary>
    /// Scans the specified assemblies (using reflection) for types that implement either <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/> or <see cref="IRequestHandler{TRequest}"/>. 
    /// The found types are registered in the DI as Transient service (both the consumer type and its interface are registered).
    /// </summary>
    /// <param name="services"></param>
    /// <param name="filterPredicate">Filtering predicate that allows to further narrow down the </param>
    /// <param name="assemblies">Assemblies to be scanned</param>
    /// <returns></returns>
    [Obsolete("Use the new mbb.AddServicesFromAssembly() or mbb.AddServicesFromAssemblyContaining()")]
    public static IServiceCollection AddMessageBusConsumersFromAssembly(this IServiceCollection services, Func<Type, bool> filterPredicate, params Assembly[] assemblies)
    {
        var foundTypes = ReflectionDiscoveryScanner.From(assemblies).GetConsumerTypes(filterPredicate);
        foreach (var (foundType, interfaceTypes) in foundTypes.GroupBy(x => x.ConsumerType, x => x.InterfaceType).ToDictionary(x => x.Key, x => x))
        {
            // Register the consumer/handler type
            services.TryAddTransient(foundType);

            foreach (var interfaceType in interfaceTypes)
            {
                // Register the interface of the consumer / handler
                services.TryAddTransient(interfaceType, foundType);
            }
        }

        return services;
    }

    /// <summary>
    /// Scans the specified assemblies (using reflection) for types that implement either <see cref="IConsumer{TMessage}"/> or <see cref="IRequestHandler{TRequest, TResponse}"/> or <see cref="IRequestHandler{TRequest}"/>. 
    /// The found types are registered in the DI as Transient service.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assemblies">Assemblies to be scanned</param>
    /// <returns></returns>
    [Obsolete("Use the new mbb.AddServicesFromAssembly() or mbb.AddServicesFromAssemblyContaining()")]
    public static IServiceCollection AddMessageBusConsumersFromAssembly(this IServiceCollection services, params Assembly[] assemblies)
        => services.AddMessageBusConsumersFromAssembly(filterPredicate: null, assemblies);


    /// <summary>
    /// Scans the specified assemblies (using reflection) for types that implement <see cref="IMessageBusConfigurator{TMessage}"/> and adds them to DI.
    /// This types will be use during message bus configuration.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assemblies">Assemblies to be scanned</param>
    /// <returns></returns>
    [Obsolete("Use the new mbb.AddServicesFromAssembly() or mbb.AddServicesFromAssemblyContaining()")]
    public static IServiceCollection AddMessageBusConfiguratorsFromAssembly(this IServiceCollection services, params Assembly[] assemblies)
    {
        var foundTypes = ReflectionDiscoveryScanner.From(assemblies).GetMessageBusConfiguratorTypes();
        foreach (var foundType in foundTypes)
        {
            services.AddTransient(typeof(IMessageBusConfigurator), foundType);
        }

        return services;
    }

    /// <summary>
    /// Scans the specified assemblies (using reflection) for types that implement one of the interceptor interfaces (<see cref="IPublishInterceptor{TMessage}"/> or <see cref="IConsumerInterceptor{TMessage}"/>) and adds them to DI.
    /// This types will be use during message bus configuration.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="assemblies">Assemblies to be scanned</param>
    /// <returns></returns>
    [Obsolete("Use the new mbb.AddServicesFromAssembly() or mbb.AddServicesFromAssemblyContaining()")]
    public static IServiceCollection AddMessageBusInterceptorsFromAssembly(this IServiceCollection services, params Assembly[] assemblies)
    {
        var foundTypes = ReflectionDiscoveryScanner.From(assemblies).GetInterceptorTypes();
        foreach (var foundType in foundTypes)
        {
            services.AddTransient(foundType.InterfaceType, foundType.Type);
        }

        return services;
    }

    #endregion
}
