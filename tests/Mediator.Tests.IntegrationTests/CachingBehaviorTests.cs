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

namespace Boutquin.Mediator.Tests.IntegrationTests;

// Boutquin.Mediator
using Abstractions;
using Abstractions.Extensions;
using Boutquin.Abstractions.Auth;
using Boutquin.Domain.Abstractions;
using Caching;
// Boutquin.Mediator.Tests
using Common;
using Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
/// Contains unit tests for verifying the behavior of the <c>CachingBehavior</c> in various scenarios.
/// </summary>
/// <remarks>This class includes tests for processing commands and queries with and without caching,  as well as
/// scenarios involving per-user caching, disabled caching, and cache resets.  The tests ensure that the
/// <c>CachingBehavior</c> integrates correctly with the mediator pipeline  and behaves as expected under different
/// conditions.</remarks>
public class CachingBehaviorTests
{
    /// <summary>
    /// Represents a constant user identifier.
    /// </summary>
    /// <remarks>This constant is used to uniquely identify a specific user within the system.</remarks>
    private const string UserId1 = "2E9076B4-5737-44F6-AC20-307FE3AA4FBD";

    /// <summary>
    /// Verifies that the mediator correctly processes plain commands without caching,  ensuring the expected behavior
    /// of the command handler and logging mechanisms.
    /// </summary>
    /// <remarks>This test sets up a mediator with a caching behavior and verifies that plain commands  are
    /// processed successfully without caching being applied. It also ensures that the  appropriate log messages are
    /// generated during the command's lifecycle.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task BehaviorShouldCorrectlyProcessPlainCommands()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        // Add Mediator services, including the CachingBehavior
        _ = services.AddMediator(
                typeof(SimpleCommand.Handler).Assembly,
                typeof(CachingBehavior<,>).Assembly);
        // Add memory cache for distributed caching
        _ = services.AddDistributedMemoryCache();
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Set up ILogger mocks
        var loggerMock = new Mock<ILogger<SimpleCommand.Command>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Add the JSON configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        _ = services.AddSingleton<IConfiguration>(builder.Build());
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple command to process
        var command = new SimpleCommand.Command("Hello, World!");

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        // Process the command using the mediator
        var result = await mediator.SendAsync(command);

        //--------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        // Verify that the command was processed successfully
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, "The command should have been processed successfully.");
        // Verify that the handler logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Command received: [Hello, World!], expected to fail: [False]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Command executed successfully."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        // Verify that the behavior logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "Caching is not applied for request [Boutquin.Mediator.Tests.Common.SimpleCommand+Command]. Proceeding without caching."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    /// <summary>
    /// Verifies that the mediator correctly processes plain queries and ensures the expected behavior of the caching
    /// and logging mechanisms.
    /// </summary>
    /// <remarks>This test validates the integration of the mediator, caching behavior, and logging for a
    /// simple query.  It ensures that the query is processed successfully, the output is as expected, and the
    /// appropriate log messages are generated.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task BehaviorShouldCorrectlyProcessPlainQueries()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        // Add Mediator services, including the CachingBehavior
        _ = services.AddMediator(
            typeof(SimpleQuery.Handler).Assembly,
            typeof(CachingBehavior<,>).Assembly);
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Add memory cache for distributed caching
        _ = services.AddDistributedMemoryCache();
        // Set up ILogger mock
        var loggerMock = new Mock<ILogger<SimpleQuery.Query>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Add the JSON configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        _ = services.AddSingleton<IConfiguration>(builder.Build());
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple query to process
        var query = new SimpleQuery.Query("Ping...");

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        // Process the command using the mediator
        var result = await mediator.SendAsync(query);

        //--------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        // Verify that the query was processed successfully
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, "The query should have been processed successfully.");
        // Verify that the output is correct
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.Reply);
        Assert.Equal("Processed message: [Ping...].", result.Value.Reply);
        // Verify that the logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Query received: [Ping...], expected to fail: [False]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Query executed successfully."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        // Verify that the behavior logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "Caching is not applied for request [Boutquin.Mediator.Tests.Common.SimpleQuery+Query]. Proceeding without caching."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    /// <summary>
    /// Verifies that the caching behavior processes a query correctly and ensures that cached queries are handled only
    /// once.
    /// </summary>
    /// <remarks>This test ensures that the mediator correctly integrates with the caching behavior, processes
    /// the query as expected, and logs the appropriate messages. It validates that the caching mechanism retrieves and
    /// stores responses as intended, and that duplicate processing of cached queries does not occur.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task BehaviorShouldCorrectlyProcessCachedQueriesOnce()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        // Add Mediator services, including the CachingBehavior
        _ = services.AddMediator(
            typeof(SimpleQueryWithCaching.Handler).Assembly,
            typeof(CachingBehavior<,>).Assembly);
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Add memory cache for distributed caching
        _ = services.AddDistributedMemoryCache();
        // Set up ILogger mock
        var loggerMock = new Mock<ILogger<SimpleQueryWithCaching.Query>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Add the JSON configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        _ = services.AddSingleton<IConfiguration>(builder.Build());
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple query to process
        var query = new SimpleQueryWithCaching.Query("Ping...");

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        // Process the command using the mediator
        var result = await mediator.SendAsync(query);

        //--------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        // Verify that the query was processed successfully
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, "The query should have been processed successfully.");
        // Verify that the output is correct
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.Reply);
        Assert.Equal("Processed message: [Ping...].", result.Value.Reply);
        // Verify that the logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Query received: [Ping...], expected to fail: [False]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Query executed successfully."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        // Verify that the behavior logger was NOT called with the non-caching message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "Caching is not applied for request [Boutquin.Mediator.Tests.Common.SimpleQuery+Query]. Proceeding without caching."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Never);
        // Verify that the behavior logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "Attempting to retrieve cached response for request [Boutquin.Mediator.Tests.Common.SimpleQueryWithCaching+Query] with cache key [|Boutquin.Mediator.Tests.Common.SimpleQueryWithCaching+Query|$Message:[Ping...]|$Fail:[False]]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "Caching response for request [Boutquin.Mediator.Tests.Common.SimpleQueryWithCaching+Query] with cache key [|Boutquin.Mediator.Tests.Common.SimpleQueryWithCaching+Query|$Message:[Ping...]|$Fail:[False]]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    /// <summary>
    /// Verifies that the caching behavior processes queries correctly when executed twice with different properties.
    /// </summary>
    /// <remarks>This test ensures that the caching mechanism correctly handles queries with distinct
    /// properties,  caching the appropriate responses and logging the expected messages. It validates that the mediator
    /// processes the queries successfully, caches the results, and logs the correct debug and information
    /// messages.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task BehaviorShouldCorrectlyProcessCachedQueriesTwiceWithDifferentProperties()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        // Add Mediator services, including the CachingBehavior
        _ = services.AddMediator(
            typeof(SimpleQueryWithCaching.Handler).Assembly,
            typeof(CachingBehavior<,>).Assembly);
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Add memory cache for distributed caching
        _ = services.AddDistributedMemoryCache();
        // Set up ILogger mock
        var loggerMock = new Mock<ILogger<SimpleQueryWithCaching.Query>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Add the JSON configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        _ = services.AddSingleton<IConfiguration>(builder.Build());
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple query to process
        var query = new SimpleQueryWithCaching.Query("Ping...");

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        // Process the query once using the mediator
        var result = await mediator.SendAsync(new SimpleQueryWithCaching.Query("Not Ping"));
        // Verify that the query was processed successfully
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, "The query should have been processed successfully.");
        loggerMock.Reset();
        // Process the query again using the mediator
        result = await mediator.SendAsync(query);

        //--------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        // Verify that the query was processed successfully
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, "The query should have been processed successfully.");
        // Verify that the output is correct
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.Reply);
        Assert.Equal("Processed message: [Ping...].", result.Value.Reply);
        // Verify that the logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Query received: [Ping...], expected to fail: [False]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Query executed successfully."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        // Verify that the behavior logger was NOT called with the non-caching message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "Caching is not applied for request [Boutquin.Mediator.Tests.Common.SimpleQuery+Query]. Proceeding without caching."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Never);
        // Verify that the behavior logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "Attempting to retrieve cached response for request [Boutquin.Mediator.Tests.Common.SimpleQueryWithCaching+Query] with cache key [|Boutquin.Mediator.Tests.Common.SimpleQueryWithCaching+Query|$Message:[Ping...]|$Fail:[False]]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "Caching response for request [Boutquin.Mediator.Tests.Common.SimpleQueryWithCaching+Query] with cache key [|Boutquin.Mediator.Tests.Common.SimpleQueryWithCaching+Query|$Message:[Ping...]|$Fail:[False]]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    /// <summary>
    /// Verifies that a cached query is correctly processed twice using the mediator, ensuring that the second execution
    /// retrieves the result from the cache.
    /// </summary>
    /// <remarks>This test ensures that the caching behavior is functioning as expected by verifying that the
    /// second execution of the same query: <list type="bullet"> <item>Retrieves the result from the cache instead of
    /// reprocessing the query.</item> <item>Does not trigger any logging related to query execution.</item> </list> The
    /// test uses a mock logger to confirm that no unnecessary processing occurs during the second execution.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task BehaviorShouldCorrectlyProcessCachedQueriesTwice()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        // Add Mediator services, including the CachingBehavior
        _ = services.AddMediator(
            typeof(SimpleQueryWithCaching.Handler).Assembly,
            typeof(CachingBehavior<,>).Assembly);
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Add memory cache for distributed caching
        _ = services.AddDistributedMemoryCache();
        // Set up ILogger mock
        var loggerMock = new Mock<ILogger<SimpleQueryWithCaching.Query>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Add the JSON configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        _ = services.AddSingleton<IConfiguration>(builder.Build());
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple query to process
        var query = new SimpleQueryWithCaching.Query("Ping...");

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        // Process the query once using the mediator
        var result = await mediator.SendAsync(query);
        // Verify that the query was processed successfully
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, "The query should have been processed successfully.");
        loggerMock.Reset();
        // Process the query again using the mediator
        result = await mediator.SendAsync(query);

        //--------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        // Verify that the query was processed successfully
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, "The query should have been processed successfully.");
        // Verify that the output is correct
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.Reply);
        Assert.Equal("Processed message: [Ping...].", result.Value.Reply);
        // Verify that the logger was NOT called (since the query was cached)
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Query received: [Ping...], expected to fail: [False]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Never);
        loggerMock.Verify(
            x => x.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Query executed successfully."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Never);
        // Verify that the behavior logger was NOT called with the non-caching message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "Caching is not applied for request [Boutquin.Mediator.Tests.Common.SimpleQuery+Query]. Proceeding without caching."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Never);
        // Verify that the behavior logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "Attempting to retrieve cached response for request [Boutquin.Mediator.Tests.Common.SimpleQueryWithCaching+Query] with cache key [|Boutquin.Mediator.Tests.Common.SimpleQueryWithCaching+Query|$Message:[Ping...]|$Fail:[False]]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "Retrieved cached response for request [Boutquin.Mediator.Tests.Common.SimpleQueryWithCaching+Query] with cache key [|Boutquin.Mediator.Tests.Common.SimpleQueryWithCaching+Query|$Message:[Ping...]|$Fail:[False]]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    /// <summary>
    /// Verifies that the mediator correctly processes queries with caching explicitly disabled.
    /// </summary>
    /// <remarks>This test ensures that the mediator processes a query without utilizing caching when caching
    /// is disabled for the specific request type. It validates the query's successful execution, the correctness of the
    /// output, and the expected logging behavior.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task BehaviorShouldCorrectlyProcessQueriesWithDisabledCaching()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        // Add Mediator services, including the CachingBehavior
        _ = services.AddMediator(
            typeof(SimpleQueryWithDisabledCaching.Handler).Assembly,
            typeof(CachingBehavior<,>).Assembly);
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Add memory cache for distributed caching
        _ = services.AddDistributedMemoryCache();
        // Set up ILogger mock
        var loggerMock = new Mock<ILogger<SimpleQueryWithDisabledCaching.Query>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Add the JSON configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        _ = services.AddSingleton<IConfiguration>(builder.Build());
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple query to process
        var query = new SimpleQueryWithDisabledCaching.Query("Ping...");

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        // Process the command using the mediator
        var result = await mediator.SendAsync(query);

        //--------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        // Verify that the query was processed successfully
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, "The query should have been processed successfully.");
        // Verify that the output is correct
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.Reply);
        Assert.Equal("Processed message: [Ping...].", result.Value.Reply);
        // Verify that the logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Query received: [Ping...], expected to fail: [False]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Query executed successfully."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        // Verify that the behavior logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "Caching has been disabled for request [Boutquin.Mediator.Tests.Common.SimpleQueryWithDisabledCaching+Query]. Proceeding without caching."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    /// <summary>
    /// Verifies that the mediator correctly processes queries with per-user caching behavior applied, ensuring that the
    /// query is processed only once per user and that the expected caching and logging behaviors are executed.
    /// </summary>
    /// <remarks>This test ensures that the caching behavior is applied correctly for queries that are scoped
    /// per user. It validates that: <list type="bullet"> <item><description>The query is processed successfully and
    /// returns the expected result.</description></item> <item><description>The caching mechanism retrieves and stores
    /// the response using the correct cache key format.</description></item> <item><description>Logging is performed as
    /// expected, including debug and informational messages.</description></item> <item><description>Non-caching
    /// behavior is not triggered for queries that should be cached.</description></item> </list></remarks>
    /// <returns></returns>
    [Fact]
    public async Task BehaviorShouldCorrectlyProcessPerUserQueriesOnce()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        // Add Mediator services, including the CachingBehavior
        _ = services.AddMediator(
            typeof(SimpleQueryWithPerUserCaching.Handler).Assembly,
            typeof(CachingBehavior<,>).Assembly);
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = currentUserServiceMock.Setup(x => x.UserId).Returns(UserId1);
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Add memory cache for distributed caching
        _ = services.AddDistributedMemoryCache();
        // Set up ILogger mock
        var loggerMock = new Mock<ILogger<SimpleQueryWithPerUserCaching.Query>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Add the JSON configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        _ = services.AddSingleton<IConfiguration>(builder.Build());
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple query to process
        var query = new SimpleQueryWithPerUserCaching.Query("Ping...");

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        // Process the command using the mediator
        var result = await mediator.SendAsync(query);

        //--------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        // Verify that the query was processed successfully
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, "The query should have been processed successfully.");
        // Verify that the output is correct
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.Reply);
        Assert.Equal("Processed message: [Ping...].", result.Value.Reply);
        // Verify that the logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Query received: [Ping...], expected to fail: [False]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Query executed successfully."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        // Verify that the behavior logger was NOT called with the non-caching message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "Caching is not applied for request [Boutquin.Mediator.Tests.Common.SimpleQueryWithPerUserCaching+Query]. Proceeding without caching."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Never);
        // Verify that the behavior logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    $"Attempting to retrieve cached response for request [Boutquin.Mediator.Tests.Common.SimpleQueryWithPerUserCaching+Query] with cache key [{UserId1}|Boutquin.Mediator.Tests.Common.SimpleQueryWithPerUserCaching+Query|$Message:[Ping...]|$Fail:[False]]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    $"Caching response for request [Boutquin.Mediator.Tests.Common.SimpleQueryWithPerUserCaching+Query] with cache key [{UserId1}|Boutquin.Mediator.Tests.Common.SimpleQueryWithPerUserCaching+Query|$Message:[Ping...]|$Fail:[False]]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    /// <summary>
    /// Verifies that the caching behavior correctly processes cache reset queries and ensures the expected logging and
    /// caching operations occur.
    /// </summary>
    /// <remarks>This test method validates the behavior of the caching mechanism when handling a query that
    /// resets the cache.  It ensures that the query is processed successfully, the cache is updated as expected, and
    /// the appropriate log messages are generated. The test uses a mediator to send the query and verifies the results
    /// using assertions and mock verifications.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task BehaviorShouldCorrectlyProcessCacheResetQueriesOnce()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        // Add Mediator services, including the CachingBehavior
        _ = services.AddMediator(
            typeof(SimpleQueryWithCaching.Handler).Assembly,
            typeof(CachingBehavior<,>).Assembly);
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Add memory cache for distributed caching
        _ = services.AddDistributedMemoryCache();
        // Set up ILogger mock
        var loggerMock = new Mock<ILogger<SimpleQueryWithResetCaching.Query>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Add the JSON configuration
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        _ = services.AddSingleton<IConfiguration>(builder.Build());
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple query to process
        var query = new SimpleQueryWithResetCaching.Query("Ping...");

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        // Process the command using the mediator
        var result = await mediator.SendAsync(query);

        //--------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        // Verify that the query was processed successfully
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, "The query should have been processed successfully.");
        // Verify that the output is correct
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.Reply);
        Assert.Equal("Processed message: [Ping...].", result.Value.Reply);
        // Verify that the logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Query received: [Ping...], expected to fail: [False]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Query executed successfully."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        // Verify that the behavior logger was NOT called with the non-caching message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "Caching is not applied for request [Boutquin.Mediator.Tests.Common.SimpleQueryWithResetCaching+Query]. Proceeding without caching."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Never);
        // Verify that the behavior logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "Removing cached response for request [Boutquin.Mediator.Tests.Common.SimpleQueryWithResetCaching+Query] with cache key [|Boutquin.Mediator.Tests.Common.SimpleQueryWithResetCaching+Query|$Message:[Ping...]|$Fail:[False]]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "Caching response for request [Boutquin.Mediator.Tests.Common.SimpleQueryWithResetCaching+Query] with cache key [|Boutquin.Mediator.Tests.Common.SimpleQueryWithResetCaching+Query|$Message:[Ping...]|$Fail:[False]]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    // -----------------------------------------------------------------------
    // S9-08 / S2-01: corrupt JSON in cache → fall through to handler
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that when the distributed cache returns corrupt JSON, the caching behavior
    /// catches the <see cref="System.Text.Json.JsonException"/>, logs a warning, and falls
    /// through to the actual request handler rather than returning an error.
    /// </summary>
    [Fact]
    public async Task CachingBehavior_CorruptJsonInCache_FallsThroughToHandler()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        const string CorruptJson = "{not valid json";
        var corruptBytes = System.Text.Encoding.UTF8.GetBytes(CorruptJson);

        var cacheMock = new Mock<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        // Return corrupt JSON on first call (cache "hit" with corrupt data)
        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(corruptBytes);

        // SetAsync is called after the handler populates the response
        cacheMock.Setup(c => c.SetAsync(
                     It.IsAny<string>(),
                     It.IsAny<byte[]>(),
                     It.IsAny<Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions>(),
                     It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger<SimpleQueryWithCaching.Query>>();
        var userServiceMock = new Mock<ICurrentUserService>();
        _ = userServiceMock.Setup(u => u.UserId).Returns(UserId1);

        var services = new ServiceCollection();
        _ = services.AddSingleton(cacheMock.Object);
        _ = services.AddSingleton(userServiceMock.Object);
        _ = services.AddSingleton(loggerMock.Object);
        _ = services.AddTransient<IRequestHandler<SimpleQueryWithCaching.Query, Result<SimpleQueryWithCaching.Output>>,
                                  SimpleQueryWithCaching.Handler>();
        _ = services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
        _ = services.AddSingleton<IMediator, Helpers.Mediator>();

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        _ = services.AddSingleton<IConfiguration>(builder.Build());

        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();
        var query = new SimpleQueryWithCaching.Query("CorruptCacheTest");

        //---------------------------------------------------------
        // Act — corrupt cache → should fall through to handler
        //---------------------------------------------------------
        var result = await mediator.SendAsync(query).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(result.IsSuccess,
            "When the cache contains corrupt JSON the behavior must fall through to the handler and return success.");
        Assert.NotNull(result.Value);
        Assert.Contains("CorruptCacheTest", result.Value.Reply);

        // Handler was invoked (not the cached value)
        cacheMock.Verify(
            c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "SetAsync must be called once to cache the fresh handler response.");
    }

    // -----------------------------------------------------------------------
    // CACHE-POISON-NONGENERIC-001 / CACHE-CATCH-NARROW-001:
    // a [CachingPolicy] request returning the NON-GENERIC Result must round-trip
    // through the cache — miss populates, HIT deserializes back without crashing.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that a <see cref="CachingPolicyAttribute"/>-decorated request whose response is the
    /// non-generic <see cref="Result"/> can be cached on the first miss and read back on the second
    /// hit without throwing. Before the fix, the second send deserialized via
    /// <c>JsonSerializer.Deserialize&lt;Result&gt;</c>, which threw <see cref="NotSupportedException"/>
    /// (escaping <c>catch (JsonException)</c>) and crashed the pipeline.
    /// </summary>
    [Fact]
    public async Task CachingBehavior_NonGenericResultRequest_CacheHitReturnsResultWithoutThrowing()
    {
        //---------------------------------------------------------
        // Arrange — single shared in-memory cache so the 2nd call hits
        //---------------------------------------------------------
        var services = new ServiceCollection();
        _ = services.AddMediator(
            typeof(SimpleCommandWithCaching.Handler).Assembly,
            typeof(CachingBehavior<,>).Assembly);

        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        _ = services.AddDistributedMemoryCache();
        _ = services.AddSingleton(Mock.Of<ILogger<SimpleCommandWithCaching.Command>>());

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        _ = services.AddSingleton<IConfiguration>(builder.Build());

        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();
        var command = new SimpleCommandWithCaching.Command("RoundTrip");

        //---------------------------------------------------------
        // Act — first send misses (populates cache); second send HITS (deserializes)
        //---------------------------------------------------------
        var firstResult = await mediator.SendAsync(command).ConfigureAwait(true);
        var cachedResult = await mediator.SendAsync(command).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert — the cache hit deserialized the non-generic Result without crashing
        //---------------------------------------------------------
        Assert.NotNull(firstResult);
        Assert.True(firstResult.IsSuccess, "First (miss) send must succeed.");
        Assert.NotNull(cachedResult);
        Assert.True(cachedResult.IsSuccess,
            "Cache HIT must deserialize the cached non-generic Result and report IsSuccess without throwing.");
    }

    // -----------------------------------------------------------------------
    // CACHE-CATCH-NARROW-001 / F-L2-01:
    // a cached literal "null" for a generic Result<T> request must NOT crash —
    // the non-JsonException must be caught and the request must fall through.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that when the cache holds the JSON literal <c>"null"</c> for a generic
    /// <see cref="Result{T}"/> response, the behavior does not crash. Before the fix, the
    /// <c>?? (TResponse)Result.Failure(...)</c> fallback upcast a non-generic <see cref="Result"/>
    /// to <c>Result&lt;T&gt;</c> → <see cref="InvalidCastException"/>, which escaped
    /// <c>catch (JsonException)</c> and crashed the pipeline. After the fix the deserializer returns
    /// <see langword="null"/>, and the <c>?? CreateFailureResponse(...)</c> fallback builds a
    /// type-safe <c>Result&lt;Output&gt;</c> failure (no upcast, no exception). The send completes
    /// and returns a typed failure result instead of throwing.
    /// </summary>
    [Fact]
    public async Task CachingBehavior_CachedNullLiteral_ReturnsTypeSafeFailureWithoutThrowing()
    {
        //---------------------------------------------------------
        // Arrange — cache returns the JSON literal "null" on read
        //---------------------------------------------------------
        var nullBytes = System.Text.Encoding.UTF8.GetBytes("null");

        var cacheMock = new Mock<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        _ = cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(nullBytes);
        _ = cacheMock.Setup(c => c.SetAsync(
                     It.IsAny<string>(),
                     It.IsAny<byte[]>(),
                     It.IsAny<Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions>(),
                     It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger<SimpleQueryWithCaching.Query>>();
        var userServiceMock = new Mock<ICurrentUserService>();
        _ = userServiceMock.Setup(u => u.UserId).Returns(UserId1);

        var services = new ServiceCollection();
        _ = services.AddSingleton(cacheMock.Object);
        _ = services.AddSingleton(userServiceMock.Object);
        _ = services.AddSingleton(loggerMock.Object);
        _ = services.AddTransient<IRequestHandler<SimpleQueryWithCaching.Query, Result<SimpleQueryWithCaching.Output>>,
                                  SimpleQueryWithCaching.Handler>();
        _ = services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
        _ = services.AddSingleton<IMediator, Helpers.Mediator>();

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        _ = services.AddSingleton<IConfiguration>(builder.Build());

        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();
        var query = new SimpleQueryWithCaching.Query("NullLiteralTest");

        //---------------------------------------------------------
        // Act — cached "null" must NOT throw InvalidCastException; it returns a typed failure
        //---------------------------------------------------------
        var result = await mediator.SendAsync(query).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert — no crash; a type-safe Result<Output> failure is returned
        //---------------------------------------------------------
        Assert.NotNull(result);
        Assert.IsType<Result<SimpleQueryWithCaching.Output>>(result, exactMatch: false);
        Assert.True(result.IsFailure,
            "A cached \"null\" literal must yield a type-safe failure rather than throwing InvalidCastException.");
    }

    // -----------------------------------------------------------------------
    // CACHE-NULLGUARD-001: null request must throw ArgumentNullException
    // (matching RetryBehavior / AuthorizationBehavior), not NullReferenceException.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <c>CachingBehavior.HandleAsync</c> throws <see cref="ArgumentNullException"/>
    /// when the request is <see langword="null"/>, matching the sibling behaviors' contract.
    /// Before the fix, the missing guard let the method dereference <c>request.GetType()</c> and
    /// throw <see cref="NullReferenceException"/> instead.
    /// </summary>
    [Fact]
    public async Task CachingBehavior_NullRequest_ThrowsArgumentNullException()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var cacheMock = new Mock<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        var userServiceMock = new Mock<ICurrentUserService>();
        var loggerMock = new Mock<ILogger<SimpleCommandWithCaching.Command>>();
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        var configuration = builder.Build();

        var behavior = new CachingBehavior<SimpleCommandWithCaching.Command, Result>(
            cacheMock.Object, userServiceMock.Object, configuration, loggerMock.Object);

        //---------------------------------------------------------
        // Act & Assert — null request
        //---------------------------------------------------------
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await behavior.HandleAsync(
                request: null!,
                next: () => ValueTask.FromResult(Result.Success()),
                cancellationToken: default).ConfigureAwait(true));

        Assert.Equal("request", ex.ParamName);
    }

    // -----------------------------------------------------------------------
    // F-caching-1: corrupt failure entry {"IsSuccess":false} (no Error property)
    // causes NonGenericResultJsonConverter.Read to call Result.Failure(Error.None),
    // which throws InvalidOperationException("A failed result must have an error.").
    // That exception MUST be caught by the deserialize catch block and degrade to
    // a fresh handler call, not escape and crash the pipeline.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that when the distributed cache holds a corrupt failure entry
    /// <c>{"IsSuccess":false}</c> (Error property absent) for a non-generic
    /// <see cref="Result"/> request, the <c>InvalidOperationException</c> thrown by
    /// <see cref="Result.Failure(Error)"/> when given <see cref="Error.None"/> is
    /// caught by the deserialize guard and the behavior degrades to a fresh handler
    /// call rather than crashing the pipeline.
    /// </summary>
    [Fact]
    public async Task CachingBehavior_CorruptFailureEntryNoError_FallsThroughToHandler()
    {
        //---------------------------------------------------------
        // Arrange — cache returns {"IsSuccess":false} (no Error) on every read.
        // The converter will call Result.Failure(Error.None) which throws
        // InvalidOperationException; the current catch filter does NOT include it,
        // so without the fix this exception escapes and the test throws.
        //---------------------------------------------------------
        const string CorruptFailureJson = """{"IsSuccess":false}""";
        var poisonBytes = System.Text.Encoding.UTF8.GetBytes(CorruptFailureJson);

        var cacheMock = new Mock<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .ReturnsAsync(poisonBytes);
        cacheMock.Setup(c => c.SetAsync(
                     It.IsAny<string>(),
                     It.IsAny<byte[]>(),
                     It.IsAny<Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions>(),
                     It.IsAny<CancellationToken>()))
                 .Returns(Task.CompletedTask);

        var loggerMock = new Mock<ILogger<SimpleCommandWithCaching.Command>>();
        var userServiceMock = new Mock<ICurrentUserService>();

        var services = new ServiceCollection();
        _ = services.AddSingleton(cacheMock.Object);
        _ = services.AddSingleton(userServiceMock.Object);
        _ = services.AddSingleton(loggerMock.Object);
        _ = services.AddTransient<IRequestHandler<SimpleCommandWithCaching.Command, Result>,
                                  SimpleCommandWithCaching.Handler>();
        _ = services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));
        _ = services.AddSingleton<IMediator, Helpers.Mediator>();

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        _ = services.AddSingleton<IConfiguration>(builder.Build());

        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();
        var command = new SimpleCommandWithCaching.Command("PoisonTest");

        //---------------------------------------------------------
        // Act — poisoned cache must degrade to the handler (no throw)
        //---------------------------------------------------------
        var result = await mediator.SendAsync(command).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert — handler ran; InvalidOperationException was caught
        //---------------------------------------------------------
        Assert.True(result.IsSuccess,
            "A corrupt failure entry {\"IsSuccess\":false} must cause the behavior to fall through " +
            "to the handler, not surface an InvalidOperationException.");

        cacheMock.Verify(
            c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "SetAsync must be called once when the handler runs after the corrupt cache hit.");
    }

    /// <summary>
    /// Verifies that a failure <see cref="Result"/> is returned to the caller but never cached, while a later
    /// success is cached. The second call must reach the handler again (the failure was not stored); the third
    /// call must be served from the cache (the success was stored).
    /// </summary>
    [Fact]
    public async Task CachingBehavior_FailureResult_IsNotCached_SuccessIs()
    {
        //---------------------------------------------------------
        // Arrange — real in-memory cache; handler fails once, then succeeds
        //---------------------------------------------------------
        var cache = new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(
                new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions()));
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        var behavior = new CachingBehavior<SimpleCommandWithCaching.Command, Result>(
            cache, Mock.Of<ICurrentUserService>(), builder.Build(),
            Mock.Of<ILogger<SimpleCommandWithCaching.Command>>());

        var request = new SimpleCommandWithCaching.Command("FailThenSucceed");
        var handlerCalls = 0;
        ValueTask<Result> Next()
        {
            handlerCalls++;
            return ValueTask.FromResult(handlerCalls == 1
                ? Result.Failure(Error.InternalServerError("Test.Transient", "Transient failure."))
                : Result.Success());
        }

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var first = await behavior.HandleAsync(request, Next).ConfigureAwait(true);
        var second = await behavior.HandleAsync(request, Next).ConfigureAwait(true);
        var third = await behavior.HandleAsync(request, Next).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(first.IsFailure, "The handler's failure must reach the caller.");
        Assert.True(second.IsSuccess, "The failure must not be cached; the handler runs again and succeeds.");
        Assert.True(third.IsSuccess, "The success must be served from the cache.");
        Assert.Equal(2, handlerCalls);
    }

    // -----------------------------------------------------------------------
    // CACHE-NULLGUARD-001: null next delegate must throw ArgumentNullException.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that <c>CachingBehavior.HandleAsync</c> throws <see cref="ArgumentNullException"/>
    /// when the <c>next</c> delegate is <see langword="null"/>, matching the sibling behaviors.
    /// </summary>
    [Fact]
    public async Task CachingBehavior_NullNext_ThrowsArgumentNullException()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var cacheMock = new Mock<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
        var userServiceMock = new Mock<ICurrentUserService>();
        var loggerMock = new Mock<ILogger<SimpleCommandWithCaching.Command>>();
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        var configuration = builder.Build();

        var behavior = new CachingBehavior<SimpleCommandWithCaching.Command, Result>(
            cacheMock.Object, userServiceMock.Object, configuration, loggerMock.Object);

        //---------------------------------------------------------
        // Act & Assert — null next
        //---------------------------------------------------------
        var ex = await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await behavior.HandleAsync(
                request: new SimpleCommandWithCaching.Command("x"),
                next: null!,
                cancellationToken: default).ConfigureAwait(true));

        Assert.Equal("next", ex.ParamName);
    }
}
