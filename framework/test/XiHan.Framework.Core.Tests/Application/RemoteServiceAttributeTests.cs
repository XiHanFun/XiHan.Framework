// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.Application;

namespace XiHan.Framework.Core.Tests.Application;

/// <summary>
/// 远程服务特性测试
/// </summary>
/// <remarks>
/// 四组静态判定方法都是「明确启用 / 明确禁用」的三态语义：未标注特性时两边都返回 false，
/// 调用方据此回落到自己的默认策略。用例按这条三态契约组织，未标注的情形必须两边都断言。
/// </remarks>
public class RemoteServiceAttributeTests
{
    /// <summary>
    /// 无参构造时启用远程服务并启用元数据，组名为空
    /// </summary>
    [Fact]
    public void Constructor_Default_EnablesServiceAndMetadata()
    {
        var attribute = new RemoteServiceAttribute();

        Assert.True(attribute.IsEnabled);
        Assert.True(attribute.IsMetadataEnabled);
        Assert.Null(attribute.Name);
    }

    /// <summary>
    /// 显式关闭远程服务时元数据仍默认启用
    /// </summary>
    /// <remarks>
    /// 两个开关互相独立，构造函数只接管 <c>IsEnabled</c>，
    /// 关服务不等于关元数据，这条区别直接决定接口文档是否还会暴露端点。
    /// </remarks>
    [Fact]
    public void Constructor_WhenDisabled_StillEnablesMetadata()
    {
        var attribute = new RemoteServiceAttribute(false);

        Assert.False(attribute.IsEnabled);
        Assert.True(attribute.IsMetadataEnabled);
    }

    /// <summary>
    /// 虚方法按当前开关取值，与传入的类型或方法无关
    /// </summary>
    [Fact]
    public void IsEnabledFor_ReflectsSwitchesRegardlessOfTarget()
    {
        var method = typeof(RemoteServiceSampleTarget).GetMethod(nameof(RemoteServiceSampleTarget.PlainMethod))!;
        var attribute = new RemoteServiceAttribute(false)
        {
            IsMetadataEnabled = false
        };

        Assert.False(attribute.IsEnabledFor(typeof(RemoteServiceSampleTarget)));
        Assert.False(attribute.IsEnabledFor(method));
        Assert.False(attribute.IsMetadataEnabledFor(typeof(RemoteServiceSampleTarget)));
        Assert.False(attribute.IsMetadataEnabledFor(method));
    }

    /// <summary>
    /// 类型标注为启用时明确启用为真、明确禁用为假
    /// </summary>
    [Fact]
    public void IsExplicitlyEnabledFor_WhenTypeEnabled_ReturnsTrue()
    {
        Assert.True(RemoteServiceAttribute.IsExplicitlyEnabledFor(typeof(RemoteEnabledSample)));
        Assert.False(RemoteServiceAttribute.IsExplicitlyDisabledFor(typeof(RemoteEnabledSample)));
    }

    /// <summary>
    /// 类型标注为禁用时明确禁用为真、明确启用为假
    /// </summary>
    [Fact]
    public void IsExplicitlyDisabledFor_WhenTypeDisabled_ReturnsTrue()
    {
        Assert.True(RemoteServiceAttribute.IsExplicitlyDisabledFor(typeof(RemoteDisabledSample)));
        Assert.False(RemoteServiceAttribute.IsExplicitlyEnabledFor(typeof(RemoteDisabledSample)));
    }

    /// <summary>
    /// 类型未标注时明确启用与明确禁用同时为假
    /// </summary>
    [Fact]
    public void ExplicitChecks_WhenTypeIsNotDecorated_AreBothFalse()
    {
        Assert.False(RemoteServiceAttribute.IsExplicitlyEnabledFor(typeof(RemoteUndecoratedSample)));
        Assert.False(RemoteServiceAttribute.IsExplicitlyDisabledFor(typeof(RemoteUndecoratedSample)));
        Assert.False(RemoteServiceAttribute.IsMetadataExplicitlyEnabledFor(typeof(RemoteUndecoratedSample)));
        Assert.False(RemoteServiceAttribute.IsMetadataExplicitlyDisabledFor(typeof(RemoteUndecoratedSample)));
    }

    /// <summary>
    /// 类型标注后元数据默认视为明确启用
    /// </summary>
    [Fact]
    public void IsMetadataExplicitlyEnabledFor_WhenTypeDecorated_FollowsMetadataSwitch()
    {
        Assert.True(RemoteServiceAttribute.IsMetadataExplicitlyEnabledFor(typeof(RemoteEnabledSample)));
        Assert.False(RemoteServiceAttribute.IsMetadataExplicitlyDisabledFor(typeof(RemoteEnabledSample)));

        Assert.True(RemoteServiceAttribute.IsMetadataExplicitlyDisabledFor(typeof(RemoteMetadataDisabledSample)));
        Assert.False(RemoteServiceAttribute.IsMetadataExplicitlyEnabledFor(typeof(RemoteMetadataDisabledSample)));
    }

    /// <summary>
    /// 方法级标注按方法自身的元数据开关判定
    /// </summary>
    [Fact]
    public void MetadataChecks_OnMethod_FollowMethodLevelAttribute()
    {
        var enabled = typeof(RemoteServiceSampleTarget).GetMethod(nameof(RemoteServiceSampleTarget.MetadataEnabledMethod))!;
        var disabled = typeof(RemoteServiceSampleTarget).GetMethod(nameof(RemoteServiceSampleTarget.MetadataDisabledMethod))!;
        var plain = typeof(RemoteServiceSampleTarget).GetMethod(nameof(RemoteServiceSampleTarget.PlainMethod))!;

        Assert.True(RemoteServiceAttribute.IsMetadataExplicitlyEnabledFor(enabled));
        Assert.False(RemoteServiceAttribute.IsMetadataExplicitlyDisabledFor(enabled));

        Assert.True(RemoteServiceAttribute.IsMetadataExplicitlyDisabledFor(disabled));
        Assert.False(RemoteServiceAttribute.IsMetadataExplicitlyEnabledFor(disabled));

        Assert.False(RemoteServiceAttribute.IsMetadataExplicitlyEnabledFor(plain));
        Assert.False(RemoteServiceAttribute.IsMetadataExplicitlyDisabledFor(plain));
    }

    /// <summary>
    /// 接口上的标注不会沿实现关系传给实现类
    /// </summary>
    /// <remarks>
    /// 与 <see cref="IntegrationServiceAttribute"/> 的判定语义刚好相反：
    /// 远程服务特性只看类型自身与基类，不遍历接口，这条差异属于对外契约。
    /// </remarks>
    [Fact]
    public void IsExplicitlyEnabledFor_DoesNotFollowInterfaces()
    {
        Assert.True(RemoteServiceAttribute.IsExplicitlyEnabledFor(typeof(IRemoteEnabledContract)));
        Assert.False(RemoteServiceAttribute.IsExplicitlyEnabledFor(typeof(ImplementsRemoteEnabledContract)));
    }

    /// <summary>
    /// 特性可标注在接口、类与方法上，可被继承、不允许重复标注
    /// </summary>
    [Fact]
    public void AttributeUsage_TargetsInterfaceClassAndMethod()
    {
        var usage = typeof(RemoteServiceAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .Single();

        Assert.Equal(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Method, usage.ValidOn);
        Assert.False(usage.AllowMultiple);
        Assert.True(usage.Inherited);
    }

    /// <summary>
    /// 组名可写，供模块统一分组
    /// </summary>
    [Fact]
    public void Name_IsWritable()
    {
        var attribute = new RemoteServiceAttribute
        {
            Name = "曦寒模块组"
        };

        Assert.Equal("曦寒模块组", attribute.Name);
    }
}

/// <summary>
/// 标注为启用远程服务的契约
/// </summary>
[RemoteService]
public interface IRemoteEnabledContract
{
}

/// <summary>
/// 实现了被标注契约但自身未标注的样例
/// </summary>
public class ImplementsRemoteEnabledContract : IRemoteEnabledContract
{
}

/// <summary>
/// 标注为启用远程服务的样例
/// </summary>
[RemoteService]
public class RemoteEnabledSample
{
}

/// <summary>
/// 标注为禁用远程服务的样例
/// </summary>
[RemoteService(false)]
public class RemoteDisabledSample
{
}

/// <summary>
/// 标注为禁用元数据的样例
/// </summary>
[RemoteService(IsMetadataEnabled = false)]
public class RemoteMetadataDisabledSample
{
}

/// <summary>
/// 完全未标注的样例
/// </summary>
public class RemoteUndecoratedSample
{
}

/// <summary>
/// 承载方法级远程服务标注的样例
/// </summary>
public class RemoteServiceSampleTarget
{
    /// <summary>
    /// 标注为启用元数据的方法
    /// </summary>
    [RemoteService]
    public void MetadataEnabledMethod()
    {
    }

    /// <summary>
    /// 标注为禁用元数据的方法
    /// </summary>
    [RemoteService(IsMetadataEnabled = false)]
    public void MetadataDisabledMethod()
    {
    }

    /// <summary>
    /// 未标注的方法
    /// </summary>
    public void PlainMethod()
    {
    }
}
