// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DistributedIds.SnowflakeIds;

namespace XiHan.Framework.DistributedIds.Tests.SnowflakeIds;

/// <summary>
/// 雪花唯一标识类型枚举的测试
/// </summary>
/// <remarks>
/// 该枚举显式赋值 1/2，并且配置文档里直接以「1-雪花漂移，2-传统雪花」的数字口径对外描述，
/// 属于对外协议的一部分，必须锁死。
/// </remarks>
public class SnowflakeIdTypesTests
{
    /// <summary>
    /// 枚举底层数值被配置口径依赖，不允许漂移
    /// </summary>
    [Theory]
    [InlineData(SnowflakeIdTypes.SnowFlakeMethod, 1)]
    [InlineData(SnowflakeIdTypes.ClassicSnowFlakeMethod, 2)]
    public void UnderlyingValue_IsStable(SnowflakeIdTypes value, int expected)
    {
        Assert.Equal(expected, (int)value);
    }

    /// <summary>
    /// 枚举没有 0 值成员，避免默认值被误当成合法算法
    /// </summary>
    [Fact]
    public void Members_HaveNoZeroValue()
    {
        var values = Enum.GetValues<SnowflakeIdTypes>();

        Assert.Equal(2, values.Length);
        Assert.DoesNotContain(values, value => (int)value == 0);
    }
}
