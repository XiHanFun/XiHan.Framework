// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Settings.Definitions;
using XiHan.Framework.Settings.Providers;
using XiHan.Framework.Settings.Stores;

namespace XiHan.Framework.Settings.Tests.Providers;

/// <summary>
/// 设置值提供者上下文测试
/// </summary>
/// <remarks>
/// 上下文是只读载体，三个属性都没有 setter，构造时必须一次性落位并原样透传。
/// </remarks>
public class SettingValueProviderContextTests
{
    /// <summary>
    /// 构造器逐一落到三个只读属性
    /// </summary>
    [Fact]
    public void Ctor_AssignsAllProperties()
    {
        var setting = new SettingDefinition("Foo", "bar");
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var context = new SettingValueProviderContext(setting, SettingScope.User, serviceProvider);

        Assert.Same(setting, context.Setting);
        Assert.Equal(SettingScope.User, context.Scope);
        Assert.Same(serviceProvider, context.ServiceProvider);
    }

    /// <summary>
    /// 作用域原样透传，不做归一化
    /// </summary>
    /// <param name="scope">作用域</param>
    [Theory]
    [InlineData(SettingScope.Application)]
    [InlineData(SettingScope.Tenant)]
    [InlineData(SettingScope.User)]
    [InlineData(SettingScope.Session)]
    public void Ctor_KeepsScopeAsGiven(SettingScope scope)
    {
        using var serviceProvider = new ServiceCollection().BuildServiceProvider();

        var context = new SettingValueProviderContext(new SettingDefinition("Foo"), scope, serviceProvider);

        Assert.Equal(scope, context.Scope);
    }
}
