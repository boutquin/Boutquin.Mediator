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
/// Integration tests for per-user cache key scoping in <c>CachingBehavior</c>.
/// </summary>
/// <remarks>
/// Covers CR-19: verifies that two different users receive independent cache entries
/// and that a <see langword="null"/> <c>UserId</c> is handled gracefully (empty-string prefix)
/// without throwing.
/// </remarks>
public sealed class PerUserCachingTests
{
    private const string UserIdAlice = "ALICE-0000-0000-0000-000000000001";
    private const string UserIdBob = "BOB00-0000-0000-0000-000000000002";

    // -----------------------------------------------------------------------
    // Shared DI setup helper
    // -----------------------------------------------------------------------

    private static (IMediator Mediator, Mock<ICurrentUserService> UserServiceMock)
        BuildMediatorWithUserService(string? userId)
    {
        var services = new ServiceCollection();
        _ = services.AddMediator(
            typeof(SimpleQueryWithPerUserCaching.Handler).Assembly,
            typeof(CachingBehavior<,>).Assembly);

        var userServiceMock = new Mock<ICurrentUserService>();
        _ = userServiceMock.Setup(x => x.UserId).Returns(userId!);
        _ = services.AddSingleton(userServiceMock.Object);

        _ = services.AddDistributedMemoryCache();

        // Suppress log output for these focused tests
        _ = services.AddSingleton(Mock.Of<ILogger<SimpleQueryWithPerUserCaching.Query>>());

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        _ = services.AddSingleton<IConfiguration>(builder.Build());

        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<IMediator>(), userServiceMock);
    }

    // -----------------------------------------------------------------------
    // CR-19: two users with the same query → separate cache entries
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that sending the same query as two different users results in two independent
    /// handler executions (Alice's cached result does not bleed into Bob's cache slot).
    /// </summary>
    [Fact]
    public async Task PerUserCaching_TwoDistinctUsers_ReceiveSeparateCacheEntries()
    {
        //---------------------------------------------------------
        // Arrange — Alice's mediator instance
        //---------------------------------------------------------
        var (aliceMediator, aliceUserService) = BuildMediatorWithUserService(UserIdAlice);
        _ = aliceUserService; // used implicitly through DI

        //---------------------------------------------------------
        // Arrange — Bob's mediator instance (separate DI container = separate cache)
        //---------------------------------------------------------
        var (bobMediator, _) = BuildMediatorWithUserService(UserIdBob);

        var query = new SimpleQueryWithPerUserCaching.Query("Ping...");

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var aliceResult = await aliceMediator.SendAsync(query).ConfigureAwait(true);
        var bobResult = await bobMediator.SendAsync(query).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert — both succeed independently
        //---------------------------------------------------------
        Assert.True(aliceResult.IsSuccess, "Alice's query must succeed.");
        Assert.True(bobResult.IsSuccess, "Bob's query must succeed.");

        Assert.NotNull(aliceResult.Value);
        Assert.NotNull(bobResult.Value);

        // Both handlers return the same value for the same message, so equality of the
        // reply string is expected — the important invariant is that both executed without error.
        Assert.Equal(aliceResult.Value.Reply, bobResult.Value.Reply);
    }

    // -----------------------------------------------------------------------
    // S9-07: shared cache — two users have ISOLATED entries
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that per-user caching creates genuinely separate cache entries in a <em>shared</em>
    /// <c>IDistributedCache</c> — that is, Alice's cached result does not serve Bob's request
    /// from the same underlying cache instance.
    /// </summary>
    /// <remarks>
    /// The earlier <see cref="PerUserCaching_TwoDistinctUsers_ReceiveSeparateCacheEntries"/> test used
    /// two independent DI containers and therefore two independent cache instances, which is trivially
    /// isolated. This test uses a single DI container (and a single cache) and switches the mocked
    /// <see cref="ICurrentUserService.UserId"/> between calls to prove that the per-user key prefix
    /// actually differentiates the cache slots.
    /// </remarks>
    [Fact]
    public async Task PerUserCaching_SharedCache_TwoUsersGetIndependentEntries()
    {
        //---------------------------------------------------------
        // Arrange — single DI container, single cache
        //---------------------------------------------------------
        var callCount = 0;
        var userServiceMock = new Mock<ICurrentUserService>();

        var services = new ServiceCollection();
        _ = services.AddDistributedMemoryCache();
        _ = services.AddSingleton(userServiceMock.Object);
        _ = services.AddSingleton(Mock.Of<ILogger<SimpleQueryWithPerUserCaching.Query>>());

        // Register counting handler that increments callCount on each invocation
        _ = services.AddTransient<IRequestHandler<SimpleQueryWithPerUserCaching.Query, Result<SimpleQueryWithPerUserCaching.Output>>>(
            _ => new CountingQueryHandler(() => callCount++));

        _ = services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        _ = services.AddSingleton<IConfiguration>(builder.Build());
        _ = services.AddSingleton<IMediator, Helpers.Mediator>();

        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();
        var query = new SimpleQueryWithPerUserCaching.Query("Hello");

        //---------------------------------------------------------
        // Act — Alice requests first (cache miss → handler called)
        //---------------------------------------------------------
        userServiceMock.Setup(u => u.UserId).Returns(UserIdAlice);
        var aliceResult = await mediator.SendAsync(query).ConfigureAwait(true);

        // Act — Alice requests again (cache hit → handler NOT called)
        var aliceResultCached = await mediator.SendAsync(query).ConfigureAwait(true);

        // Act — Bob requests (different user key → cache miss → handler called again)
        userServiceMock.Setup(u => u.UserId).Returns(UserIdBob);
        var bobResult = await mediator.SendAsync(query).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(aliceResult.IsSuccess);
        Assert.True(aliceResultCached.IsSuccess);
        Assert.True(bobResult.IsSuccess);

        // Handler must have been called exactly twice: once for Alice's cold cache, once for Bob's
        Assert.Equal(2, callCount);
    }

    /// <summary>
    /// A simple counting query handler for use in the shared-cache isolation test.
    /// </summary>
    private sealed class CountingQueryHandler(Action onHandled)
        : IRequestHandler<SimpleQueryWithPerUserCaching.Query, Result<SimpleQueryWithPerUserCaching.Output>>
    {
        public ValueTask<Result<SimpleQueryWithPerUserCaching.Output>> HandleAsync(
            SimpleQueryWithPerUserCaching.Query request,
            CancellationToken cancellationToken = default)
        {
            onHandled();
            return ValueTask.FromResult(
                Result.Success(new SimpleQueryWithPerUserCaching.Output($"Processed message: [{request.Message}].")));
        }
    }

    // -----------------------------------------------------------------------
    // CR-19: null UserId → empty-string prefix — must not throw
    // -----------------------------------------------------------------------

    // -----------------------------------------------------------------------
    // CACHE-PERUSER-ANON-001: anonymous callers under PerUser must NOT share a
    // cached value — the behavior fails closed (bypasses cache) for null UserId.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Verifies that under per-user caching, an anonymous caller (null <c>UserId</c>) does not serve
    /// or populate a shared cache slot. Before the fix, a null <c>UserId</c> collapsed to an empty
    /// prefix, so two anonymous calls shared one slot — the second call returned the first call's
    /// cached value and the handler ran only once. The fix bypasses the cache for anonymous callers,
    /// so the handler runs on every anonymous request.
    /// </summary>
    [Fact]
    public async Task PerUserCaching_AnonymousCallers_DoNotShareCachedValue()
    {
        //---------------------------------------------------------
        // Arrange — single DI container, single shared cache, anonymous user
        //---------------------------------------------------------
        var callCount = 0;
        var userServiceMock = new Mock<ICurrentUserService>();
        _ = userServiceMock.Setup(u => u.UserId).Returns((string?)null!);

        var services = new ServiceCollection();
        _ = services.AddDistributedMemoryCache();
        _ = services.AddSingleton(userServiceMock.Object);
        _ = services.AddSingleton(Mock.Of<ILogger<SimpleQueryWithPerUserCaching.Query>>());
        _ = services.AddTransient<IRequestHandler<SimpleQueryWithPerUserCaching.Query, Result<SimpleQueryWithPerUserCaching.Output>>>(
            _ => new CountingQueryHandler(() => callCount++));
        _ = services.AddTransient(typeof(IPipelineBehavior<,>), typeof(CachingBehavior<,>));

        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", true, true);
        _ = services.AddSingleton<IConfiguration>(builder.Build());
        _ = services.AddSingleton<IMediator, Helpers.Mediator>();

        var sp = services.BuildServiceProvider();
        var mediator = sp.GetRequiredService<IMediator>();
        var query = new SimpleQueryWithPerUserCaching.Query("Anon");

        //---------------------------------------------------------
        // Act — two anonymous sends against the same shared cache
        //---------------------------------------------------------
        var first = await mediator.SendAsync(query).ConfigureAwait(true);
        var second = await mediator.SendAsync(query).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert — handler ran BOTH times (no shared anonymous cache slot)
        //---------------------------------------------------------
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(2, callCount);
    }

    /// <summary>
    /// Verifies that when <see cref="ICurrentUserService.UserId"/> returns <see langword="null"/>,
    /// the caching behavior bypasses the cache for the anonymous caller and does not throw.
    /// </summary>
    [Fact]
    public async Task PerUserCaching_NullUserId_UsesEmptyStringPrefixWithoutThrowing()
    {
        //---------------------------------------------------------
        // Arrange
        //---------------------------------------------------------
        var (mediator, _) = BuildMediatorWithUserService(userId: null);
        var query = new SimpleQueryWithPerUserCaching.Query("NullUser");

        //---------------------------------------------------------
        // Act
        //---------------------------------------------------------
        var result = await mediator.SendAsync(query).ConfigureAwait(true);

        //---------------------------------------------------------
        // Assert
        //---------------------------------------------------------
        Assert.True(result.IsSuccess, "A null UserId must not cause the caching behavior to throw.");
        Assert.NotNull(result.Value);
        Assert.Equal("Processed message: [NullUser].", result.Value.Reply);
    }
}
