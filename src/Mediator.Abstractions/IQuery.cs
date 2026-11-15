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

namespace Boutquin.Mediator.Abstractions;

using Boutquin.Domain.Abstractions;

/// <summary>
/// Represents a query that produces a result of type <typeparamref name="TIQueryResult"/>.
/// </summary>
/// <remarks>This interface is typically used to define queries in a CQRS (Command Query Responsibility
/// Segregation) pattern. It extends <see cref="IRequest{TResponse}"/>, allowing it to be processed by a mediator or
/// similar request-handling mechanism.</remarks>
/// <typeparam name="TIQueryResult">The type of the result produced by the query. Must derive from <see cref="Result"/>.</typeparam>
public interface IQuery<out TIQueryResult>
    : IRequest<TIQueryResult> where TIQueryResult : Result;
