// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Tasks.BackgroundServices;

namespace XiHan.Framework.Tasks.Tests.BackgroundServices;

/// <summary>
/// 配置变更事件参数测试
/// </summary>
/// <remarks>
/// 事件参数里的 PropertyName 是订阅方唯一的分支依据（判断改的是并发数还是开关），
/// 新旧值又要允许为 null（布尔与整数装箱后可能为空），所以三个属性的只读性与可空性都要锁住。
/// </remarks>
public class ConfigChangedEventArgsTests
{
    /// <summary>
    /// 构造时原样保存三个要素
    /// </summary>
    [Fact]
    public void Constructor_KeepsPropertyNameAndValues()
    {
        var args = new ConfigChangedEventArgs("MaxConcurrentTasks", 5, 10);

        Assert.Equal("MaxConcurrentTasks", args.PropertyName);
        Assert.Equal(5, args.OldValue);
        Assert.Equal(10, args.NewValue);
    }

    /// <summary>
    /// 新旧值允许为 null
    /// </summary>
    [Fact]
    public void Constructor_AllowsNullValues()
    {
        var args = new ConfigChangedEventArgs("ApplicationName", null, null);

        Assert.Equal("ApplicationName", args.PropertyName);
        Assert.Null(args.OldValue);
        Assert.Null(args.NewValue);
    }

    /// <summary>
    /// 布尔值同样能被承载
    /// </summary>
    [Fact]
    public void Constructor_CarriesBooleanValues()
    {
        var args = new ConfigChangedEventArgs("IsTaskProcessingEnabled", true, false);

        Assert.True(args.OldValue is bool oldValue && oldValue);
        Assert.True(args.NewValue is bool newValue && !newValue);
    }

    /// <summary>
    /// 继承自 EventArgs，可用于标准事件模式
    /// </summary>
    [Fact]
    public void Type_DerivesFromEventArgs()
    {
        Assert.IsAssignableFrom<EventArgs>(new ConfigChangedEventArgs("x", null, null));
    }
}
