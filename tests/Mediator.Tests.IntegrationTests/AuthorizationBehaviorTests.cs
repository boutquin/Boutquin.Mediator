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
using Authorization;
using Boutquin.Abstractions.Auth;
using Boutquin.Domain.Abstractions;
// Boutquin.Mediator.Tests
using Common;
using Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

public sealed class AuthorizationBehaviorTests
{
    /// <summary>
    /// Represents a constant user identifier.
    /// </summary>
    /// <remarks>This constant is used to uniquely identify a specific user within the system.</remarks>
    private const string UserId1 = "2E9076B4-5737-44F6-AC20-307FE3AA4FBD";

    /// <summary>
    /// Verifies that the mediator correctly processes plain commands, including logging and authorization behavior.
    /// </summary>
    /// <remarks>This test ensures that a simple command is processed successfully by the mediator, with
    /// appropriate logging  and authorization checks performed. It validates that the command handler and behavior
    /// loggers are invoked  with the expected messages and that the result indicates successful processing.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task BehaviorShouldCorrectlyProcessPlainCommands()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        _ = services.AddMediator(
                typeof(SimpleCommand.Handler).Assembly,
                typeof(AuthorizationBehavior<,>).Assembly);
        // Set up ILogger mocks
        var loggerMock = new Mock<ILogger<SimpleCommand.Command>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Set up a mock for the authorization service
        var authorizationServiceMock = new Mock<IAuthorizationService>();
        _ = services.AddSingleton(authorizationServiceMock.Object);
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple command to process
        var command = new SimpleCommand.Command("Hello, World!");

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        // Process the command using the mediator with type inference
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
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Authorizing request [Boutquin.Mediator.Tests.Common.SimpleCommand+Command]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "No authorization attributes found for request [Boutquin.Mediator.Tests.Common.SimpleCommand+Command]. Proceeding without authorization."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    /// <summary>
    /// Verifies that the mediator correctly processes plain queries, including logging, authorization, and result
    /// validation.
    /// </summary>
    /// <remarks>This test ensures that a simple query is processed successfully by the mediator, with proper
    /// logging and authorization behavior. It validates the query's result, checks the correctness of the output, and
    /// verifies that the expected log messages are generated.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task BehaviorShouldCorrectlyProcessPlainQueries()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        _ = services.AddMediator(
            typeof(SimpleQuery.Handler).Assembly,
            typeof(AuthorizationBehavior<,>).Assembly);
        // Set up ILogger mocks
        var loggerMock = new Mock<ILogger<SimpleQuery.Query>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Set up a mock for the authorization service
        var authorizationServiceMock = new Mock<IAuthorizationService>();
        _ = services.AddSingleton(authorizationServiceMock.Object);
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple query to process
        var query = new SimpleQuery.Query("Ping...");

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        // Process the query using the mediator with type inference
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
        // Verify that the handler logger was called with the expected message
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
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Authorizing request [Boutquin.Mediator.Tests.Common.SimpleQuery+Query]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v != null && v.ToString() ==
                    "No authorization attributes found for request [Boutquin.Mediator.Tests.Common.SimpleQuery+Query]. Proceeding without authorization."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    /// <summary>
    /// Verifies that role-based commands are rejected when the user is unauthenticated.
    /// </summary>
    /// <remarks>This test ensures that the authorization behavior correctly identifies unauthenticated users
    /// and prevents the execution of commands requiring role-based authorization. It validates that the appropriate
    /// error details are returned and that the expected log messages are generated.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task BehaviorShouldRejectUnauthenticatedRoleBasedCommands()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        _ = services.AddMediator(
            typeof(SimpleCommandWithRoleAuthorization.Handler).Assembly,
            typeof(AuthorizationBehavior<,>).Assembly);
        // Set up ILogger mock
        var loggerMock = new Mock<ILogger<SimpleCommandWithRoleAuthorization.Command>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Set up a mock for the authorization service
        var authorizationServiceMock = new Mock<IAuthorizationService>();
        _ = services.AddSingleton(authorizationServiceMock.Object);
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple command to process
        var command = new SimpleCommandWithRoleAuthorization.Command("Hello, World!", Fail: false);

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        // Process the command using the mediator
        var result = await mediator.SendAsync(command);

        //--------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        // Verify that the command was processed with an error
        Assert.NotNull(result);
        Assert.False(result.IsSuccess, "The command should not have been processed successfully.");
        Assert.NotNull(result.Error);
        // Verify that the error details are correct
        Assert.Equal("Error.Unauthorized", result.Error.Code);
        Assert.Equal(ErrorType.Unauthorized, result.Error.ErrorType);
        Assert.Equal("The request requires user authentication.", result.Error.Description);
        // Verify that the behavior logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Authorizing request [Boutquin.Mediator.Tests.Common.SimpleCommandWithRoleAuthorization+Command]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Error, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "User is not authenticated for request [Boutquin.Mediator.Tests.Common.SimpleCommandWithRoleAuthorization+Command]. Authorization failed."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    [Fact]
    public async Task BehaviorShouldCorrectlyProcessAuthorizedRoleBasedCommands()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        _ = services.AddMediator(
                typeof(SimpleCommandWithRoleAuthorization.Handler).Assembly,
                typeof(AuthorizationBehavior<,>).Assembly);
        // Set up ILogger mocks
        var loggerMock = new Mock<ILogger<SimpleCommandWithRoleAuthorization.Command>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = currentUserServiceMock.Setup(x => x.UserId).Returns(UserId1);
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Set up a mock for the authorization service
        var authorizationServiceMock = new Mock<IAuthorizationService>();
        _ = authorizationServiceMock.Setup(x => x.IsInAnyRoleAsync(
            It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));
        _ = services.AddSingleton(authorizationServiceMock.Object);
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple command to process
        var command = new SimpleCommandWithRoleAuthorization.Command("Hello, World!");

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
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Authorizing request [Boutquin.Mediator.Tests.Common.SimpleCommandWithRoleAuthorization+Command]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    /// <summary>
    /// Verifies that commands requiring role-based authorization are rejected when the user lacks the necessary roles.
    /// </summary>
    /// <remarks>This test ensures that the authorization behavior correctly identifies and rejects commands 
    /// when the current user does not have the required roles. It validates that the appropriate error  details are
    /// returned and that the expected log messages are generated.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task BehaviorShouldRejectForbiddenRoleBasedCommands()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        _ = services.AddMediator(
            typeof(SimpleCommandWithRoleAuthorization.Handler).Assembly,
            typeof(AuthorizationBehavior<,>).Assembly);
        // Set up ILogger mock
        var loggerMock = new Mock<ILogger<SimpleCommandWithRoleAuthorization.Command>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = currentUserServiceMock.Setup(x => x.UserId).Returns(UserId1);
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Set up a mock for the authorization service
        var authorizationServiceMock = new Mock<IAuthorizationService>();
        _ = authorizationServiceMock.Setup(x => x.IsInAnyRoleAsync(
                It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(false));
        _ = services.AddSingleton(authorizationServiceMock.Object);
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple command to process
        var command = new SimpleCommandWithRoleAuthorization.Command("Hello, World!", Fail: false);

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        // Process the command using the mediator
        var result = await mediator.SendAsync(command);

        //--------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        // Verify that the command was processed with an error
        Assert.NotNull(result);
        Assert.False(result.IsSuccess, "The command should not have been processed successfully.");
        Assert.NotNull(result.Error);
        // Verify that the error details are correct
        Assert.Equal("Error.Forbidden.Role", result.Error.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.ErrorType);
        Assert.Equal($"User [{UserId1}] is not authorized for required roles: [ADMIN, USER].", result.Error.Description);
        // Verify that the behavior logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Authorizing request [Boutquin.Mediator.Tests.Common.SimpleCommandWithRoleAuthorization+Command]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Error, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == $"User [{UserId1}] is not authorized by role for request [Boutquin.Mediator.Tests.Common.SimpleCommandWithRoleAuthorization+Command]. Authorization failed."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    //----------------------------------------------------------
    // Policy Tests
    //----------------------------------------------------------

    /// <summary>
    /// Verifies that unauthenticated policy-based commands are rejected by the authorization behavior.
    /// </summary>
    /// <remarks>This test ensures that the authorization behavior correctly identifies and rejects commands 
    /// requiring policy-based authorization when the user is not authenticated. It validates that  the appropriate
    /// error details are returned and that the expected log messages are generated.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task BehaviorShouldRejectUnauthenticatedPolicyBasedCommands()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        _ = services.AddMediator(
            typeof(SimpleCommandWithPolicyAuthorization.Handler).Assembly,
            typeof(AuthorizationBehavior<,>).Assembly);
        // Set up ILogger mock
        var loggerMock = new Mock<ILogger<SimpleCommandWithPolicyAuthorization.Command>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Set up a mock for the authorization service
        var authorizationServiceMock = new Mock<IAuthorizationService>();
        _ = services.AddSingleton(authorizationServiceMock.Object);
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple command to process
        var command = new SimpleCommandWithPolicyAuthorization.Command("Hello, World!", Fail: false);

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        // Process the command using the mediator
        var result = await mediator.SendAsync(command);

        //--------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        // Verify that the command was processed with an error
        Assert.NotNull(result);
        Assert.False(result.IsSuccess, "The command should not have been processed successfully.");
        Assert.NotNull(result.Error);
        // Verify that the error details are correct
        Assert.Equal("Error.Unauthorized", result.Error.Code);
        Assert.Equal(ErrorType.Unauthorized, result.Error.ErrorType);
        Assert.Equal("The request requires user authentication.", result.Error.Description);
        // Verify that the behavior logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Authorizing request [Boutquin.Mediator.Tests.Common.SimpleCommandWithPolicyAuthorization+Command]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Error, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "User is not authenticated for request [Boutquin.Mediator.Tests.Common.SimpleCommandWithPolicyAuthorization+Command]. Authorization failed."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    /// <summary>
    /// Verifies that the mediator correctly processes commands requiring policy-based authorization.
    /// </summary>
    /// <remarks>This test ensures that commands with policy-based authorization are handled as expected by
    /// the mediator, including proper logging and successful execution when authorization succeeds. It validates the
    /// integration of the authorization behavior, command handler, and logging components.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task BehaviorShouldCorrectlyProcessAuthorizedPolicyBasedCommands()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        _ = services.AddMediator(
                typeof(SimpleCommandWithPolicyAuthorization.Handler).Assembly,
                typeof(AuthorizationBehavior<,>).Assembly);
        // Set up ILogger mocks
        var loggerMock = new Mock<ILogger<SimpleCommandWithPolicyAuthorization.Command>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = currentUserServiceMock.Setup(x => x.UserId).Returns(UserId1);
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Set up a mock for the authorization service
        var authorizationServiceMock = new Mock<IAuthorizationService>();
        _ = authorizationServiceMock.Setup(x => x.AuthorizeAsync(
            It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));
        _ = services.AddSingleton(authorizationServiceMock.Object);
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple command to process
        var command = new SimpleCommandWithPolicyAuthorization.Command("Hello, World!");

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
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Authorizing request [Boutquin.Mediator.Tests.Common.SimpleCommandWithPolicyAuthorization+Command]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    /// <summary>
    /// Verifies that commands requiring policy-based authorization are rejected when the user is not authorized.
    /// </summary>
    /// <remarks>This test ensures that the mediator correctly processes commands with policy-based
    /// authorization and rejects them when the user does not meet the required policies. It validates that the
    /// appropriate error details are returned and that the expected logging behavior occurs.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task BehaviorShouldRejectForbiddenPolicyBasedCommands()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        _ = services.AddMediator(
            typeof(SimpleCommandWithPolicyAuthorization.Handler).Assembly,
            typeof(AuthorizationBehavior<,>).Assembly);
        // Set up ILogger mock
        var loggerMock = new Mock<ILogger<SimpleCommandWithPolicyAuthorization.Command>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Set up a mock for the current user service
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        _ = currentUserServiceMock.Setup(x => x.UserId).Returns(UserId1);
        _ = services.AddSingleton(currentUserServiceMock.Object);
        // Set up a mock for the authorization service
        var authorizationServiceMock = new Mock<IAuthorizationService>();
        _ = services.AddSingleton(authorizationServiceMock.Object);
        _ = authorizationServiceMock.Setup(x => x.AuthorizeAsync(
                It.IsAny<string>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(false));

        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple command to process
        var command = new SimpleCommandWithPolicyAuthorization.Command("Hello, World!", Fail: false);

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        // Process the command using the mediator
        var result = await mediator.SendAsync(command);

        //--------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        // Verify that the command was processed with an error
        Assert.NotNull(result);
        Assert.False(result.IsSuccess, "The command should not have been processed successfully.");
        Assert.NotNull(result.Error);
        // Verify that the error details are correct
        Assert.Equal("Error.Forbidden.Policy", result.Error.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.ErrorType);
        Assert.Equal($"User [{UserId1}] is not authorized for policies: [ADMIN_OR_USER].", result.Error.Description);
        // Verify that the behavior logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Authorizing request [Boutquin.Mediator.Tests.Common.SimpleCommandWithPolicyAuthorization+Command]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Error, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == $"User [{UserId1}] is not authorized by policy for request [Boutquin.Mediator.Tests.Common.SimpleCommandWithPolicyAuthorization+Command]. Authorization failed."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }
}
