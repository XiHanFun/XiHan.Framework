// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Scheduling.Abstractions.Leadership;
using XiHan.Framework.Scheduling.Conformance;

namespace XiHan.Framework.Scheduling.InMemory.Tests;

/// <summary>
/// 让 <see cref="InMemoryLeaderElection"/> 跑完整套 <see cref="LeaderElectionConformance"/> 用例。
/// </summary>
public sealed class InMemoryLeaderElectionConformanceTests : LeaderElectionConformance
{
    /// <inheritdoc />
    protected override ILeaderElection CreateElection() => new InMemoryLeaderElection(TimeProvider.System);
}
