// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Upgrade.Enums;
using XiHan.Framework.Upgrade.Models;

namespace XiHan.Framework.Upgrade.Tests;

/// <summary>
/// 升级版本快照测试
/// </summary>
/// <remarks>
/// 快照是给客户端做「是否强制升级」判断的对外契约：IsCompatible 必须默认 true，
/// 否则任何未填字段的响应都会把客户端误判成不兼容。
/// </remarks>
public class UpgradeVersionSnapshotTests
{
    /// <summary>
    /// 默认快照是「兼容、不需要升级、无升级节点」
    /// </summary>
    [Fact]
    public void Defaults_AreCompatibleAndIdle()
    {
        var snapshot = new UpgradeVersionSnapshot();

        Assert.Equal(string.Empty, snapshot.CurrentAppVersion);
        Assert.Equal(string.Empty, snapshot.CurrentDbVersion);
        Assert.Equal(string.Empty, snapshot.MinSupportVersion);
        Assert.Equal(string.Empty, snapshot.RecordedAppVersion);
        Assert.False(snapshot.NeedUpgrade);
        Assert.False(snapshot.ForceUpgrade);
        Assert.True(snapshot.IsCompatible);
        Assert.Equal(UpgradeStatus.Normal, snapshot.Status);
        Assert.False(snapshot.IsUpgrading);
        Assert.Null(snapshot.UpgradeNode);
        Assert.Null(snapshot.UpgradeStartTime);
    }

    /// <summary>
    /// 相等性按值比较
    /// </summary>
    [Fact]
    public void Equality_IsValueBased()
    {
        var time = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var left = new UpgradeVersionSnapshot
        {
            CurrentAppVersion = "1.1.0",
            CurrentDbVersion = "1.0.0",
            MinSupportVersion = "1.0.0",
            RecordedAppVersion = "1.0.0",
            NeedUpgrade = true,
            Status = UpgradeStatus.Upgrading,
            IsUpgrading = true,
            UpgradeNode = "node-a",
            UpgradeStartTime = time
        };
        var right = left with { };

        Assert.Equal(left, right);
        Assert.NotEqual(left, left with { UpgradeNode = "node-b" });
    }

    /// <summary>
    /// JSON 往返保持字段名、可空字段与时间值
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesMembersAndNullability()
    {
        var snapshot = new UpgradeVersionSnapshot
        {
            CurrentAppVersion = "1.1.0",
            CurrentDbVersion = "1.0.0",
            MinSupportVersion = "1.0.0",
            RecordedAppVersion = "1.0.0",
            NeedUpgrade = true,
            ForceUpgrade = true,
            IsCompatible = false,
            Status = UpgradeStatus.Failed,
            IsUpgrading = false,
            UpgradeNode = null,
            UpgradeStartTime = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)
        };

        var json = JsonSerializer.Serialize(snapshot);
        var restored = JsonSerializer.Deserialize<UpgradeVersionSnapshot>(json);

        Assert.Contains("\"CurrentAppVersion\"", json);
        Assert.Contains("\"IsCompatible\":false", json);
        Assert.Contains("\"UpgradeNode\":null", json);
        Assert.Equal(snapshot, restored);
    }
}
