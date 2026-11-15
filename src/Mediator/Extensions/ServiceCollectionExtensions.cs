// Copyright (c) 2025-2026 Pierre G. Boutquin. All rights reserved.
//
//   Licensed under the Apache License, Version 2.0 (the "License").
//   You may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//
//   See the License for the specific language governing permissions and
//   limitations under the License.
//

namespace Boutquin.Mediator.Extensions;

using System.Reflection;
// Boutquin.Mediator
using Abstractions;
using Microsoft.Extensions.DependencyInjection;

// ReSharper disable once GrammarMistakeInComment
/// <summary>
/// Provides extension methods for registering mediator patterns and related components into an <see
/// cref="IServiceCollection"/>.
/// </summary>
/// <remarks>This static class includes methods to simplify the registration of mediators, request handlers,
/// pre-processors, and pipeline behaviors into the dependency injection container. These methods are designed to scan
/// assemblies for relevant implementations and register them automatically, reducing boilerplate code for setting up
/// mediator-based architectures.</remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds the mediator and its associated command handlers to the service collection.
    /// </summary>
    /// <remarks>This method registers the <see cref="IMediator"/> interface with a concrete implementation 
    /// and scans the specified assemblies for command handler types to register them automatically.</remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the mediator and handlers will be added.</param>
    /// <param name="assemblies">An optional array of assemblies to scan for command handler implementations.  If no assemblies are provided, the
    /// calling assembly is used by default.</param>
    /// <returns>The same <see cref="IServiceCollection"/> instance, allowing for method chaining.</returns>
    public static IServiceCollection AddMediator(
        this IServiceCollection services,
        params Assembly[]? assemblies)
    {
        // If no assemblies are provided, use the calling assembly by default
        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        // Register the IMediator interface with a concrete implementation.
        // Scoped (not Singleton) so the mediator captures the per-request IServiceProvider:
        // a singleton mediator captures the ROOT provider, which makes scoped handler
        // dependencies unresolvable under ValidateScopes=true and leaks transient
        // IDisposable handlers until root disposal (the captive-dependency defect).
        _ = services.AddScoped<IMediator, Helpers.Mediator>();

        // Forward the segregated interfaces to the same per-scope IMediator instance.
        // MS.DI does not automatically expose base-interface keys when registering a
        // derived interface, so consumers with ctor(ISender), ctor(IPublisher), or
        // ctor(IStreamer) — the natural way to use SenderExtensions / PublisherExtensions —
        // would get InvalidOperationException without these forwarding registrations.
        // The factory delegate re-uses the already-resolved IMediator from the scope,
        // so no second Mediator is ever constructed.
        _ = services.AddScoped<ISender>(sp => sp.GetRequiredService<IMediator>());
        _ = services.AddScoped<IPublisher>(sp => sp.GetRequiredService<IMediator>());
        _ = services.AddScoped<IStreamer>(sp => sp.GetRequiredService<IMediator>());

        // Register all request pre-processors from the specified assemblies
        _ = services.AddPreProcessors(assemblies);

        // Register all behaviors from the specified assemblies
        _ = services.AddBehaviors(assemblies);

        // Register all request handlers from the specified assemblies
        _ = services.AddRequestHandlers(assemblies);

        // Register all stream request handlers from the specified assemblies
        _ = services.AddStreamHandlers(assemblies);

        // Register all request post-processors from the specified assemblies
        _ = services.AddPostProcessors(assemblies);

        // Register all notification handlers from the specified assemblies
        _ = services.AddNotificationHandlers(assemblies);

        return services;
    }

    /// <summary>
    /// Registers all implementations of <see cref="IRequestHandler{TRequest, TResponse}"/> found in the specified
    /// assemblies with the dependency injection container.
    /// </summary>
    /// <remarks>This method scans the specified assemblies for classes that implement <see
    /// cref="IRequestHandler{TRequest, TResponse}"/> and registers them with a transient lifetime. Multiple
    /// implementations of the same interface are all registered; the container resolves each one in turn when
    /// <see cref="Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetServices{T}"/> is called.</remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the request handlers will be added.</param>
    /// <param name="assemblies">The assemblies to scan for implementations of <see cref="IRequestHandler{TRequest, TResponse}"/>. If no
    /// assemblies are provided, the calling assembly is used by default.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddRequestHandlers(
        this IServiceCollection services,
        params Assembly[]? assemblies)
    {
        // If no assemblies are provided, use the calling assembly by default
        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        // Register all request handlers from the specified assemblies
        foreach (var assembly in assemblies)
        {
            // Scan the assembly for classes that implement
            // IRequestHandler<TRequest, TResponse>
            _ = services.Scan(scan => scan
                .FromAssemblies(assembly)
                .AddClasses(classes => classes.AssignableTo(typeof(IRequestHandler<,>)))
                .AsImplementedInterfaces()
                .WithTransientLifetime());
        }

        // Return the updated service collection
        return services;
    }

    /// <summary>
    /// Registers all request pre-processors from the specified assemblies into the service collection.
    /// </summary>
    /// <remarks>This method scans the specified assemblies for classes that implement the <see
    /// cref="IRequestPreProcessor{TRequest}"/> interface and registers them with a transient lifetime. Pre-processors
    /// are used to perform operations before handling a request.</remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the pre-processors will be added.</param>
    /// <param name="assemblies">An array of <see cref="Assembly"/> objects to scan for request pre-processors.  If no assemblies are provided,
    /// the calling assembly is used by default.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> with the registered pre-processors.</returns>
    public static IServiceCollection AddPreProcessors(
        this IServiceCollection services,
        params Assembly[]? assemblies)
    {
        // If no assemblies are provided, use the calling assembly by default
        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        // Register all request pre-processors from the specified assemblies
        foreach (var assembly in assemblies)
        {
            // Scan the assembly for classes that implement IRequestPreProcessor<TRequest>
            _ = services.Scan(scan => scan
                .FromAssemblies(assembly)
                .AddClasses(classes => classes.AssignableTo(typeof(IRequestPreProcessor<>)))
                .AsImplementedInterfaces()
                .WithTransientLifetime());
        }

        // Return the updated service collection
        return services;
    }

    /// <summary>
    /// Registers all implementations of <see cref="IPipelineBehavior{TRequest, TResponse}"/>  from the specified
    /// assemblies into the service collection with a transient lifetime.
    /// </summary>
    /// <remarks>This method scans the specified assemblies for classes that implement  <see
    /// cref="IPipelineBehavior{TRequest, TResponse}"/> and registers them as their  implemented interfaces with a
    /// transient lifetime. If no assemblies are provided,  the calling assembly is scanned by default.</remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the behaviors will be added.</param>
    /// <param name="assemblies">An array of assemblies to scan for implementations of <see cref="IPipelineBehavior{TRequest, TResponse}"/>. If
    /// no assemblies are provided, the calling assembly is used by default.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> with the registered pipeline behaviors.</returns>
    public static IServiceCollection AddBehaviors(
        this IServiceCollection services,
        params Assembly[]? assemblies)
    {
        // If no assemblies are provided, use the calling assembly by default
        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        // Register all pipeline behaviors from the specified assemblies
        foreach (var assembly in assemblies)
        {
            // Scan the assembly for classes that implement IPipelineBehavior<TRequest, TResponse>
            _ = services.Scan(scan => scan
                .FromAssemblies(assembly)
                .AddClasses(classes => classes.AssignableTo(typeof(IPipelineBehavior<,>)))
                .AsImplementedInterfaces()
                .WithTransientLifetime());
        }

        // Return the updated service collection
        return services;
    }

    /// <summary>
    /// Registers all implementations of <see cref="IStreamRequestHandler{TRequest, TResponse}"/> found in the specified
    /// assemblies with the dependency injection container.
    /// </summary>
    /// <remarks>This method scans the specified assemblies for classes that implement <see
    /// cref="IStreamRequestHandler{TRequest, TResponse}"/> and registers them with a transient lifetime. Stream
    /// request handlers are used to handle streaming requests and produce asynchronous sequences of results.</remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the stream request handlers will be added.</param>
    /// <param name="assemblies">The assemblies to scan for implementations of <see cref="IStreamRequestHandler{TRequest, TResponse}"/>. If no
    /// assemblies are provided, the calling assembly is used by default.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddStreamHandlers(
        this IServiceCollection services,
        params Assembly[]? assemblies)
    {
        // If no assemblies are provided, use the calling assembly by default
        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        // Register all stream request handlers from the specified assemblies
        foreach (var assembly in assemblies)
        {
            // Scan the assembly for classes that implement IStreamRequestHandler<TRequest, TResponse>
            _ = services.Scan(scan => scan
                .FromAssemblies(assembly)
                .AddClasses(classes => classes.AssignableTo(typeof(IStreamRequestHandler<,>)))
                .AsImplementedInterfaces()
                .WithTransientLifetime());
        }

        // Return the updated service collection
        return services;
    }

    /// <summary>
    /// Registers all implementations of <see cref="INotificationHandler{TNotification}"/> found in the specified
    /// assemblies with the dependency injection container.
    /// </summary>
    /// <remarks>This method scans the specified assemblies for classes that implement <see
    /// cref="INotificationHandler{TNotification}"/> and registers them with a transient lifetime. Multiple
    /// implementations for the same notification type are all registered so that every handler is invoked
    /// when the notification is published.</remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the notification handlers will be added.</param>
    /// <param name="assemblies">The assemblies to scan for implementations of <see cref="INotificationHandler{TNotification}"/>. If no
    /// assemblies are provided, the calling assembly is used by default.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> instance.</returns>
    public static IServiceCollection AddNotificationHandlers(
        this IServiceCollection services,
        params Assembly[]? assemblies)
    {
        // If no assemblies are provided, use the calling assembly by default
        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        // Register all notification handlers from the specified assemblies
        foreach (var assembly in assemblies)
        {
            _ = services.Scan(scan => scan
                .FromAssemblies(assembly)
                .AddClasses(classes => classes.AssignableTo(typeof(INotificationHandler<>)))
                .AsImplementedInterfaces()
                .WithTransientLifetime());
        }

        return services;
    }

    /// <summary>
    /// Registers all implementations of <see cref="IRequestPostProcessor{TRequest, TResponse}"/> found in the specified
    /// assemblies with the dependency injection container.
    /// </summary>
    /// <remarks>This method scans the specified assemblies for classes that implement <see
    /// cref="IRequestPostProcessor{TRequest, TResponse}"/> and registers them with a transient lifetime. Post-processors
    /// are invoked after the request handler completes, in registration order, and are typically used for logging,
    /// auditing, or side-effect dispatch.</remarks>
    /// <param name="services">The <see cref="IServiceCollection"/> to which the post-processors will be added.</param>
    /// <param name="assemblies">An array of <see cref="Assembly"/> objects to scan for request post-processors. If no assemblies are provided,
    /// the calling assembly is used by default.</param>
    /// <returns>The updated <see cref="IServiceCollection"/> with the registered post-processors.</returns>
    public static IServiceCollection AddPostProcessors(
        this IServiceCollection services,
        params Assembly[]? assemblies)
    {
        // If no assemblies are provided, use the calling assembly by default
        if (assemblies == null || assemblies.Length == 0)
        {
            assemblies = [Assembly.GetCallingAssembly()];
        }

        // Register all request post-processors from the specified assemblies
        foreach (var assembly in assemblies)
        {
            // Scan the assembly for classes that implement IRequestPostProcessor<TRequest, TResponse>
            _ = services.Scan(scan => scan
                .FromAssemblies(assembly)
                .AddClasses(classes => classes.AssignableTo(typeof(IRequestPostProcessor<,>)))
                .AsImplementedInterfaces()
                .WithTransientLifetime());
        }

        // Return the updated service collection
        return services;
    }
}
