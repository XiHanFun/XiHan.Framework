// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Upgrade.Models;

namespace XiHan.Framework.Upgrade.Tests.Models;

/// <summary>
/// 升级迁移历史测试
/// </summary>
/// <remarks>
/// 迁移历史是脚本幂等执行的唯一依据：Success 默认必须为 false，
/// 这样「先落一条失败记录、成功后再改成 true」的写法才不会把未执行的脚本记成已执行。
/// </remarks>
public class UpgradeMigrationHistoryTests
{
    /// <summary>
    /// 默认历史是「未成功、无租户、无节点、无错误」
    /// </summary>
    [Fact]
    public void Defaults_AreNotSuccessful()
    {
        var history = new UpgradeMigrationHistory();

        Assert.Null(history.TenantId);
        Assert.Equal(string.Empty, history.Version);
        Assert.Equal(string.Empty, history.ScriptName);
        Assert.Equal(default(DateTimeOffset), history.ExecutedTime);
        Assert.False(history.Success);
        Assert.Null(history.NodeName);
        Assert.Null(history.ErrorMessage);
    }

    /// <summary>
    /// JSON 往返保持全部成员，包含可空租户与错误信息
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesMembers()
    {
        var history = new UpgradeMigrationHistory
        {
            TenantId = 5,
            Version = "1.0.0",
            ScriptName = "01_init.sql",
            ExecutedTime = new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.Zero),
            Success = false,
            NodeName = "node-a",
            ErrorMessage = "boom"
        };

        var json = JsonSerializer.Serialize(history);
        var restored = JsonSerializer.Deserialize<UpgradeMigrationHistory>(json);

        Assert.NotNull(restored);
        Assert.NotNull(restored!.TenantId);
        Assert.Equal(5L, restored.TenantId!.Value);
        Assert.Equal("1.0.0", restored.Version);
        Assert.Equal("01_init.sql", restored.ScriptName);
        Assert.Equal(history.ExecutedTime, restored.ExecutedTime);
        Assert.False(restored.Success);
        Assert.Equal("node-a", restored.NodeName);
        Assert.Equal("boom", restored.ErrorMessage);
    }
}
