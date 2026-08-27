// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DistributedIds.Guids;

namespace XiHan.Framework.DistributedIds.Tests.Guids;

/// <summary>
/// 顺序 GUID 选项的测试
/// </summary>
/// <remarks>
/// 该选项只有一个可空的类型开关，重点在于「没配置时回落到哪个类型」——
/// 这条回落规则决定了默认写库的 GUID 形态，必须锁死。
/// </remarks>
public class SequentialGuidOptionsTests
{
    /// <summary>
    /// 配置节名称被 appsettings 直接引用，不允许漂移
    /// </summary>
    [Fact]
    public void SectionName_IsStable()
    {
        Assert.Equal("XiHan:DistributedIds:SequentialGuid", SequentialGuidOptions.SectionName);
    }

    /// <summary>
    /// 未显式配置时类型开关保持为空
    /// </summary>
    [Fact]
    public void DefaultSequentialGuidType_WhenNotConfigured_IsNull()
    {
        var options = new SequentialGuidOptions();

        Assert.Null(options.DefaultSequentialGuidType);
    }

    /// <summary>
    /// 未显式配置时回落到末尾形式（对 SQL Server 聚集索引最友好的形态）
    /// </summary>
    [Fact]
    public void GetDefaultSequentialGuidType_WhenNotConfigured_FallsBackToAtEnd()
    {
        var options = new SequentialGuidOptions();

        Assert.Equal(SequentialGuidType.SequentialAtEnd, options.GetDefaultSequentialGuidType());
    }

    /// <summary>
    /// 显式配置后按配置返回
    /// </summary>
    [Theory]
    [InlineData(SequentialGuidType.SequentialAsString)]
    [InlineData(SequentialGuidType.SequentialAsBinary)]
    [InlineData(SequentialGuidType.SequentialAtEnd)]
    public void GetDefaultSequentialGuidType_WhenConfigured_ReturnsConfiguredValue(SequentialGuidType guidType)
    {
        var options = new SequentialGuidOptions
        {
            DefaultSequentialGuidType = guidType
        };

        Assert.Equal(guidType, options.GetDefaultSequentialGuidType());
    }

    /// <summary>
    /// 三个工厂方法各自落到对应类型
    /// </summary>
    [Fact]
    public void Factories_ProduceMatchingGuidType()
    {
        Assert.Equal(SequentialGuidType.SequentialAsString, SequentialGuidOptions.AsString().DefaultSequentialGuidType);
        Assert.Equal(SequentialGuidType.SequentialAsBinary, SequentialGuidOptions.AsBinary().DefaultSequentialGuidType);
        Assert.Equal(SequentialGuidType.SequentialAtEnd, SequentialGuidOptions.AtEnd().DefaultSequentialGuidType);
    }

    /// <summary>
    /// 默认工厂等价于末尾形式
    /// </summary>
    [Fact]
    public void Default_IsSameAsAtEnd()
    {
        Assert.Equal(SequentialGuidType.SequentialAtEnd, SequentialGuidOptions.Default().GetDefaultSequentialGuidType());
    }
}
