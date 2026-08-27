// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Script.Enums;

namespace XiHan.Framework.Script.Tests.Enums;

/// <summary>
/// 断点命中条件枚举测试
/// </summary>
/// <remarks>
/// 断点配置会随调试会话被序列化保存，序号漂移会让历史断点变成另一种命中策略。
/// </remarks>
public class HitCountConditionTests
{
    /// <summary>
    /// 枚举成员的序号不允许漂移
    /// </summary>
    [Theory]
    [InlineData(HitCountCondition.Always, 0)]
    [InlineData(HitCountCondition.Equal, 1)]
    [InlineData(HitCountCondition.GreaterOrEqual, 2)]
    [InlineData(HitCountCondition.Multiple, 3)]
    public void Members_KeepStableNumericValues(HitCountCondition condition, int expected)
    {
        Assert.Equal(expected, (int)condition);
    }

    /// <summary>
    /// 默认值落在总是命中上，与断点默认配置一致
    /// </summary>
    [Fact]
    public void Default_IsAlways()
    {
        Assert.Equal(HitCountCondition.Always, default(HitCountCondition));
    }
}
