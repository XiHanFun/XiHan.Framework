// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection;
using XiHan.Framework.Core.Exceptions;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 暴露键值服务特性测试
/// </summary>
/// <remarks>
/// 键值暴露把服务键与服务类型打包成服务标识，服务键为空时必须显式报错而不是退化成普通暴露，
/// 否则会静默产生一条无法按键解析的注册。
/// </remarks>
public class ExposeKeyedServiceAttributeTests
{
    /// <summary>
    /// 构造后服务标识携带声明的键与类型
    /// </summary>
    [Fact]
    public void Constructor_WithServiceKey_BuildsIdentifier()
    {
        var attribute = new ExposeKeyedServiceAttribute<IEksContract>("primary");

        Assert.Equal("primary", attribute.ServiceIdentifier.ServiceKey);
        Assert.Equal(typeof(IEksContract), attribute.ServiceIdentifier.ServiceType);
    }

    /// <summary>
    /// 服务键为空时抛出框架异常
    /// </summary>
    [Fact]
    public void Constructor_WhenServiceKeyNull_ThrowsXiHanException()
    {
        var exception = Assert.Throws<XiHanException>(() =>
        {
            _ = new ExposeKeyedServiceAttribute<IEksContract>(null!);
        });

        Assert.Contains(nameof(ExposeServicesAttribute), exception.Message);
    }

    /// <summary>
    /// 获取暴露类型时忽略目标类型只回吐自身标识
    /// </summary>
    [Fact]
    public void GetExposedServiceTypes_ReturnsDeclaredIdentifier()
    {
        var attribute = new ExposeKeyedServiceAttribute<IEksContract>(EksKeys.Primary);

        var identifier = Assert.Single(attribute.GetExposedServiceTypes(typeof(EksService)));

        Assert.Equal(attribute.ServiceIdentifier, identifier);
    }

    /// <summary>
    /// 非字符串服务键同样被原样保留
    /// </summary>
    [Fact]
    public void Constructor_WithEnumServiceKey_KeepsOriginalKeyObject()
    {
        var attribute = new ExposeKeyedServiceAttribute<IEksContract>(EksKind.Secondary);

        Assert.Equal(EksKind.Secondary, attribute.ServiceIdentifier.ServiceKey);
    }
}

/// <summary>
/// 键值暴露测试用契约
/// </summary>
internal interface IEksContract;

/// <summary>
/// 键值暴露测试用实现
/// </summary>
internal class EksService : IEksContract;

/// <summary>
/// 键值暴露测试用服务键常量
/// </summary>
internal static class EksKeys
{
    /// <summary>
    /// 主键值
    /// </summary>
    public const string Primary = "primary";
}

/// <summary>
/// 键值暴露测试用枚举服务键
/// </summary>
internal enum EksKind
{
    /// <summary>
    /// 主
    /// </summary>
    Primary = 0,

    /// <summary>
    /// 次
    /// </summary>
    Secondary = 1
}
