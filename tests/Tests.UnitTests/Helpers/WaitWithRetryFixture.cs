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

namespace Boutquin.Tests.UnitTests.Helpers;

/// <summary>
/// Provides a fixture for creating instances of <see cref="WaitWithRetry"/> along with a mocked logger.
/// </summary>
/// <remarks>This utility is intended for testing scenarios where <see cref="WaitWithRetry"/> requires a logger.
/// It creates a new instance of <see cref="WaitWithRetry"/> and a mock implementation of <see
/// cref="ILogger{T}"/>.</remarks>
internal static class WaitWithRetryFixture
{
    /// <summary>
    /// Creates a new instance of the <see cref="WaitWithRetry"/> class along with a mock logger.
    /// </summary>
    /// <returns>A tuple containing the created <see cref="WaitWithRetry"/> instance and a mock <see cref="ILogger{T}"/>. The
    /// mock logger can be used for testing or verifying logging behavior.</returns>
    public static (WaitWithRetry SUT, Mock<ILogger<WaitWithRetry>> Log) Create()
    {
        var mockLog = new Mock<ILogger<WaitWithRetry>>();
        return (new WaitWithRetry(mockLog.Object), mockLog);
    }
}
