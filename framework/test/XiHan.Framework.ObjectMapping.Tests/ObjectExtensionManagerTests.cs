// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace XiHan.Framework.ObjectMapping.Tests;

/// <summary>
/// 对象扩展管理器测试
/// </summary>
/// <remarks>
/// <see cref="ObjectExtensionManager.Instance"/> 是进程级单例且没有公开的复位入口，
/// 因此每个用例都使用自己专属的标记类型注册——管理器以类型为键做隔离，只要类型不重复，
/// 用例之间就不会相互污染，也不需要串行化整个测试类。
/// 对 GetExtendedObjects 这类返回全量数据的接口，只断言「包含」而不断言数量。
/// </remarks>
public class ObjectExtensionManagerTests
{
    /// <summary>
    /// 单例入口稳定且非空
    /// </summary>
    [Fact]
    public void Instance_IsStableSingleton()
    {
        Assert.NotNull(ObjectExtensionManager.Instance);
        Assert.Same(ObjectExtensionManager.Instance, ObjectExtensionManager.Instance);
    }

    /// <summary>
    /// 未注册的类型查不到扩展信息
    /// </summary>
    [Fact]
    public void GetOrNull_WhenTypeNotRegistered_ReturnsNull()
    {
        Assert.Null(ObjectExtensionManager.Instance.GetOrNull<NeverRegisteredTarget>());
        Assert.Null(ObjectExtensionManager.Instance.GetOrNull(typeof(NeverRegisteredTarget)));
    }

    /// <summary>
    /// 注册后可按泛型与 Type 两种方式取回同一份扩展信息
    /// </summary>
    [Fact]
    public void AddOrUpdate_RegistersExtensionInfoForGivenType()
    {
        var manager = ObjectExtensionManager.Instance;

        var returned = manager.AddOrUpdate<RegisteredTarget>();

        Assert.Same(manager, returned);
        var info = manager.GetOrNull<RegisteredTarget>();
        Assert.NotNull(info);
        Assert.Equal(typeof(RegisteredTarget), info.Type);
        Assert.Same(info, manager.GetOrNull(typeof(RegisteredTarget)));
    }

    /// <summary>
    /// 重复注册同一类型是幂等的，始终复用第一次创建的扩展信息实例
    /// </summary>
    [Fact]
    public void AddOrUpdate_CalledTwice_ReusesSameExtensionInfoInstance()
    {
        var manager = ObjectExtensionManager.Instance;

        manager.AddOrUpdate<IdempotentTarget>();
        var first = manager.GetOrNull<IdempotentTarget>();
        manager.AddOrUpdate<IdempotentTarget>();
        var second = manager.GetOrNull<IdempotentTarget>();

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    /// <summary>
    /// 配置委托每次调用都会执行，这正是「Update」语义的落点
    /// </summary>
    [Fact]
    public void AddOrUpdate_InvokesConfigureActionOnEveryCall()
    {
        var manager = ObjectExtensionManager.Instance;
        var invokedCount = 0;

        manager.AddOrUpdate<ConfigureCountTarget>(_ => invokedCount++);
        manager.AddOrUpdate<ConfigureCountTarget>(_ => invokedCount++);

        Assert.Equal(2, invokedCount);
    }

    /// <summary>
    /// 配置委托拿到的就是该类型对应的扩展信息
    /// </summary>
    [Fact]
    public void AddOrUpdate_PassesExtensionInfoOfRequestedTypeToConfigureAction()
    {
        var manager = ObjectExtensionManager.Instance;
        ObjectExtensionInfo? captured = null;

        manager.AddOrUpdate(typeof(CaptureTarget), info => captured = info);

        Assert.NotNull(captured);
        Assert.Equal(typeof(CaptureTarget), captured.Type);
        Assert.Same(captured, manager.GetOrNull(typeof(CaptureTarget)));
    }

    /// <summary>
    /// 批量重载会为数组中的每个类型都建立扩展信息
    /// </summary>
    [Fact]
    public void AddOrUpdate_WithTypeArray_RegistersEveryType()
    {
        var manager = ObjectExtensionManager.Instance;
        var types = new[] { typeof(BatchTargetA), typeof(BatchTargetB) };

        var returned = manager.AddOrUpdate(types, info => info.AddOrUpdateProperty<string>("Shared"));

        Assert.Same(manager, returned);
        Assert.True(manager.GetOrNull(typeof(BatchTargetA))!.HasProperty("Shared"));
        Assert.True(manager.GetOrNull(typeof(BatchTargetB))!.HasProperty("Shared"));
    }

    /// <summary>
    /// 空类型数组是合法输入，不做任何注册
    /// </summary>
    [Fact]
    public void AddOrUpdate_WithEmptyTypeArray_DoesNothing()
    {
        var manager = ObjectExtensionManager.Instance;

        var returned = manager.AddOrUpdate(Type.EmptyTypes);

        Assert.Same(manager, returned);
    }

    /// <summary>
    /// 不同类型的扩展属性互不可见
    /// </summary>
    [Fact]
    public void AddOrUpdate_KeepsPropertiesIsolatedBetweenTypes()
    {
        var manager = ObjectExtensionManager.Instance;

        manager.AddOrUpdate(typeof(IsolatedTargetA), info => info.AddOrUpdateProperty<string>("OnlyOnA"));
        manager.AddOrUpdate(typeof(IsolatedTargetB));

        Assert.True(manager.GetOrNull(typeof(IsolatedTargetA))!.HasProperty("OnlyOnA"));
        Assert.False(manager.GetOrNull(typeof(IsolatedTargetB))!.HasProperty("OnlyOnA"));
    }

    /// <summary>
    /// 类型为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void AddOrUpdate_WhenTypeNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => ObjectExtensionManager.Instance.AddOrUpdate((Type)null!));

        Assert.Equal("type", exception.ParamName);
    }

    /// <summary>
    /// 类型数组为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void AddOrUpdate_WhenTypeArrayNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => ObjectExtensionManager.Instance.AddOrUpdate((Type[])null!));

        Assert.Equal("types", exception.ParamName);
    }

    /// <summary>
    /// 查询时类型为 null 抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void GetOrNull_WhenTypeNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => ObjectExtensionManager.Instance.GetOrNull(null!));

        Assert.Equal("type", exception.ParamName);
    }

    /// <summary>
    /// 已注册的类型会出现在全量扩展对象列表中
    /// </summary>
    [Fact]
    public void GetExtendedObjects_ContainsRegisteredType()
    {
        var manager = ObjectExtensionManager.Instance;
        manager.AddOrUpdate<ExtendedObjectsTarget>();

        var extendedObjects = manager.GetExtendedObjects();

        Assert.Contains(extendedObjects, info => info.Type == typeof(ExtendedObjectsTarget));
        Assert.DoesNotContain(extendedObjects, info => info.Type == typeof(NeverRegisteredTarget));
    }

    /// <summary>
    /// 配置字典可存放任意键值对
    /// </summary>
    [Fact]
    public void Configuration_StoresArbitraryEntries()
    {
        var key = "ObjectExtensionManagerTests." + Guid.NewGuid().ToString("N");

        ObjectExtensionManager.Instance.Configuration[key] = 42;

        Assert.True(ObjectExtensionManager.Instance.Configuration.TryGetValue(key, out var stored));
        Assert.Equal(42, stored);
    }

    /// <summary>
    /// 并发注册同一类型只会产生一份扩展信息
    /// </summary>
    /// <remarks>
    /// 管理器内部用 ConcurrentDictionary.GetOrAdd 承诺线程安全，这里用并行注册压一下该承诺。
    /// </remarks>
    [Fact]
    public void AddOrUpdate_UnderConcurrentCalls_KeepsSingleExtensionInfo()
    {
        var manager = ObjectExtensionManager.Instance;
        var observed = new ObjectExtensionInfo[64];

        Parallel.For(0, observed.Length, index =>
        {
            manager.AddOrUpdate(typeof(ConcurrentTarget));
            observed[index] = manager.GetOrNull(typeof(ConcurrentTarget))!;
        });

        Assert.All(observed, info => Assert.Same(observed[0], info));
    }

    /// <summary>
    /// 永不注册的标记类型
    /// </summary>
    private sealed class NeverRegisteredTarget
    {
    }

    /// <summary>
    /// 基础注册用例标记类型
    /// </summary>
    private sealed class RegisteredTarget
    {
    }

    /// <summary>
    /// 幂等注册用例标记类型
    /// </summary>
    private sealed class IdempotentTarget
    {
    }

    /// <summary>
    /// 配置委托计数用例标记类型
    /// </summary>
    private sealed class ConfigureCountTarget
    {
    }

    /// <summary>
    /// 配置委托入参捕获用例标记类型
    /// </summary>
    private sealed class CaptureTarget
    {
    }

    /// <summary>
    /// 批量注册用例标记类型 A
    /// </summary>
    private sealed class BatchTargetA
    {
    }

    /// <summary>
    /// 批量注册用例标记类型 B
    /// </summary>
    private sealed class BatchTargetB
    {
    }

    /// <summary>
    /// 类型隔离用例标记类型 A
    /// </summary>
    private sealed class IsolatedTargetA
    {
    }

    /// <summary>
    /// 类型隔离用例标记类型 B
    /// </summary>
    private sealed class IsolatedTargetB
    {
    }

    /// <summary>
    /// 全量列表用例标记类型
    /// </summary>
    private sealed class ExtendedObjectsTarget
    {
    }

    /// <summary>
    /// 并发注册用例标记类型
    /// </summary>
    private sealed class ConcurrentTarget
    {
    }
}
