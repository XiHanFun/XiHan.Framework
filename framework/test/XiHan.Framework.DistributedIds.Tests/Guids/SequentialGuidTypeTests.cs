// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.DistributedIds.Guids;

namespace XiHan.Framework.DistributedIds.Tests.Guids;

/// <summary>
/// 顺序 Guid 生成类型枚举的测试
/// </summary>
/// <remarks>
/// 该枚举决定时间戳落在 GUID 的哪一段，一旦数值漂移，历史 GUID 的时间反解会整体错位。
/// </remarks>
public class SequentialGuidTypeTests
{
    /// <summary>
    /// 枚举底层数值被配置绑定依赖，不允许漂移
    /// </summary>
    [Theory]
    [InlineData(SequentialGuidType.SequentialAsString, 0)]
    [InlineData(SequentialGuidType.SequentialAsBinary, 1)]
    [InlineData(SequentialGuidType.SequentialAtEnd, 2)]
    public void UnderlyingValue_IsStable(SequentialGuidType value, int expected)
    {
        Assert.Equal(expected, (int)value);
    }

    /// <summary>
    /// 枚举恰好三个成员
    /// </summary>
    [Fact]
    public void Members_AreExactlyThree()
    {
        Assert.Equal(3, Enum.GetValues<SequentialGuidType>().Length);
    }
}
