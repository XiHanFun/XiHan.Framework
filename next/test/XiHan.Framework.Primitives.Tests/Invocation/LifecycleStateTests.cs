// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Tests.Invocation;

/// <summary>
/// <see cref="LifecycleState"/> 迁移表与派生判断的行为，以一致性向量为准。
/// </summary>
public sealed class LifecycleStateTests
{
    /// <summary>
    /// <c>lifecycle-transitions.json</c> 向量：允许的迁移，每项为 <c>[from, to]</c> 的状态名。
    /// </summary>
    private sealed record TransitionVectors(string[][] Allowed);

    [Fact]
    public void CanTransitionTo_MatchesConformanceVectorsExactly()
    {
        var vectors = ConformanceVectors.Load<TransitionVectors>("lifecycle-transitions.json");
        var allowed = vectors.Allowed
            .Select(static pair => (Enum.Parse<LifecycleState>(pair[0]), Enum.Parse<LifecycleState>(pair[1])))
            .ToHashSet();

        // 全量枚举笛卡尔积：向量之外的任何迁移都必须被拒绝，防止实现比规范宽松。
        var states = Enum.GetValues<LifecycleState>();
        foreach (var from in states)
        {
            foreach (var to in states)
            {
                Assert.Equal(allowed.Contains((from, to)), from.CanTransitionTo(to));
            }
        }
    }

    [Fact]
    public void IsServing_And_IsTerminal()
    {
        Assert.True(LifecycleState.Ready.IsServing());
        Assert.True(LifecycleState.Degraded.IsServing());
        Assert.False(LifecycleState.Started.IsServing());
        Assert.True(LifecycleState.Stopped.IsTerminal());
        Assert.False(LifecycleState.Failed.IsTerminal());
    }
}
