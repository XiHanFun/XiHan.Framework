// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.DistributedIds.Tests;

/// <summary>
/// 时间戳类型枚举的测试
/// </summary>
/// <remarks>
/// 该枚举会被写进 <c>XiHan:DistributedIds:*</c> 配置节并参与 ID 位布局计算，
/// 数值一旦漂移，已生成的 ID 就无法再被正确解析，因此这里锁死底层数值。
/// </remarks>
public class TimestampTypesTests
{
    /// <summary>
    /// 枚举底层数值被协议依赖，不允许漂移
    /// </summary>
    [Theory]
    [InlineData(TimestampTypes.Seconds, 0)]
    [InlineData(TimestampTypes.Milliseconds, 1)]
    public void UnderlyingValue_IsStable(TimestampTypes value, int expected)
    {
        Assert.Equal(expected, (int)value);
    }

    /// <summary>
    /// 枚举只有秒级与毫秒级两个成员
    /// </summary>
    [Fact]
    public void Members_AreExactlySecondsAndMilliseconds()
    {
        var names = Enum.GetNames<TimestampTypes>();

        Assert.Equal(2, names.Length);
        Assert.Contains(nameof(TimestampTypes.Seconds), names);
        Assert.Contains(nameof(TimestampTypes.Milliseconds), names);
    }
}
