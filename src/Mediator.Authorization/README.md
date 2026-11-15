# Boutquin.Mediator.Authorization

`AuthorizationBehavior<TRequest, TResponse>` — a `Boutquin.Mediator` pipeline behavior that enforces `[Authorize]` on request types before the handler runs.

## How a request is checked

1. A request type with no `[Authorize]` attribute passes straight through.
2. Otherwise the caller must be authenticated: an empty `ICurrentUserService.UserId` returns `Error.Unauthorized` without calling the handler.
3. Roles from every `RequiredRoles` value are combined; the caller must be in **any** of them (`IAuthorizationService.IsInAnyRoleAsync`). Failure returns `Error.Forbidden` (`Error.Forbidden.Role`).
4. Policies from every `Policy` value are combined and passed to `IAuthorizationService.AuthorizeAsync`. Failure returns `Error.Forbidden` (`Error.Forbidden.Policy`).

Role and policy names are trimmed, de-duplicated and upper-cased (invariant culture) before your service sees them. An error `Result` from your service is returned unchanged. The behavior never throws for an authorization failure; the failure arrives as the request's `Result`.

## Registration

```csharp
services.AddMediator(
    typeof(ApproveInvoiceHandler).Assembly,
    typeof(AuthorizationBehavior<,>).Assembly);
services.AddScoped<ICurrentUserService>(/* see Boutquin.Abstractions */);
services.AddScoped<IAuthorizationService, MyAuthorizationService>();
```

Source, architecture notes and changelog: https://github.com/boutquin/Boutquin.Mediator
