// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Script.Enums;

namespace XiHan.Framework.Script.Tests.Enums;

/// <summary>
/// 调试级别枚举测试
/// </summary>
/// <remarks>
/// 级别语义按"输出量递增"排列，生产预设取 <c>Error</c>、详细预设取 <c>Verbose</c>，
/// 顺序即过滤阈值，因此序号与相对次序都要锁死。
/// </remarks>
public class DebugLevelTests
{
    /// <summary>
    /// 枚举成员的序号不允许漂移
    /// </summary>
    [Theory]
    [InlineData(DebugLevel.None, 0)]
    [InlineData(DebugLevel.Error, 1)]
    [InlineData(DebugLevel.Warning, 2)]
    [InlineData(DebugLevel.Information, 3)]
    [InlineData(DebugLevel.Verbose, 4)]
    public void Members_KeepStableNumericValues(DebugLevel level, int expected)
    {
        Assert.Equal(expected, (int)level);
    }

    /// <summary>
    /// 级别按输出量从少到多递增
    /// </summary>
    [Fact]
    public void Members_AreOrderedByVerbosity()
    {
        Assert.True(DebugLevel.None < DebugLevel.Error);
        Assert.True(DebugLevel.Error < DebugLevel.Warning);
        Assert.True(DebugLevel.Warning < DebugLevel.Information);
        Assert.True(DebugLevel.Information < DebugLevel.Verbose);
    }

    /// <summary>
    /// 默认值落在无输出上
    /// </summary>
    [Fact]
    public void Default_IsNone()
    {
        Assert.Equal(DebugLevel.None, default(DebugLevel));
    }
}
