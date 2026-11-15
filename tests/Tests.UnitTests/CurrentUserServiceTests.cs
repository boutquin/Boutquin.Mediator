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

using System.Security.Claims;

using Boutquin.Abstractions.Auth;

public class CurrentUserServiceTests
{
    [Fact]
    public void UserId_PrincipalWithNameIdentifier_ReturnsClaimValue()
    {
        // Arrange: a principal carrying a NameIdentifier claim is supplied to the service.
        var principal = new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-123")]));
        var sut = new CurrentUserService(() => principal);

        // Act
        var userId = sut.UserId;

        // Assert: the service must surface the injected principal's id, not the ambient static.
        Assert.Equal("user-123", userId);
    }

    [Fact]
    public void UserId_NullPrincipal_ReturnsNull()
    {
        // Arrange
        var sut = new CurrentUserService(() => null);

        // Act
        var userId = sut.UserId;

        // Assert
        Assert.Null(userId);
    }

    [Fact]
    public void UserId_PrincipalWithoutNameIdentifier_ReturnsNull()
    {
        // Arrange
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        var sut = new CurrentUserService(() => principal);

        // Act
        var userId = sut.UserId;

        // Assert
        Assert.Null(userId);
    }

    [Fact]
    public void Constructor_NullAccessor_ThrowsArgumentNullException()
    {
        // Act + Assert
        _ = Assert.Throws<ArgumentNullException>(() => new CurrentUserService(null!));
    }
}

public class CurrentWindowsUserServiceTests
{
    [Fact]
    public void UserId_OnNonWindowsHost_ThrowsClearPlatformNotSupportedException()
    {
        if (OperatingSystem.IsWindows())
        {
            // On Windows the call resolves a real identity; this guard-message assertion
            // is only meaningful on non-Windows hosts (the macOS/Linux CI surface).
            return;
        }

        // This test deliberately instantiates and invokes a [SupportedOSPlatform("windows")]
        // type on a non-Windows host to prove its runtime guard fires — that is the behaviour
        // under test, so CA1416 (the compile-time "only supported on Windows" hint) is
        // intentionally suppressed for this scope.
#pragma warning disable CA1416 // Validate platform compatibility
        // Arrange
        var sut = new CurrentWindowsUserService();

        // Act + Assert: the service must surface a clear, documented platform error
        // rather than the raw BCL PlatformNotSupportedException from WindowsIdentity.
        var ex = Assert.Throws<PlatformNotSupportedException>(() => _ = sut.UserId);
        Assert.Contains("CurrentWindowsUserService requires Windows", ex.Message);
#pragma warning restore CA1416
    }
}
