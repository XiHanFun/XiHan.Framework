// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Tests;

public sealed class LifecycleStateTests
{
    private sealed record TransitionVectors(string[][] Allowed);

    [Fact]
    public void CanTransitionTo_MatchesConformanceVectorsExactly()
    {
        var vectors = ConformanceVectors.Load<TransitionVectors>("lifecycle-transitions.json");
        var allowed = vectors.Allowed
            .Select(static pair => (Enum.Parse<LifecycleState>(pair[0]), Enum.Parse<LifecycleState>(pair[1])))
            .ToHashSet();

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
