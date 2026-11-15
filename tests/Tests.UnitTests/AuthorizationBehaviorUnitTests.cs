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

namespace Boutquin.Tests.UnitTests;

using Boutquin.Abstractions.Auth;
using Boutquin.Domain.Abstractions;
using Boutquin.Mediator.Abstractions;
using Boutquin.Mediator.Authorization;

// Dummy request type for unit testing
internal sealed class DummyRequest : IRequest<Result>;

// For attribute-based test cases, define requests with Authorize attributes
[Authorize(RequiredRoles = "ADMIN,USER")]
internal sealed class RoleSecuredRequest : IRequest<Result>;

[Authorize(Policy = "ADMIN_OR_USER")]
internal sealed class PolicySecuredRequest : IRequest<Result>;

public class AuthorizationBehaviorUnitTests
{
    // ------------------------------------------------------------------
    // Helper Methods & Setup
    // ------------------------------------------------------------------
    private static Func<ValueTask<Result>> SuccessfulNext(Result? response = null)
        => () => ValueTask.FromResult(response ?? Result.Success());

    private static Func<ValueTask<Result>> ThrowingNext(Exception ex)
        => () => throw ex;

    private const string UserId = "user-123";

    // ------------------------------------------------------------------
    // 1. No Authorize Attribute → Next is called, no auth checks
    // ------------------------------------------------------------------
    [Fact]
    public async Task HandleAsync_NoAuthorizeAttribute_CallsNextDirectly()
    {
        // Arrange
        var logger = new Mock<ILogger<DummyRequest>>();
        var currentUser = new Mock<ICurrentUserService>();
        var authService = new Mock<IAuthorizationService>();
        var called = false;
        var expected = Result.Success();

        var behavior = new AuthorizationBehavior<DummyRequest, Result>(currentUser.Object, authService.Object, logger.Object);

        // Act
        var result = await behavior.HandleAsync(new DummyRequest(), Next).ConfigureAwait(true);

        // Assert
        Assert.True(called);
        Assert.Same(expected, result);
        logger.Verify(l => l.Log(LogLevel.Debug, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains("Authorizing request")), null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        logger.Verify(l => l.Log(LogLevel.Debug, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains("No authorization attributes found")), null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        logger.VerifyNoOtherCalls();
        return;

        ValueTask<Result> Next()
        {
            called = true;
            return ValueTask.FromResult(expected);
        }
    }

    // ------------------------------------------------------------------
    // 2. Authorize Attribute Present - Unauthenticated User → Unauthorized
    // ------------------------------------------------------------------
    [Fact]
    public async Task HandleAsync_AuthorizeAttributePresent_UserNotAuthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var logger = new Mock<ILogger<RoleSecuredRequest>>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(string.Empty); // User not authenticated
        var authService = new Mock<IAuthorizationService>();
        var next = SuccessfulNext();

        var behavior = new AuthorizationBehavior<RoleSecuredRequest, Result>(currentUser.Object, authService.Object, logger.Object);

        // Act
        var result = await behavior.HandleAsync(new RoleSecuredRequest(), next).ConfigureAwait(true);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Error.Unauthorized", result.Error.Code);
        Assert.Equal(ErrorType.Unauthorized, result.Error.ErrorType);
        Assert.Equal("The request requires user authentication.", result.Error.Description);

        logger.Verify(l => l.Log(LogLevel.Debug, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains("Authorizing request")), null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains("User is not authenticated")), null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        logger.VerifyNoOtherCalls();
    }

    // ------------------------------------------------------------------
    // 3. Role Authorization Fails → Forbidden
    // ------------------------------------------------------------------
    [Fact]
    public async Task HandleAsync_UserNotInRole_ReturnsForbidden()
    {
        // Arrange
        var logger = new Mock<ILogger<RoleSecuredRequest>>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(UserId);

        var authService = new Mock<IAuthorizationService>();
        authService.Setup(a => a.IsInAnyRoleAsync(UserId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(false));

        var next = SuccessfulNext();

        var behavior = new AuthorizationBehavior<RoleSecuredRequest, Result>(currentUser.Object, authService.Object, logger.Object);

        // Act
        var result = await behavior.HandleAsync(new RoleSecuredRequest(), next).ConfigureAwait(true);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Error.Forbidden.Role", result.Error.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.ErrorType);
        Assert.Contains(UserId, result.Error.Description);
        Assert.Contains("not authorized for required roles", result.Error.Description);

        logger.Verify(l => l.Log(LogLevel.Debug, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains("not authorized by role")), null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        logger.VerifyNoOtherCalls();
    }

    // ------------------------------------------------------------------
    // 4. Policy Authorization Fails → Forbidden
    // ------------------------------------------------------------------
    [Fact]
    public async Task HandleAsync_UserNotInPolicy_ReturnsForbidden()
    {
        // Arrange
        var logger = new Mock<ILogger<PolicySecuredRequest>>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(UserId);

        var authService = new Mock<IAuthorizationService>();
        authService.Setup(a => a.IsInAnyRoleAsync(UserId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true)); // Roles pass (no roles in attribute)
        authService.Setup(a => a.AuthorizeAsync(UserId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(false));

        var next = SuccessfulNext();

        var behavior = new AuthorizationBehavior<PolicySecuredRequest, Result>(currentUser.Object, authService.Object, logger.Object);

        // Act
        var result = await behavior.HandleAsync(new PolicySecuredRequest(), next).ConfigureAwait(true);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Equal("Error.Forbidden.Policy", result.Error.Code);
        Assert.Equal(ErrorType.Forbidden, result.Error.ErrorType);
        Assert.Contains(UserId, result.Error.Description);
        Assert.Contains("not authorized for policies", result.Error.Description);

        logger.Verify(l => l.Log(LogLevel.Debug, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(),
            It.Is<It.IsAnyType>((v, t) => v != null && v.ToString()!.Contains("not authorized by policy")), null,
            It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        logger.VerifyNoOtherCalls();
    }

    // ------------------------------------------------------------------
    // 5. All Authorization Passes → Calls Next
    // ------------------------------------------------------------------
    [Fact]
    public async Task HandleAsync_AuthorizationPasses_CallsNext()
    {
        // Arrange
        var logger = new Mock<ILogger<RoleSecuredRequest>>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(UserId);

        var authService = new Mock<IAuthorizationService>();
        authService.Setup(a => a.IsInAnyRoleAsync(UserId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));
        authService.Setup(a => a.AuthorizeAsync(UserId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true)); // For completeness, even if not needed by this attribute

        var called = false;

        var behavior = new AuthorizationBehavior<RoleSecuredRequest, Result>(currentUser.Object, authService.Object, logger.Object);

        // Act
        var result = await behavior.HandleAsync(new RoleSecuredRequest(), Next).ConfigureAwait(true);

        // Assert
        Assert.True(called);
        Assert.True(result.IsSuccess);

        logger.Verify(l => l.Log(LogLevel.Debug, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        logger.VerifyNoOtherCalls();
        return;

        ValueTask<Result> Next()
        {
            called = true;
            return ValueTask.FromResult(Result.Success());
        }
    }

    // ------------------------------------------------------------------
    // 6. Authorization Service Returns Failure Result (internal error)
    // ------------------------------------------------------------------
    [Fact]
    public async Task HandleAsync_RoleServiceReturnsError_ReturnsError()
    {
        // Arrange
        var logger = new Mock<ILogger<RoleSecuredRequest>>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(UserId);

        var expectedError = Error.InternalServerError("Error.Failure", "Role service failed.");
        var authService = new Mock<IAuthorizationService>();
        authService.Setup(a => a.IsInAnyRoleAsync(UserId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<bool>(expectedError));

        var next = SuccessfulNext();

        var behavior = new AuthorizationBehavior<RoleSecuredRequest, Result>(currentUser.Object, authService.Object, logger.Object);

        // Act
        var result = await behavior.HandleAsync(new RoleSecuredRequest(), next).ConfigureAwait(true);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Same(expectedError, result.Error);
    }

    [Fact]
    public async Task HandleAsync_PolicyServiceReturnsError_ReturnsError()
    {
        // Arrange
        var logger = new Mock<ILogger<PolicySecuredRequest>>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(UserId);

        var expectedError = Error.InternalServerError("Error.Failure", "Policy service failed.");
        var authService = new Mock<IAuthorizationService>();
        authService.Setup(a => a.IsInAnyRoleAsync(UserId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(true));
        authService.Setup(a => a.AuthorizeAsync(UserId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<bool>(expectedError));

        var next = SuccessfulNext();

        var behavior = new AuthorizationBehavior<PolicySecuredRequest, Result>(currentUser.Object, authService.Object, logger.Object);

        // Act
        var result = await behavior.HandleAsync(new PolicySecuredRequest(), next).ConfigureAwait(true);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Same(expectedError, result.Error);
    }

    // ------------------------------------------------------------------
    // 7. Throws on Null Arguments
    // ------------------------------------------------------------------
    [Fact]
    public async Task HandleAsync_ThrowsOnNullRequestOrNext()
    {
        // Arrange
        var logger = new Mock<ILogger<DummyRequest>>();
        var currentUser = new Mock<ICurrentUserService>();
        var authService = new Mock<IAuthorizationService>();
        var behavior = new AuthorizationBehavior<DummyRequest, Result>(currentUser.Object, authService.Object, logger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => behavior.HandleAsync(null!, SuccessfulNext()).AsTask()).ConfigureAwait(true);
        await Assert.ThrowsAsync<ArgumentNullException>(() => behavior.HandleAsync(new DummyRequest(), null!).AsTask()).ConfigureAwait(true);
    }

    // ------------------------------------------------------------------
    // 8. Exception in Next is Propagated
    // ------------------------------------------------------------------
    [Fact]
    public async Task HandleAsync_PropagatesExceptionFromNext()
    {
        // Arrange
        var logger = new Mock<ILogger<DummyRequest>>();
        var currentUser = new Mock<ICurrentUserService>();
        var authService = new Mock<IAuthorizationService>();
        var behavior = new AuthorizationBehavior<DummyRequest, Result>(currentUser.Object, authService.Object, logger.Object);

        var ex = new InvalidOperationException("fail");
        var next = ThrowingNext(ex);

        // Act & Assert
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => behavior.HandleAsync(new DummyRequest(), next).AsTask()).ConfigureAwait(true);
        Assert.Same(ex, thrown);
    }

    // ------------------------------------------------------------------
    // S3-01: Constructor null guards
    // ------------------------------------------------------------------

    /// <summary>
    /// Verifies that passing a null <c>currentUserService</c> to the constructor throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Constructor_NullCurrentUserService_ThrowsArgumentNullException()
    {
        var logger = new Mock<ILogger<DummyRequest>>();
        var authService = new Mock<IAuthorizationService>();

        var ex = Assert.Throws<ArgumentNullException>(
            () => new AuthorizationBehavior<DummyRequest, Result>(null!, authService.Object, logger.Object));
        Assert.Equal("currentUserService", ex.ParamName);
    }

    /// <summary>
    /// Verifies that passing a null <c>authorizationService</c> to the constructor throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Constructor_NullAuthorizationService_ThrowsArgumentNullException()
    {
        var logger = new Mock<ILogger<DummyRequest>>();
        var currentUser = new Mock<ICurrentUserService>();

        var ex = Assert.Throws<ArgumentNullException>(
            () => new AuthorizationBehavior<DummyRequest, Result>(currentUser.Object, null!, logger.Object));
        Assert.Equal("authorizationService", ex.ParamName);
    }

    /// <summary>
    /// Verifies that passing a null <c>logger</c> to the constructor throws <see cref="ArgumentNullException"/>.
    /// </summary>
    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var currentUser = new Mock<ICurrentUserService>();
        var authService = new Mock<IAuthorizationService>();

        var ex = Assert.Throws<ArgumentNullException>(
            () => new AuthorizationBehavior<DummyRequest, Result>(currentUser.Object, authService.Object, null!));
        Assert.Equal("logger", ex.ParamName);
    }

    // ------------------------------------------------------------------
    // S9-02: TResponse = Result<T> (generic) — unsafe cast fixed
    // ------------------------------------------------------------------

    /// <summary>Dummy request whose handler returns <see cref="Result{T}"/>.</summary>
    [Authorize]
    private sealed class GenericResultRequest : IRequest<Result<string>>;

    /// <summary>
    /// Verifies that <see cref="AuthorizationBehavior{TRequest,TResponse}"/> correctly creates a typed
    /// <see cref="Result{T}"/> failure when <c>TResponse</c> is <c>Result&lt;string&gt;</c>
    /// and the user is unauthenticated — proving the unsafe <c>(TResponse)Result.Failure(error)</c> cast
    /// has been replaced with a reflection-based factory.
    /// </summary>
    [Fact]
    public async Task HandleAsync_GenericResultT_UnauthenticatedUser_ReturnsTypedFailure()
    {
        // Arrange — NullLogger avoids Moq accessibility issues with the private test type
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<GenericResultRequest>.Instance;
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.Setup(u => u.UserId).Returns(string.Empty); // not authenticated

        var authService = new Mock<IAuthorizationService>();

        var behavior = new AuthorizationBehavior<GenericResultRequest, Result<string>>(
            currentUser.Object, authService.Object, logger);

        var next = new Func<ValueTask<Result<string>>>(
            () => ValueTask.FromResult(Result.Success("hello")));

        // Act — must NOT throw InvalidCastException
        var response = await behavior.HandleAsync(new GenericResultRequest(), next).ConfigureAwait(true);

        // Assert
        Assert.False(response.IsSuccess);
        Assert.Equal("Error.Unauthorized", response.Error.Code);
    }

    // ------------------------------------------------------------------
    // AUTH-PARAMNAME-001 (F-V3-01): IAuthorizationService cancellation
    // parameters must be named `cancellationToken` (repo-wide convention),
    // so that named-argument call sites compile.
    // ------------------------------------------------------------------

    /// <summary>
    /// AUTH-PARAMNAME-001 (F-V3-01): both <see cref="IAuthorizationService.IsInAnyRoleAsync"/> and
    /// <see cref="IAuthorizationService.AuthorizeAsync"/> must name their <see cref="CancellationToken"/>
    /// parameter <c>cancellationToken</c>, matching the repo-wide convention. This test calls both with
    /// the <c>cancellationToken:</c> NAMED argument: if either parameter is still named <c>cancellation</c>,
    /// this file fails to COMPILE (the parameter name is part of a public interface contract). Compilation
    /// of this test is therefore the proof of the rename.
    /// </summary>
    [Fact]
    public async Task IAuthorizationService_Methods_AcceptCancellationTokenNamedArgument()
    {
        // Arrange
        var authService = new Mock<IAuthorizationService>();
        authService
            .Setup(a => a.IsInAnyRoleAsync(UserId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));
        authService
            .Setup(a => a.AuthorizeAsync(UserId, It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<bool>.Success(true));

        IAuthorizationService sut = authService.Object;
        using var cts = new CancellationTokenSource();

        // Act — the `cancellationToken:` named argument only compiles if the rename landed
        var roleResult = await sut.IsInAnyRoleAsync(UserId, ["ADMIN"], cancellationToken: cts.Token).ConfigureAwait(true);
        var policyResult = await sut.AuthorizeAsync(UserId, ["ADMIN_OR_USER"], cancellationToken: cts.Token).ConfigureAwait(true);

        // Assert
        Assert.True(roleResult.Value);
        Assert.True(policyResult.Value);
    }
}
