// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Primitives.Tests;

/// <summary>
/// 框架级原语的行为：<see cref="FrameworkException"/>、<see cref="FrameworkInfo"/>、<see cref="ApiLevelAttribute"/> 与管道阶段名互转。
/// </summary>
public sealed class FrameworkTests
{
    [Fact]
    public void FrameworkException_CarriesCodeAndConvertsToError()
    {
        var inner = new FrameworkException("PERSISTENCE_UNAVAILABLE_001", "Database unavailable.");
        var outer = new FrameworkException("ORDERS_UNEXPECTED_001", "Failed to create order.", inner);

        var error = outer.ToError();

        Assert.Equal("ORDERS_UNEXPECTED_001", error.Code);
        Assert.Equal("PERSISTENCE_UNAVAILABLE_001", error.Inner!.Code);
        Assert.Throws<ArgumentException>(() => new FrameworkException("bad", "message"));
    }

    [Fact]
    public void FrameworkInfo_ExposesIdentity()
    {
        Assert.Equal("XiHan Framework", FrameworkInfo.Name);
        Assert.Equal("1", FrameworkInfo.SpecVersion);
        Assert.StartsWith("5.", FrameworkInfo.Version, StringComparison.Ordinal);
        Assert.Equal("github.com", FrameworkInfo.Repository.Host);
    }

    [Fact]
    public void ApiLevel_IsAppliedToPublicTypes()
    {
        var attribute = Assert.Single(typeof(Result).GetCustomAttributes(typeof(ApiLevelAttribute), false));
        var apiLevel = Assert.IsType<ApiLevelAttribute>(attribute);

        Assert.Equal(Stability.Stable, apiLevel.Stability);
        Assert.Equal("5.0", apiLevel.Since);
    }

    [Fact]
    public void PipelineStage_RoundTripsManifestNames()
    {
        foreach (var stage in PipelineStageExtensions.All)
        {
            Assert.True(PipelineStageExtensions.TryParse(stage.ToManifestName(), out var parsed));
            Assert.Equal(stage, parsed);
        }

        Assert.False(PipelineStageExtensions.TryParse("unknown", out _));
        Assert.Equal(11, PipelineStageExtensions.All.Count);
        Assert.Equal(PipelineStage.Ingress, PipelineStageExtensions.All[0]);
        Assert.Equal(PipelineStage.Egress, PipelineStageExtensions.All[^1]);
    }
}
