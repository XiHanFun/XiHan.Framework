// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.ObjectMapping.Extensions;
using XiHan.Framework.ObjectMapping.Tests.Fakes;

namespace XiHan.Framework.ObjectMapping.Tests.Extensions;

/// <summary>
/// 对象扩展管理器扩展方法测试
/// </summary>
/// <remarks>
/// 这批扩展方法是「注册扩展属性」的日常入口，四个重载最终都收敛到同一条
/// AddOrUpdate(objectType, options =&gt; options.AddOrUpdateProperty(...)) 路径上。
/// GetProperties 对未注册类型返回共享的空列表而不是 null，调用方因此可以无脑 foreach。
/// GetPropertiesAndCheckPolicyAsync 需要从容器里解析 ExtensionPropertyPolicyChecker，
/// 这里用手写替身控制放行结果，不依赖任何真实的功能开关或权限系统。
/// </remarks>
public class ObjectExtensionManagerExtensionsTests
{
    /// <summary>
    /// 双泛型重载按对象类型与属性类型注册扩展属性
    /// </summary>
    [Fact]
    public void AddOrUpdateProperty_WithObjectAndPropertyTypeArguments_RegistersProperty()
    {
        var manager = ObjectExtensionManager.Instance;

        var returned = manager.AddOrUpdateProperty<GenericTarget, string>("Name");

        Assert.Same(manager, returned);
        var property = manager.GetPropertyOrNull<GenericTarget>("Name");
        Assert.NotNull(property);
        Assert.Equal(typeof(string), property.Type);
    }

    /// <summary>
    /// 单泛型重载可一次性把同一个属性注册到多个对象类型上
    /// </summary>
    [Fact]
    public void AddOrUpdateProperty_WithTypeArray_RegistersPropertyOnEveryType()
    {
        var manager = ObjectExtensionManager.Instance;
        var types = new[] { typeof(ArrayTargetA), typeof(ArrayTargetB) };

        var returned = manager.AddOrUpdateProperty<int>(types, "Age");

        Assert.Same(manager, returned);
        Assert.NotNull(manager.GetPropertyOrNull(typeof(ArrayTargetA), "Age"));
        Assert.NotNull(manager.GetPropertyOrNull(typeof(ArrayTargetB), "Age"));
    }

    /// <summary>
    /// 配置委托对数组里的每个类型都会执行一次
    /// </summary>
    [Fact]
    public void AddOrUpdateProperty_WithTypeArray_AppliesConfigureActionToEveryType()
    {
        var manager = ObjectExtensionManager.Instance;
        var types = new[] { typeof(ConfigureArrayTargetA), typeof(ConfigureArrayTargetB) };

        manager.AddOrUpdateProperty(types, typeof(string), "Name", property =>
        {
            property.Ui.Order = 7;
        });

        Assert.Equal(7, manager.GetPropertyOrNull(typeof(ConfigureArrayTargetA), "Name")!.Ui.Order);
        Assert.Equal(7, manager.GetPropertyOrNull(typeof(ConfigureArrayTargetB), "Name")!.Ui.Order);
    }

    /// <summary>
    /// 类型数组为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void AddOrUpdateProperty_WhenTypeArrayNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => ObjectExtensionManager.Instance.AddOrUpdateProperty((Type[])null!, typeof(string), "Name"));

        Assert.Equal("objectTypes", exception.ParamName);
    }

    /// <summary>
    /// 管理器为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void AddOrUpdateProperty_WhenManagerNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => ObjectExtensionManagerExtensions.AddOrUpdateProperty(null!, typeof(GenericTarget), typeof(string), "Name"));

        Assert.Equal("objectExtensionManager", exception.ParamName);
    }

    /// <summary>
    /// 未注册的对象类型查属性返回 null
    /// </summary>
    [Fact]
    public void GetPropertyOrNull_WhenObjectTypeNotRegistered_ReturnsNull()
    {
        Assert.Null(ObjectExtensionManager.Instance.GetPropertyOrNull<NeverRegisteredTarget>("Name"));
    }

    /// <summary>
    /// 已注册对象类型但属性名不存在时返回 null
    /// </summary>
    [Fact]
    public void GetPropertyOrNull_WhenPropertyNotRegistered_ReturnsNull()
    {
        ObjectExtensionManager.Instance.AddOrUpdateProperty<MissingPropertyTarget, string>("Name");

        Assert.Null(ObjectExtensionManager.Instance.GetPropertyOrNull<MissingPropertyTarget>("Missing"));
    }

    /// <summary>
    /// 查属性时对象类型或属性名为 null 抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void GetPropertyOrNull_WhenArgumentNull_ThrowsArgumentNullException()
    {
        var manager = ObjectExtensionManager.Instance;

        var typeException = Assert.Throws<ArgumentNullException>(() => manager.GetPropertyOrNull(null!, "Name"));
        var nameException = Assert.Throws<ArgumentNullException>(() => manager.GetPropertyOrNull(typeof(GenericTarget), null!));

        Assert.Equal("objectType", typeException.ParamName);
        Assert.Equal("propertyName", nameException.ParamName);
    }

    /// <summary>
    /// 未注册的对象类型取属性列表返回空列表而不是 null
    /// </summary>
    [Fact]
    public void GetProperties_WhenObjectTypeNotRegistered_ReturnsEmptyList()
    {
        var properties = ObjectExtensionManager.Instance.GetProperties<NeverRegisteredTarget>();

        Assert.NotNull(properties);
        Assert.Empty(properties);
    }

    /// <summary>
    /// 已注册的对象类型按界面顺序返回属性列表
    /// </summary>
    [Fact]
    public void GetProperties_ReturnsRegisteredPropertiesInUiOrder()
    {
        var manager = ObjectExtensionManager.Instance;
        manager.AddOrUpdateProperty(typeof(OrderedTarget), typeof(string), "Second", property =>
        {
            property.Ui.Order = 20;
        });
        manager.AddOrUpdateProperty(typeof(OrderedTarget), typeof(string), "First", property =>
        {
            property.Ui.Order = 10;
        });

        var names = manager.GetProperties<OrderedTarget>().Select(property => property.Name).ToArray();

        Assert.Equal(new[] { "First", "Second" }, names);
    }

    /// <summary>
    /// 取属性列表时对象类型为 null 抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public void GetProperties_WhenObjectTypeNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => ObjectExtensionManager.Instance.GetProperties(null!));

        Assert.Equal("objectType", exception.ParamName);
    }

    /// <summary>
    /// 策略过滤只保留检查通过的属性
    /// </summary>
    [Fact]
    public async Task GetPropertiesAndCheckPolicyAsync_KeepsOnlyGrantedProperties()
    {
        var manager = ObjectExtensionManager.Instance;
        manager.AddOrUpdateProperty(typeof(PolicyTarget), typeof(string), "Allowed", property =>
        {
            property.Ui.Order = 10;
            property.Policy.Permissions.PermissionNames = ["Policy.Allowed"];
        });
        manager.AddOrUpdateProperty(typeof(PolicyTarget), typeof(string), "Denied", property =>
        {
            property.Ui.Order = 20;
            property.Policy.Permissions.PermissionNames = ["Policy.Denied"];
        });

        var checker = new FakePolicyChecker();
        checker.GrantedPermissions.Add("Policy.Allowed");
        using var provider = BuildProvider(checker);

        var properties = await manager.GetPropertiesAndCheckPolicyAsync<PolicyTarget>(provider);

        var property = Assert.Single(properties);
        Assert.Equal("Allowed", property.Name);
    }

    /// <summary>
    /// 没有配置任何策略的属性一律保留
    /// </summary>
    [Fact]
    public async Task GetPropertiesAndCheckPolicyAsync_WhenNoPolicyConfigured_KeepsAllProperties()
    {
        var manager = ObjectExtensionManager.Instance;
        manager.AddOrUpdateProperty(typeof(NoPolicyTarget), typeof(string), "Name");
        manager.AddOrUpdateProperty(typeof(NoPolicyTarget), typeof(int), "Age");

        using var provider = BuildProvider(new FakePolicyChecker());

        var properties = await manager.GetPropertiesAndCheckPolicyAsync(typeof(NoPolicyTarget), provider);

        Assert.Equal(2, properties.Count);
    }

    /// <summary>
    /// 未注册的对象类型返回空列表
    /// </summary>
    [Fact]
    public async Task GetPropertiesAndCheckPolicyAsync_WhenObjectTypeNotRegistered_ReturnsEmptyList()
    {
        using var provider = BuildProvider(new FakePolicyChecker());

        var properties = await ObjectExtensionManager.Instance
            .GetPropertiesAndCheckPolicyAsync(typeof(NeverRegisteredTarget), provider);

        Assert.Empty(properties);
    }

    /// <summary>
    /// 服务提供程序为 null 时抛出 ArgumentNullException
    /// </summary>
    [Fact]
    public async Task GetPropertiesAndCheckPolicyAsync_WhenServiceProviderNull_ThrowsArgumentNullException()
    {
        var exception = await Assert.ThrowsAsync<ArgumentNullException>(
            () => ObjectExtensionManager.Instance.GetPropertiesAndCheckPolicyAsync(typeof(GenericTarget), null!));

        Assert.Equal("serviceProvider", exception.ParamName);
    }

    /// <summary>
    /// 构造一个只注册了策略检查器替身的服务提供程序
    /// </summary>
    /// <param name="checker">策略检查器替身</param>
    /// <returns>服务提供程序</returns>
    private static ServiceProvider BuildProvider(FakePolicyChecker checker)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ExtensionPropertyPolicyChecker>(checker);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// 双泛型重载用例标记类型
    /// </summary>
    private sealed class GenericTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 批量注册用例标记类型 A
    /// </summary>
    private sealed class ArrayTargetA : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 批量注册用例标记类型 B
    /// </summary>
    private sealed class ArrayTargetB : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 批量配置用例标记类型 A
    /// </summary>
    private sealed class ConfigureArrayTargetA : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 批量配置用例标记类型 B
    /// </summary>
    private sealed class ConfigureArrayTargetB : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 永不注册的标记类型
    /// </summary>
    private sealed class NeverRegisteredTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 属性名缺失用例标记类型
    /// </summary>
    private sealed class MissingPropertyTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 界面顺序用例标记类型
    /// </summary>
    private sealed class OrderedTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 策略过滤用例标记类型
    /// </summary>
    private sealed class PolicyTarget : FakeExtensibleObject
    {
    }

    /// <summary>
    /// 无策略用例标记类型
    /// </summary>
    private sealed class NoPolicyTarget : FakeExtensibleObject
    {
    }
}
