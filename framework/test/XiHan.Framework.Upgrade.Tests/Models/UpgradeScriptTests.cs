// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text.Json;
using XiHan.Framework.Upgrade.Models;

namespace XiHan.Framework.Upgrade.Tests.Models;

/// <summary>
/// 升级脚本信息测试
/// </summary>
/// <remarks>
/// 升级引擎会按 Version + ScriptName 去重（迁移历史查重）并按二者排序，
/// 因此这个 record 的值相等语义与成员顺序都是被依赖的契约。
/// </remarks>
public class UpgradeScriptTests
{
    /// <summary>
    /// 位置参数按声明顺序映射到三个成员
    /// </summary>
    [Fact]
    public void Constructor_MapsPositionalParametersInOrder()
    {
        var script = new UpgradeScript("1.0.0", "01_init.sql", "root/1.0.0/01_init.sql");

        Assert.Equal("1.0.0", script.Version);
        Assert.Equal("01_init.sql", script.ScriptName);
        Assert.Equal("root/1.0.0/01_init.sql", script.ScriptPath);
    }

    /// <summary>
    /// 相等性按值比较，脚本名不同即视为不同脚本
    /// </summary>
    [Fact]
    public void Equality_IsValueBased()
    {
        var left = new UpgradeScript("1.0.0", "a.sql", "root/a.sql");
        var right = new UpgradeScript("1.0.0", "a.sql", "root/a.sql");
        var other = left with { ScriptName = "b.sql" };

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
        Assert.NotEqual(left, other);
    }

    /// <summary>
    /// 支持解构，便于在编排代码里直接取三段信息
    /// </summary>
    [Fact]
    public void Deconstruct_ReturnsAllMembers()
    {
        var (version, scriptName, scriptPath) = new UpgradeScript("2.0.0", "x.sql", "root/x.sql");

        Assert.Equal("2.0.0", version);
        Assert.Equal("x.sql", scriptName);
        Assert.Equal("root/x.sql", scriptPath);
    }

    /// <summary>
    /// JSON 往返保持字段名与取值
    /// </summary>
    [Fact]
    public void JsonRoundTrip_PreservesMembers()
    {
        var script = new UpgradeScript("1.0.0", "a.sql", "root/a.sql");

        var json = JsonSerializer.Serialize(script);
        var restored = JsonSerializer.Deserialize<UpgradeScript>(json);

        Assert.Contains("\"Version\"", json);
        Assert.Contains("\"ScriptName\"", json);
        Assert.Contains("\"ScriptPath\"", json);
        Assert.Equal(script, restored);
    }
}
