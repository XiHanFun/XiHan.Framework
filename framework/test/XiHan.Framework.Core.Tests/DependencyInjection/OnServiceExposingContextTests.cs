// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.Core.DependencyInjection;

namespace XiHan.Framework.Core.Tests.DependencyInjection;

/// <summary>
/// 服务暴露上下文与暴露回调列表测试
/// </summary>
/// <remarks>
/// 暴露上下文是模块在注册落盘前改写暴露清单的最后一道口子，
/// 关键契约是「服务标识列表可被就地修改」——直接持有传入列表而不是拷贝，改写才会生效。
/// </remarks>
public class OnServiceExposingContextTests
{
    /// <summary>
    /// 以类型列表构造时逐个转换为无键服务标识
    /// </summary>
    [Fact]
    public void Constructor_WithTypeList_ConvertsToKeylessIdentifiers()
    {
        var context = new OnServiceExposingContext(typeof(OseService), new List<Type> { typeof(IOseContract), typeof(OseService) });

        Assert.Equal(typeof(OseService), context.ImplementationType);
        Assert.Equal(2, context.ExposedTypes.Count);
        Assert.All(context.ExposedTypes, identifier => Assert.Null(identifier.ServiceKey));
        Assert.Equal(typeof(IOseContract), context.ExposedTypes[0].ServiceType);
    }

    /// <summary>
    /// 以服务标识列表构造时直接持有原列表
    /// </summary>
    [Fact]
    public void Constructor_WithIdentifierList_KeepsSameListInstance()
    {
        List<ServiceIdentifier> identifiers = [new ServiceIdentifier(typeof(IOseContract))];

        var context = new OnServiceExposingContext(typeof(OseService), identifiers);

        Assert.Same(identifiers, context.ExposedTypes);
    }

    /// <summary>
    /// 就地追加的暴露类型对调用方可见
    /// </summary>
    [Fact]
    public void ExposedTypes_WhenMutated_IsVisibleToCaller()
    {
        List<ServiceIdentifier> identifiers = [new ServiceIdentifier(typeof(IOseContract))];
        var context = new OnServiceExposingContext(typeof(OseService), identifiers);

        context.ExposedTypes.Add(new ServiceIdentifier("keyed", typeof(IOseContract)));

        Assert.Equal(2, identifiers.Count);
        Assert.Equal("keyed", identifiers[1].ServiceKey);
    }

    /// <summary>
    /// 实现类型为空时抛出
    /// </summary>
    [Fact]
    public void Constructor_WhenImplementationTypeNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new OnServiceExposingContext(null!, new List<Type> { typeof(IOseContract) });
        });
    }

    /// <summary>
    /// 暴露类型列表为空引用时抛出
    /// </summary>
    [Fact]
    public void Constructor_WhenExposedTypesNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new OnServiceExposingContext(typeof(OseService), (List<ServiceIdentifier>)null!);
        });
    }

    /// <summary>
    /// 暴露回调列表按加入顺序执行
    /// </summary>
    [Fact]
    public void ServiceExposingActionList_InvokesActionsInOrder()
    {
        var list = new ServiceExposingActionList();
        List<string> calls = [];
        list.Add(_ => calls.Add("first"));
        list.Add(_ => calls.Add("second"));

        var context = new OnServiceExposingContext(typeof(OseService), new List<Type> { typeof(IOseContract) });
        foreach (var action in list)
        {
            action(context);
        }

        Assert.Equal(2, calls.Count);
        Assert.Equal("first", calls[0]);
        Assert.Equal("second", calls[1]);
    }
}

/// <summary>
/// 暴露上下文测试用契约
/// </summary>
internal interface IOseContract;

/// <summary>
/// 暴露上下文测试用实现
/// </summary>
internal class OseService : IOseContract;
