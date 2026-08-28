// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Upgrade.Models;

namespace XiHan.Framework.Upgrade.Tests.Models;

/// <summary>
/// 升级版本状态测试
/// </summary>
/// <remarks>
/// 与快照不同，版本状态是可变实体：引擎会把同一个实例在存储与迁移流程之间来回传递并原地改写，
/// 因此它必须保持引用相等语义，且数据库版本默认从 0.0.0 起跑（新库要能跑全部脚本）。
/// </remarks>
public class UpgradeVersionStateTests
{
    /// <summary>
    /// 默认状态：未升级中、数据库版本 0.0.0、应用版本为空串
    /// </summary>
    [Fact]
    public void Defaults_StartFromZeroDbVersion()
    {
        var state = new UpgradeVersionState();

        Assert.Equal(0, state.Id);
        Assert.Null(state.TenantId);
        Assert.Equal(string.Empty, state.AppVersion);
        Assert.Equal("0.0.0", state.DbVersion);
        Assert.Null(state.MinSupportVersion);
        Assert.False(state.IsUpgrading);
        Assert.Null(state.UpgradeNode);
        Assert.Null(state.UpgradeStartTime);
    }

    /// <summary>
    /// 全部成员可写，供存储层原地回填
    /// </summary>
    [Fact]
    public void Members_AreMutable()
    {
        var time = DateTimeOffset.UtcNow;
        var state = new UpgradeVersionState
        {
            Id = 7,
            TenantId = 42,
            AppVersion = "1.1.0",
            DbVersion = "1.0.0",
            MinSupportVersion = "0.9.0",
            IsUpgrading = true,
            UpgradeNode = "node-a",
            UpgradeStartTime = time
        };

        Assert.Equal(7, state.Id);
        Assert.NotNull(state.TenantId);
        Assert.Equal(42L, state.TenantId.Value);
        Assert.Equal("1.1.0", state.AppVersion);
        Assert.Equal("1.0.0", state.DbVersion);
        Assert.Equal("0.9.0", state.MinSupportVersion);
        Assert.True(state.IsUpgrading);
        Assert.Equal("node-a", state.UpgradeNode);
        Assert.Equal(time, state.UpgradeStartTime);
    }

    /// <summary>
    /// 相等性是引用相等，两个字段相同的状态不是同一条记录
    /// </summary>
    [Fact]
    public void Equality_IsReferenceBased()
    {
        var left = new UpgradeVersionState { Id = 1, AppVersion = "1.0.0" };
        var right = new UpgradeVersionState { Id = 1, AppVersion = "1.0.0" };

        Assert.NotEqual(left, right);
        Assert.NotSame(left, right);
        Assert.Equal(left, left);
    }
}
