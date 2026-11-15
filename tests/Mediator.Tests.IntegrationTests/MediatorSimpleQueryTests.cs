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
using Boutquin.Domain.Abstractions;
// Boutquin.Mediator.Tests
using Common;
using Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
/// Contains unit tests for verifying the behavior of the mediator when processing queries.
/// </summary>
/// <remarks>This class includes tests to ensure that the mediator correctly processes queries and handles both
/// successful and error scenarios. The tests validate the mediator's ability to process queries, log appropriate
/// messages, and return expected results or errors.</remarks>
public sealed class MediatorSimpleQueryTests
{
    /// <summary>
    /// Verifies that the mediator correctly processes queries and produces the expected results.
    /// </summary>
    /// <remarks>This test ensures that the mediator can handle a query, return a successful result, and log
    /// the appropriate messages. It validates the query's output and checks that the logger is invoked with the
    /// expected log entries.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task MediatorShouldCorrectlyProcessQueries()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(SimpleQuery.Handler).Assembly);
        // Set up ILogger mock
        var loggerMock = new Mock<ILogger<SimpleQuery.Query>>();
        _ = services.AddSingleton(loggerMock.Object);
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
        var result = await mediator.SendAsync<SimpleQuery.Query, Result<SimpleQuery.Output>>(query);

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
    }

    /// <summary>
    /// Verifies that the mediator correctly processes query errors and handles failure scenarios as expected.
    /// </summary>
    /// <remarks>This test ensures that when a query is intentionally designed to fail, the mediator: <list
    /// type="bullet"> <item>Returns a result indicating failure with the appropriate error details.</item> <item>Logs
    /// the expected debug and error messages.</item> </list> The test uses a mock logger to validate that the correct
    /// log messages are generated during query processing.</remarks>
    /// <returns></returns>
    [Fact]
    public async Task MediatorShouldCorrectlyProcessQueryErrors()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        // Create a service collection and add the mediator services
        var services = new ServiceCollection();
        _ = services.AddMediator(typeof(SimpleQuery.Handler).Assembly);
        // Set up ILogger mock
        var loggerMock = new Mock<ILogger<SimpleQuery.Query>>();
        _ = services.AddSingleton(loggerMock.Object);
        // Build the service provider
        var serviceProvider = services.BuildServiceProvider();
        // Create an instance of the mediator
        var mediator = serviceProvider.GetRequiredService<IMediator>();
        // Create a simple query to process
        var query = new SimpleQuery.Query("Ping...", Fail: true);

        //--------------------------------------------------------
        // Act
        //---------------------------------------------------------
        // Process the command using the mediator
        var result = await mediator.SendAsync<SimpleQuery.Query, Result<SimpleQuery.Output>>(query);

        //--------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        // Verify that the command was processed with an error
        Assert.NotNull(result);
        Assert.False(result.IsSuccess, "The query should not have been processed successfully.");
        Assert.NotNull(result.Error);
        // Verify that the error details are correct
        Assert.Equal("BadRequest.QueryFailed", result.Error.Code);
        Assert.Equal(ErrorType.BadRequest, result.Error.ErrorType);
        Assert.Equal("The query failed intentionally with a [BadRequest.QueryFailed] Error.", result.Error.Description);
        // Verify that the logger was called with the expected message
        loggerMock.Verify(
            x => x.Log(LogLevel.Debug, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Query received: [Ping...], expected to fail: [True]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
        loggerMock.Verify(
            x => x.Log(LogLevel.Error, It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v != null && v.ToString() == "Query failed intentionally with [Error.BadRequest]."),
                It.IsAny<Exception>(), (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()), Times.Once);
    }
}
