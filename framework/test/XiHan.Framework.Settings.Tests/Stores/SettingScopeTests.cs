// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings.Tests.Stores;

/// <summary>
/// 设置作用域测试
/// </summary>
/// <remarks>
/// 作用域会随设置变更事件对外广播，也会被持久化到配置里，
/// 因此成员的数值与名称都属于对外契约，不能因为"补一个成员"就整体位移。
/// </remarks>
public class SettingScopeTests
{
    /// <summary>
    /// 各成员的数值保持稳定
    /// </summary>
    /// <param name="scope">作用域</param>
    /// <param name="expected">期望数值</param>
    [Theory]
    [InlineData(SettingScope.Application, 0)]
    [InlineData(SettingScope.Tenant, 1)]
    [InlineData(SettingScope.User, 2)]
    [InlineData(SettingScope.Session, 3)]
    public void SettingScope_NumericValue_IsStable(SettingScope scope, int expected)
    {
        Assert.Equal(expected, (int)scope);
    }

    /// <summary>
    /// 成员名称与顺序保持稳定
    /// </summary>
    [Fact]
    public void SettingScope_MemberNames_AreStable()
    {
        Assert.Equal(new[] { "Application", "Tenant", "User", "Session" }, Enum.GetNames<SettingScope>());
    }

    /// <summary>
    /// 恰好四个成员，新增成员必须显式过评审
    /// </summary>
    [Fact]
    public void SettingScope_HasExactlyFourMembers()
    {
        Assert.Equal(4, Enum.GetValues<SettingScope>().Length);
    }

    /// <summary>
    /// 默认值是应用级——设置管理器的可选参数默认值依赖这一点
    /// </summary>
    [Fact]
    public void SettingScope_DefaultValue_IsApplication()
    {
        Assert.Equal(SettingScope.Application, default(SettingScope));
    }

    /// <summary>
    /// 按名称可以往返解析
    /// </summary>
    /// <param name="scope">作用域</param>
    [Theory]
    [InlineData(SettingScope.Application)]
    [InlineData(SettingScope.Tenant)]
    [InlineData(SettingScope.User)]
    [InlineData(SettingScope.Session)]
    public void SettingScope_RoundTripsThroughItsName(SettingScope scope)
    {
        Assert.Equal(scope, Enum.Parse<SettingScope>(scope.ToString()));
    }
}
