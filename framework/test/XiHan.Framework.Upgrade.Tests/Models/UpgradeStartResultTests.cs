// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Upgrade.Enums;
using XiHan.Framework.Upgrade.Models;

namespace XiHan.Framework.Upgrade.Tests.Models;

/// <summary>
/// 升级启动结果测试
/// </summary>
/// <remarks>
/// 该结果既是协调器/引擎的返回值，也会直接回给调用方，
/// 因此默认值（Message 非 null）与 JSON 形状都要固定。
/// </remarks>
public class UpgradeStartResultTests
{
    /// <summary>
    /// 默认实例是「未启动 + 正常状态 + 空消息」
    /// </summary>
    [Fact]
    public void Defaults_AreNotStartedWithEmptyMessage()
    {
        var result = new UpgradeStartResult();

        Assert.False(result.Started);
        Assert.Equal(UpgradeStatus.Normal, result.Status);
        Assert.Equal(string.Empty, result.Message);
    }

    /// <summary>
    /// 相等性按值比较，且 with 表达式只改动指定成员
    /// </summary>
    [Fact]
    public void Equality_IsValueBasedAndWithExpressionCopiesRest()
    {
        var left = new UpgradeStartResult { Started = true, Status = UpgradeStatus.Completed, Message = "升级完成" };
        var right = new UpgradeStartResult { Started = true, Status = UpgradeStatus.Completed, Message = "升级完成" };
        var changed = left with { Status = UpgradeStatus.Failed };

        Assert.Equal(left, right);
        Assert.NotEqual(left, changed);
        Assert.True(changed.Started);
        Assert.Equal("升级完成", changed.Message);
    }

    /// <summary>
    /// JSON 往返保持成员，状态按数值序列化
    /// </summary>
    [Fact]
    public void JsonRoundTrip_SerializesStatusAsNumber()
    {
        var result = new UpgradeStartResult { Started = true, Status = UpgradeStatus.Upgrading, Message = "upgrading" };

        var json = JsonSerializer.Serialize(result);
        var restored = JsonSerializer.Deserialize<UpgradeStartResult>(json);

        Assert.Contains("\"Status\":1", json);
        Assert.Equal(result, restored);
    }
}
