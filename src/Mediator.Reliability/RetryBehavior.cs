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

namespace Boutquin.Mediator.Reliability;

// Boutquin.Mediator
using Abstractions;
using Boutquin.Domain.Abstractions;
using Boutquin.Reliability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// A pipeline behavior that retries the inner handler according to a configurable retry policy.
/// </summary>
/// <remarks>
/// Retry settings are read from <c>MediatorSettings:{requestName}:RetrySettings</c> in the application
/// configuration. When no configuration section is found (or <c>Enabled</c> is <see langword="false"/>),
/// the request is forwarded without retrying.
/// </remarks>
/// <typeparam name="TRequest">The type of the request being processed. Must implement <see cref="IRequest{TResponse}"/>.</typeparam>
/// <typeparam name="TResponse">The type of the response returned by the request. Must inherit from <see cref="Result"/>.</typeparam>
public sealed class RetryBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    private readonly IWaitWithRetry _waitWithRetry;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TRequest> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="RetryBehavior{TRequest, TResponse}"/>.
    /// </summary>
    /// <param name="waitWithRetry">The retry/timeout execution engine. Cannot be <see langword="null"/>.</param>
    /// <param name="configuration">The application configuration used to read per-request retry settings. Cannot be <see langword="null"/>.</param>
    /// <param name="logger">Logger for retry diagnostics. Cannot be <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="waitWithRetry"/>, <paramref name="configuration"/>, or <paramref name="logger"/> is <see langword="null"/>.
    /// </exception>
    public RetryBehavior(
        IWaitWithRetry waitWithRetry,
        IConfiguration configuration,
        ILogger<TRequest> logger)
    {
        ArgumentNullException.ThrowIfNull(waitWithRetry);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logger);

        _waitWithRetry = waitWithRetry;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async ValueTask<TResponse> HandleAsync(
        TRequest request,
        Func<ValueTask<TResponse>> next,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(next);

        var requestName = typeof(TRequest).FullName ?? string.Empty;

        // Retrieve settings from configuration (supports section per-request)
        var settings = GetRetrySettings(requestName);

        if (!settings.Enabled)
        {
            _logger.LogDebug("RetryBehavior is disabled for request [{RequestName}]. Proceeding without retries.", requestName);
            return await next().ConfigureAwait(false);
        }

        _logger.LogDebug(
            "Applying RetryBehavior for request [{RequestName}] with {NumberOfRetries} retries, timeout {TimeoutPerAttempt}, ContinueAfterTimeout={ContinueAfterTimeout}.",
            requestName, settings.NumberOfRetries, settings.TimeoutPerAttempt, settings.ContinueAfterTimeout);

        // Use WaitWithRetry to wrap the next pipeline delegate, threading the caller's
        // cancellation token so a cancelled request aborts the retry loop promptly.
        var result = await _waitWithRetry.WaitWithRetryAsync(
            async () => await next().ConfigureAwait(false),
            settings,
            cancellationToken).ConfigureAwait(false);

        return result;
    }

    private RetrySettings GetRetrySettings(string requestName)
    {
        var section = _configuration.GetSection($"MediatorSettings:{requestName}:RetrySettings");

        // Default fallback settings.
        //
        // SleepDurationsMs is intentionally NOT pre-seeded here: the .NET configuration binder
        // APPENDS to a pre-seeded non-null list rather than replacing it, so seeding [100] before
        // Bind() would turn a consumer's configured [5, 7] into [100, 5, 7], silently shifting the
        // backoff schedule by one slot. We bind first, then supply the [100] default only when the
        // bound value is null or empty.
        var settings = new RetrySettings
        {
            Enabled = false,
            ContinueAfterTimeout = false,
            NumberOfRetries = 2,
            TimeoutPerAttempt = TimeSpan.FromSeconds(10)
        };

        // Bind from config if available
        section.Bind(settings, c => c.ErrorOnUnknownConfiguration = false);

        // Apply the default backoff schedule only when config supplied none.
        if (settings.SleepDurationsMs is null or { Count: 0 })
        {
            settings.SleepDurationsMs = [100];
        }

        return settings;
    }
}
