# Boutquin.Abstractions

Current-user and authorization abstractions used by the `Boutquin.Mediator.Authorization` and `Boutquin.Mediator.Caching` pipeline behaviors. The package has no dependency on ASP.NET Core; you supply the identity source.

| Type | Purpose |
|------|---------|
| `ICurrentUserService` | Exposes the current caller's `UserId` (`null` or empty for an anonymous caller). |
| `CurrentUserService` | `ICurrentUserService` backed by a `Func<ClaimsPrincipal?>`; reads the `NameIdentifier` claim. |
| `CurrentWindowsUserService` | `ICurrentUserService` backed by the current Windows identity. Windows only — throws `PlatformNotSupportedException` elsewhere. |
| `IAuthorizationService` | Your role and policy checks: `IsInAnyRoleAsync` and `AuthorizeAsync`, each returning `Result<bool>`. |
| `AuthorizeAttribute` | Marks a request type as requiring authorization. `RequiredRoles` and `Policy` take comma-separated lists. |

## Example

```csharp
using Boutquin.Abstractions.Auth;

// ASP.NET Core host: read the principal from the current HTTP context.
services.AddHttpContextAccessor();
services.AddScoped<ICurrentUserService>(sp =>
{
    var accessor = sp.GetRequiredService<IHttpContextAccessor>();
    return new CurrentUserService(() => accessor.HttpContext?.User);
});
services.AddScoped<IAuthorizationService, MyAuthorizationService>();

[Authorize(RequiredRoles = "Admin,Manager")]
public sealed record ApproveInvoice(Guid InvoiceId) : ICommand;
```

Source, architecture notes and changelog: https://github.com/boutquin/Boutquin.Mediator
