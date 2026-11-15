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

namespace Boutquin.Mediator.Abstractions.Extensions;

using System.Collections.Concurrent;
using System.Reflection;
using Boutquin.Domain.Abstractions;

/// <summary>
/// Provides extension methods for the <see cref="ISender"/> interface.
/// </summary>
public static class SenderExtensions
{
    /// <summary>
    /// Cache of <see cref="MethodInfo"/> for <see cref="ISender.SendAsync{TRequest,TResponse}"/> keyed by (concrete request type, response type).
    /// Avoids repeated <see cref="MethodInfo.MakeGenericMethod(Type[])"/> allocations on every call.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type, Type), MethodInfo> s_sendMethodCache = new();

    /// <summary>
    /// Sends a request asynchronously and infers the response type from the request type.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response expected. Must inherit from <see cref="Result"/>.</typeparam>
    /// <param name="sender">The sender instance used to send the request. Cannot be <see langword="null"/>.</param>
    /// <param name="request">The request object to be sent. Cannot be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. The default value is <see cref="CancellationToken.None"/>.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains the response of
    /// type <typeparamref name="TResponse"/>.</returns>
    public static ValueTask<TResponse> SendAsync<TResponse>(this ISender sender, IRequest<TResponse> request, CancellationToken cancellationToken = default)
        where TResponse : Result
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(request);

        var concreteType = request.GetType();
        var method = s_sendMethodCache.GetOrAdd(
            (concreteType, typeof(TResponse)),
            key => typeof(ISender)
                .GetMethod(nameof(ISender.SendAsync))!
                .MakeGenericMethod(key.Item1, key.Item2));
        return (ValueTask<TResponse>)method.Invoke(sender, [request, cancellationToken])!;
    }

    /// <summary>
    /// Sends a concrete request asynchronously and infers both the request interface and response type.
    /// </summary>
    /// <typeparam name="TConcreteRequest">The concrete type of the request being sent.</typeparam>
    /// <param name="sender">The sender instance used to send the request. Cannot be <see langword="null"/>.</param>
    /// <param name="request">The concrete request object to be sent. Cannot be <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests. Required here (unlike the strongly-typed overload) so the two overloads do not both carry optional parameters (RS0026); pass <see cref="CancellationToken.None"/> when no token applies.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> representing the asynchronous operation. The result contains the inferred response type.</returns>
    /// <remarks>
    /// This extension method allows sending a concrete request type without having to explicitly specify the response type,
    /// making it more convenient for use in tests and application code when the response type can be inferred.
    /// <para><b>Type-safety warning:</b> This overload uses reflection to resolve the response type at runtime and returns
    /// <see langword="dynamic"/>, which loses compile-time type safety and incurs reflection overhead on every call.
    /// Prefer the strongly-typed <c>SendAsync&lt;TRequest, TResponse&gt;</c> overload whenever the response type is
    /// known at compile time.</para>
    /// </remarks>
    [Obsolete("Prefer the strongly-typed SendAsync<TResponse>(ISender, IRequest<TResponse>, CancellationToken) overload. This overload loses type safety and incurs reflection overhead on every call.")]
    public static async ValueTask<dynamic> SendAsync<TConcreteRequest>(this ISender sender, TConcreteRequest request, CancellationToken cancellationToken)
        where TConcreteRequest : class
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(request);

        // Use reflection to find the IRequest<TResponse> interface implemented by TConcreteRequest
        var requestInterfaceType = request.GetType()
            .GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IRequest<>));

        if (requestInterfaceType == null)
        {
            throw new InvalidOperationException(
                $"Type {typeof(TConcreteRequest).Name} does not implement IRequest<TResponse>.");
        }

        var responseType = requestInterfaceType.GetGenericArguments()[0];

        // Resolve (or create+cache) the generic SendAsync<TConcreteRequest, TResponse> method
        var method = s_sendMethodCache.GetOrAdd(
            (typeof(TConcreteRequest), responseType),
            key => typeof(ISender)
                .GetMethod(nameof(ISender.SendAsync))!
                .MakeGenericMethod(key.Item1, key.Item2));

        dynamic resultTask = method.Invoke(sender, [request, cancellationToken])!;
        return await resultTask;
    }
}
