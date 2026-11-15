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
// ReSharper disable RedundantAssignment

// Boutquin.Mediator
using Abstractions;
using Boutquin.Abstractions.Auth;
using Boutquin.Domain.Abstractions;
using Boutquin.Reliability;
// Boutquin.Mediator.Tests
using Common;
// Boutquin.Mediator
using Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Reliability;

public class RetryBehaviorTests
{
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
                typeof(RetryBehavior<,>).Assembly);
        // Add memory cache for distributed caching
        _ = services.AddDistributedMemoryCache();
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Add the WaitWithRetry service
        _ = services.AddTransient<IWaitWithRetry, WaitWithRetry>();
        // Set up ILogger mocks
        var loggerMock = new Mock<ILogger<SimpleCommand.Command>>();
        _ = services.AddSingleton(loggerMock.Object);
        var loggerRetryMock = new Mock<ILogger<WaitWithRetry>>();
        _ = services.AddSingleton(loggerRetryMock.Object);
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
        var result = await mediator.SendAsync<SimpleCommand.Command, Result>(command);

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
                    "RetryBehavior is disabled for request [Boutquin.Mediator.Tests.Common.SimpleCommand+Command]. Proceeding without retries."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task BehaviorShouldCorrectlyProcessQueriesWithDisabledRetry()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        // Add Mediator services, including the RetryBehavior
        _ = services.AddMediator(
            typeof(SimpleQueryWithDisabledRetry.Handler).Assembly,
            typeof(RetryBehavior<,>).Assembly);
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Add the WaitWithRetry service
        _ = services.AddTransient<IWaitWithRetry, WaitWithRetry>();
        // Set up ILogger mock
        var loggerMock = new Mock<ILogger<SimpleQueryWithDisabledRetry.Query>>();
        _ = services.AddSingleton(loggerMock.Object);
        var loggerRetryMock = new Mock<ILogger<WaitWithRetry>>();
        _ = services.AddSingleton(loggerRetryMock.Object);
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
        var query = new SimpleQueryWithDisabledRetry.Query("Ping...");

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        // Process the command using the mediator
        var result = await mediator.SendAsync<SimpleQueryWithDisabledRetry.Query, Result<SimpleQueryWithDisabledRetry.Output>>(query);

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
                    "RetryBehavior is disabled for request [Boutquin.Mediator.Tests.Common.SimpleQueryWithDisabledRetry+Query]. Proceeding without retries."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task BehaviorShouldRetryAndSucceedOnSecondAttempt()
    {
        // Reset static retry counter
        SimpleQueryWithRetry.Handler.Reset();
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var services = new ServiceCollection();
        _ = services.AddMediator(
            typeof(SimpleQueryWithRetry.Handler).Assembly,
            typeof(RetryBehavior<,>).Assembly);
        var loggerMock = new Mock<ILogger<SimpleQueryWithRetry.Query>>();
        _ = services.AddSingleton(loggerMock.Object);
        var loggerRetryMock = new Mock<ILogger<WaitWithRetry>>();
        _ = services.AddSingleton(loggerRetryMock.Object);
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        var callCount = 0;
        var waitWithRetryMock = new Mock<IWaitWithRetry>();
        waitWithRetryMock.Setup(w => w.WaitWithRetryAsync(
            It.IsAny<Func<Task<Result<SimpleQueryWithRetry.Output>>>>(),
            It.IsAny<IWaitSettings>(),
            It.IsAny<CancellationToken>()))
            .Returns(async (Func<Task<Result<SimpleQueryWithRetry.Output>>> func, IWaitSettings settings, CancellationToken _) =>
            {
                Result<SimpleQueryWithRetry.Output> result = null!;
                for (var i = 0; i < settings.NumberOfRetries; i++)
                {
                    callCount++;
                    result = await func().ConfigureAwait(false);
                    if (result.IsSuccess)
                    {
                        break;
                    }
                }
                return result!;
            });
        _ = services.AddSingleton(waitWithRetryMock.Object);
        var inMemorySettings = new Dictionary<string, string>
        {
            {"MediatorSettings:Boutquin.Mediator.Tests.Common.SimpleQueryWithRetry+Query:RetrySettings:Enabled", "true"},
            {"MediatorSettings:Boutquin.Mediator.Tests.Common.SimpleQueryWithRetry+Query:RetrySettings:NumberOfRetries", "2"}
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();
        _ = services.AddSingleton<IConfiguration>(config);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var query = new SimpleQueryWithRetry.Query("Ping...");

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await mediator.SendAsync<SimpleQueryWithRetry.Query, Result<SimpleQueryWithRetry.Output>>(query);

        //--------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, "The query should have been processed successfully after retry.");
        Assert.NotNull(result.Value);
        Assert.Equal("Processed message: [Ping...].", result.Value.Reply);
        Assert.Equal(2, callCount); // Should have retried once
    }

    [Fact]
    public async Task BehaviorShouldRetryAndFailAfterAllAttempts()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var services = new ServiceCollection();
        _ = services.AddMediator(
            typeof(SimpleQueryWithRetry.Handler).Assembly,
            typeof(RetryBehavior<,>).Assembly);
        var loggerMock = new Mock<ILogger<SimpleQueryWithRetry.Query>>();
        _ = services.AddSingleton(loggerMock.Object);
        var loggerRetryMock = new Mock<ILogger<WaitWithRetry>>();
        _ = services.AddSingleton(loggerRetryMock.Object);
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        var callCount = 0;
        var waitWithRetryMock = new Mock<IWaitWithRetry>();
        waitWithRetryMock.Setup(w => w.WaitWithRetryAsync(
            It.IsAny<Func<Task<Result<SimpleQueryWithRetry.Output>>>>(),
            It.IsAny<IWaitSettings>(),
            It.IsAny<CancellationToken>()))
            .Returns((Func<Task<Result<SimpleQueryWithRetry.Output>>> _, IWaitSettings settings, CancellationToken _) =>
            {
                Result<SimpleQueryWithRetry.Output> result = null!;
                for (var i = 0; i < settings.NumberOfRetries; i++)
                {
                    callCount++;
                    result = Result.Failure<SimpleQueryWithRetry.Output>(Error.BadRequest("BadRequest.QueryFailed", "fail"));
                }
                return Task.FromResult(result!);
            });
        _ = services.AddSingleton(waitWithRetryMock.Object);
        var inMemorySettings = new Dictionary<string, string>
        {
            {"MediatorSettings:Boutquin.Mediator.Tests.Common.SimpleQueryWithRetry+Query:RetrySettings:Enabled", "true"},
            {"MediatorSettings:Boutquin.Mediator.Tests.Common.SimpleQueryWithRetry+Query:RetrySettings:NumberOfRetries", "3"}
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();
        _ = services.AddSingleton<IConfiguration>(config);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var query = new SimpleQueryWithRetry.Query("Ping...");

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await mediator.SendAsync<SimpleQueryWithRetry.Query, Result<SimpleQueryWithRetry.Output>>(query);

        //--------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.NotNull(result);
        Assert.False(result.IsSuccess, "The query should have failed after all retries.");
        Assert.NotNull(result.Error);
        Assert.Equal("BadRequest.QueryFailed", result.Error.Code);
        Assert.Equal(3, callCount); // Should have retried 3 times
    }

    [Fact]
    public async Task BehaviorShouldRespectCancellationToken()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var services = new ServiceCollection();
        _ = services.AddMediator(
            typeof(SimpleQueryWithRetry.Handler).Assembly,
            typeof(RetryBehavior<,>).Assembly);
        var loggerMock = new Mock<ILogger<SimpleQueryWithRetry.Query>>();
        _ = services.AddSingleton(loggerMock.Object);
        var loggerRetryMock = new Mock<ILogger<WaitWithRetry>>();
        _ = services.AddSingleton(loggerRetryMock.Object);
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        var waitWithRetryMock = new Mock<IWaitWithRetry>();
        waitWithRetryMock.Setup(w => w.WaitWithRetryAsync(
            It.IsAny<Func<Task<Result<SimpleQueryWithRetry.Output>>>>(),
            It.IsAny<IWaitSettings>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        _ = services.AddSingleton(waitWithRetryMock.Object);
        var inMemorySettings = new Dictionary<string, string>
        {
            {"MediatorSettings:Boutquin.Mediator.Tests.Common.SimpleQueryWithRetry+Query:RetrySettings:Enabled", "true"},
            {"MediatorSettings:Boutquin.Mediator.Tests.Common.SimpleQueryWithRetry+Query:RetrySettings:NumberOfRetries", "2"}
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();
        _ = services.AddSingleton<IConfiguration>(config);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var query = new SimpleQueryWithRetry.Query("Ping...");
        var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        //--------------------------------------------------------
        // Act & Assert
        //---------------------------------------------------------
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await mediator.SendAsync<SimpleQueryWithRetry.Query, Result<SimpleQueryWithRetry.Output>>(query, cts.Token).ConfigureAwait(true));
    }

    [Fact]
    public async Task BehaviorShouldRetryOnExceptionAndSucceed()
    {
        // Reset static retry counter
        SimpleQueryWithRetry.Handler.Reset();
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var services = new ServiceCollection();
        _ = services.AddMediator(
            typeof(SimpleQueryWithRetry.Handler).Assembly,
            typeof(RetryBehavior<,>).Assembly);
        var loggerMock = new Mock<ILogger<SimpleQueryWithRetry.Query>>();
        _ = services.AddSingleton(loggerMock.Object);
        var loggerRetryMock = new Mock<ILogger<WaitWithRetry>>();
        _ = services.AddSingleton(loggerRetryMock.Object);
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        var callCount = 0;
        var waitWithRetryMock = new Mock<IWaitWithRetry>();
        waitWithRetryMock.Setup(w => w.WaitWithRetryAsync(
            It.IsAny<Func<Task<Result<SimpleQueryWithRetry.Output>>>>(),
            It.IsAny<IWaitSettings>(),
            It.IsAny<CancellationToken>()))
            .Returns(async (Func<Task<Result<SimpleQueryWithRetry.Output>>> func, IWaitSettings settings, CancellationToken _) =>
            {
                Result<SimpleQueryWithRetry.Output> result = null!;
                for (var i = 0; i < settings.NumberOfRetries; i++)
                {
                    callCount++;
                    try
                    {
                        result = await func().ConfigureAwait(false);
                        if (result.IsSuccess)
                        {
                            break;
                        }
                    }
                    catch (Exception)
                    {
                        if (i == settings.NumberOfRetries - 1)
                        {
                            throw;
                        }
                    }
                }
                return result!;
            });
        _ = services.AddSingleton(waitWithRetryMock.Object);
        var inMemorySettings = new Dictionary<string, string>
        {
            {"MediatorSettings:Boutquin.Mediator.Tests.Common.SimpleQueryWithRetry+Query:RetrySettings:Enabled", "true"},
            {"MediatorSettings:Boutquin.Mediator.Tests.Common.SimpleQueryWithRetry+Query:RetrySettings:NumberOfRetries", "2"}
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemorySettings!).Build();
        _ = services.AddSingleton<IConfiguration>(config);
        var serviceProvider = services.BuildServiceProvider();
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        var query = new SimpleQueryWithRetry.Query("EXCEPTION");

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await mediator.SendAsync<SimpleQueryWithRetry.Query, Result<SimpleQueryWithRetry.Output>>(query);

        //--------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.NotNull(result);
        Assert.True(result.IsSuccess, "The query should have been processed successfully after retry on exception.");
        Assert.NotNull(result.Value);
        Assert.Equal("Processed message: [EXCEPTION].", result.Value.Reply);
        Assert.Equal(2, callCount); // Should have retried once after exception
    }
}
