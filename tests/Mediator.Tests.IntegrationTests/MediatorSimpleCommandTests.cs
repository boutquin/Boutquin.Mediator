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
using Boutquin.Domain.Abstractions;
// Boutquin.Mediator.Tests
using Common;
using Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
/// Contains unit tests for verifying the behavior of the mediator when processing commands.
/// </summary>
/// <remarks>This test class ensures that the mediator correctly processes commands and handles both successful
/// and error scenarios. It uses mocked dependencies, such as <see cref="ILogger{TCategoryName}"/>, to verify expected
/// logging behavior and command processing outcomes.</remarks>
public sealed class MediatorSimpleCommandTests
{
    /// <summary>
    /// Verifies that the mediator correctly processes commands and logs the expected messages.
    /// </summary>
    /// <remarks>This test ensures that a command is successfully processed by the mediator and that the
    /// appropriate log messages are generated during the process. It uses a mocked logger to validate the logging
    /// behavior.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task MediatorShouldCorrectlyProcessCommands()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(SimpleCommand.Handler).Assembly);
        // Set up ILogger mock
        var loggerMock = new Mock<ILogger<SimpleCommand.Command>>();
        _ = services.AddSingleton(loggerMock.Object);
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
        // Verify that the logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Command received: [Hello, World!], expected to fail: [False]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Information, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Command executed successfully."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }

    /// <summary>
    /// Verifies that the mediator correctly processes command errors and logs the appropriate messages.
    /// </summary>
    /// <remarks>This test ensures that when a command is processed by the mediator and fails intentionally, 
    /// the resulting error is properly handled, the error details are accurate, and the expected  log messages are
    /// generated. The test uses a mock logger to verify logging behavior.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task MediatorShouldCorrectlyProcessCommandErrors()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(SimpleCommand.Handler).Assembly);
        // Set up ILogger mock
        var loggerMock = new Mock<ILogger<SimpleCommand.Command>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple command to process
        var command = new SimpleCommand.Command("Hello, World!", Fail: true);

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
        Assert.Equal("BadRequest.CommandFailed", result.Error.Code);
        Assert.Equal(ErrorType.BadRequest, result.Error.ErrorType);
        Assert.Equal("The command failed intentionally with a [BadRequest.CommandFailed] Error.", result.Error.Description);
        // Verify that the logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Command received: [Hello, World!], expected to fail: [True]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Error, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Command failed intentionally with [Error.BadRequest]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }
}
