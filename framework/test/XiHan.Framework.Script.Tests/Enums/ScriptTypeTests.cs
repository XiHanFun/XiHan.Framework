// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Script.Enums;

namespace XiHan.Framework.Script.Tests.Enums;

/// <summary>
/// 脚本类型枚举测试
/// </summary>
/// <remarks>
/// 该枚举既参与缓存键的哈希计算，也决定代码包装方式，
/// 数值一旦漂移，历史缓存键会指向另一种包装策略，因此按序号锁死。
/// 同时锁死默认值：<c>default(ScriptType)</c> 必须落在 <c>Statement</c> 上，这是选项类不显式赋值时的兜底语义。
/// </remarks>
public class ScriptTypeTests
{
    /// <summary>
    /// 枚举成员的序号不允许漂移
    /// </summary>
    [Theory]
    [InlineData(ScriptType.Statement, 0)]
    [InlineData(ScriptType.Expression, 1)]
    [InlineData(ScriptType.Class, 2)]
    [InlineData(ScriptType.Method, 3)]
    [InlineData(ScriptType.Program, 4)]
    public void Members_KeepStableNumericValues(ScriptType scriptType, int expected)
    {
        Assert.Equal(expected, (int)scriptType);
    }

    /// <summary>
    /// 默认值落在语句脚本上
    /// </summary>
    [Fact]
    public void Default_IsStatement()
    {
        Assert.Equal(ScriptType.Statement, default(ScriptType));
    }

    /// <summary>
    /// 枚举成员数量固定为五种
    /// </summary>
    [Fact]
    public void Members_CountIsFive()
    {
        Assert.Equal(5, Enum.GetValues<ScriptType>().Length);
    }
}
