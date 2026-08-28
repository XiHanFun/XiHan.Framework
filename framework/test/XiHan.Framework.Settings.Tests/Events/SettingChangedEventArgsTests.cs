// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Settings.Events;
using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings.Tests.Events;

/// <summary>
/// 设置变更事件参数测试
/// </summary>
/// <remarks>
/// 事件参数是设置写入后对外广播的唯一载体，三个属性都是只读的，构造时必须完整落位。
/// </remarks>
public class SettingChangedEventArgsTests
{
    /// <summary>
    /// 构造器逐一落到三个只读属性
    /// </summary>
    [Fact]
    public void Ctor_AssignsAllProperties()
    {
        var args = new SettingChangedEventArgs("Foo", SettingScope.Tenant, "bar");

        Assert.Equal("Foo", args.Name);
        Assert.Equal(SettingScope.Tenant, args.Scope);
        Assert.Equal("bar", args.NewValue);
    }

    /// <summary>
    /// 新值允许为 null，用来表达"该设置被清除"
    /// </summary>
    [Fact]
    public void Ctor_AllowsNullNewValue()
    {
        var args = new SettingChangedEventArgs("Foo", SettingScope.Application, null);

        Assert.Null(args.NewValue);
    }

    /// <summary>
    /// 作用域原样透传，不做任何归一化
    /// </summary>
    /// <param name="scope">作用域</param>
    [Theory]
    [InlineData(SettingScope.Application)]
    [InlineData(SettingScope.Tenant)]
    [InlineData(SettingScope.User)]
    [InlineData(SettingScope.Session)]
    public void Ctor_KeepsScopeAsGiven(SettingScope scope)
    {
        var args = new SettingChangedEventArgs("Foo", scope, "bar");

        Assert.Equal(scope, args.Scope);
    }

    /// <summary>
    /// 事件参数继承自标准事件参数基类，可直接挂到 EventHandler 上
    /// </summary>
    [Fact]
    public void SettingChangedEventArgs_DerivesFromEventArgs()
    {
        var args = new SettingChangedEventArgs("Foo", SettingScope.Application, "bar");

        Assert.IsAssignableFrom<EventArgs>(args);
    }
}
