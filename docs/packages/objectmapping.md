# XiHan.Framework.ObjectMapping

> 对象映射：集成 Mapster 注册 `IMapper`，并内置一套"对象扩展属性（动态属性）"管理体系（存储 + 验证 + 访问策略）。

- **NuGet**：`XiHan.Framework.ObjectMapping`
- **模块类**：`XiHanObjectMappingModule`
- **所在层**：基础设施层
- **关键依赖**：`Mapster`（对象映射引擎）+ 框架内部依赖 `XiHan.Framework.Core` / `Localization.Abstractions` / `Validation.Abstractions`

## 概述

本包负责把一个对象的数据映射到另一个对象（如实体 ↔ DTO ↔ 视图模型）。它集成高性能映射库 **Mapster**，把 `MapsterMapper.IMapper` 注册进 DI 供全框架注入使用（生命周期 **Transient**）。除映射外，它还内置一套"**对象扩展属性**"机制：在不改动类定义的前提下，为任意实现 `IHasExtraProperties` 的对象动态挂载额外属性（`ExtraProperties`），并为这些动态属性配置**验证器**（对接 [Validation.Abstractions](./validation-abstractions)）与三层**访问策略**（全局功能 / 功能开关 / 权限）。可本地化显示名等能力依赖 [Localization.Abstractions](./localization-abstractions)。

## 何时使用

- 需要在实体、DTO、视图模型之间做批量属性映射，又不想手写赋值代码 → 注入 `IMapper`。
- 想为已有类型**动态追加属性**（`ExtraProperties`）并参与序列化/持久化。
- 需要给动态扩展属性配置验证规则、UI/查找配置，或按功能/权限门控其可见性。

## 安装与启用

```bash
dotnet add package XiHan.Framework.ObjectMapping
```

```csharp
[DependsOn(typeof(XiHanObjectMappingModule))]
public class MyModule : XiHanModule { }
```

模块 `ConfigureServices` 调用 `AddXiHanMapster()`，其中 `services.AddTransient<IMapper, Mapper>()` 把 Mapster 的 `IMapper` 注册为 **Transient**。`ExtensionPropertyPolicyChecker` 实现 `ITransientDependency`，由框架约定自动注册。`ObjectExtensionManager` 是**静态单例**（`ObjectExtensionManager.Instance`），不走 DI。

> 依赖模块：`XiHanObjectMappingModule` 上标注 `[DependsOn]` 引入 `XiHanLocalizationAbstractionsModule`、`XiHanValidationAbstractionsModule`。

## 核心能力

- **Mapster 集成**：注册 `IMapper`，提供高性能对象映射引擎（映射规则用 Mapster 原生 API 配置）。
- **对象扩展管理**：`ObjectExtensionManager` 单例集中管理各类型的动态扩展属性、类型级配置字典与对象级验证器。
- **动态属性存储**：`IHasExtraProperties` + `ExtraPropertyDictionary` 为对象提供可序列化的键值对额外属性存储。
- **属性读写扩展**：`HasExtraPropertiesExtensions` 提供类型安全的 `GetProperty<T>` / `SetProperty` / `RemoveProperty` 等便捷方法（含验证钩子）。
- **扩展属性验证**：设置值时按扩展属性定义的验证规则校验并收集错误（`ObjectExtensionValidationContext` / `ObjectExtensionPropertyValidationContext`）。
- **三层访问策略**：`ExtensionPropertyPolicyChecker` 按全局功能、功能开关、权限校验扩展属性可访问性，配合 `GetPropertiesAndCheckPolicyAsync` 过滤可见属性。

## 主要 API / 类型

### 映射

| 类型 | 说明 |
| --- | --- |
| `MapsterMapper.IMapper` | Mapster 映射器，DI 注入使用（Transient）。常用 `TDestination Map<TDestination>(object source)` / `Map<TSource, TDestination>(...)` |
| `XiHanObjectMappingServiceCollectionExtensions` | DI 入口 `AddXiHanMapster(IServiceCollection)` |

### 动态扩展属性

| 类型 | 说明 |
| --- | --- |
| `IHasExtraProperties` | 定义 `ExtraPropertyDictionary ExtraProperties { get; }`，用于动态属性存储 |
| `ExtraPropertyDictionary` | 额外属性字典，继承 `Dictionary<string, object?>`（可序列化） |
| `ObjectExtensionManager` | 扩展管理器单例（`Instance`）。`AddOrUpdate<TObject>(...)` / `AddOrUpdate(Type, ...)` / `AddOrUpdate(Type[], ...)`；`GetOrNull<TObject>()`；`GetExtendedObjects()` |
| `ObjectExtensionInfo` | 特定类型的扩展信息：`Type`、`Configuration`、对象级 `Validators`、属性字典与查询方法 |
| `ObjectExtensionPropertyInfo` | 扩展属性信息，含 UI 配置、查找配置、`Policy`（策略配置）、默认值 |
| `IBasicObjectExtensionPropertyInfo` | 扩展属性信息的基础契约 |

`HasExtraPropertiesExtensions`（静态扩展，作用于 `IHasExtraProperties`）：

| 方法 | 说明 |
| --- | --- |
| `bool HasProperty(this IHasExtraProperties source, string name)` | 是否存在该额外属性 |
| `object? GetProperty(this IHasExtraProperties source, string name, object? defaultValue = null)` | 取值（弱类型） |
| `TProperty? GetProperty<TProperty>(this IHasExtraProperties source, string name, TProperty? defaultValue = default)` | 取值（强类型，仅支持原始/枚举/`Guid`；非原始类型抛 `XiHanException`） |
| `TSource SetProperty<TSource>(this TSource source, string name, object? value, bool validate = true)` | 设值（`validate=true` 时先走 `ExtensibleObjectValidator.CheckValue`），链式返回 |
| `TSource RemoveProperty<TSource>(this TSource source, string name)` | 移除，链式返回 |
| `TSource SetDefaultsForExtraProperties<TSource>(this TSource source, Type? objectType = null)` | 按扩展定义填充缺省值 |
| `void SetExtraPropertiesToRegularProperties(this IHasExtraProperties source)` | 把同名额外属性回填到常规属性并移除 |
| `bool HasSameExtraProperties(this IHasExtraProperties source, IHasExtraProperties other)` | 比较两对象额外属性是否相同 |

`ObjectExtensionManagerExtensions`（静态扩展，作用于 `ObjectExtensionManager`）：`AddOrUpdateProperty<TObject, TProperty>(name, configure?)`（及 `Type` / `Type[]` 重载）、`GetPropertyOrNull<TObject>(name)`、`GetProperties<TObject>()`、`GetPropertiesAndCheckPolicyAsync<TObject>(IServiceProvider)`（按策略过滤可见属性）。

`ExtraPropertyDictionaryExtensions`：`T ToEnum<T>(this ExtraPropertyDictionary dict, string key) where T : Enum` 与非泛型重载 `object ToEnum(this ExtraPropertyDictionary dict, string key, Type enumType)`（值已是目标枚举类型时直接返回，否则原地 `Enum.Parse` 并写回字典）；`bool HasSameItems(this ExtraPropertyDictionary dict, ExtraPropertyDictionary other)` 按键逐一比较（值用 `ToString()` 比较），供 `HasSameExtraProperties` 内部调用。

### 验证与策略

| 类型 | 说明 |
| --- | --- |
| `ExtensibleObjectValidator` | 扩展对象验证器：`CheckValue(...)` 校验单个属性值、聚合对象级/属性级验证 |
| `ObjectExtensionValidationContext` | 对象级验证上下文，聚合扩展信息与错误集合 |
| `ObjectExtensionPropertyValidationContext` | 属性级验证上下文，含属性值、错误集合与 `IServiceProvider` |
| `ExtensionPropertyPolicyChecker` | `ITransientDependency`。`Task<bool> CheckPolicyAsync(ExtensionPropertyPolicyConfiguration policy)`，按全局功能→功能开关→权限依次校验 |
| `ExtensionPropertyPolicyConfiguration` | 策略聚合：`GlobalFeatures` / `Features` / `Permissions`（各自含名称集合 + `RequiresAll`） |
| `ExtensionPropertyGlobalFeaturePolicyConfiguration` / `ExtensionPropertyFeaturePolicyConfiguration` / `ExtensionPropertyPermissionPolicyConfiguration` | 三类策略的具体配置 |
| `ExtensionPropertyLookupConfiguration` | 扩展属性的查找/字典配置 |
| `ExtensionPropertyHelper` | 静态帮助类：`GetDefaultAttributes(Type)` 按属性类型自动生成默认验证特性——非可空基元类型或枚举类型自动追加 `RequiredAttribute`，枚举类型再追加 `EnumDataTypeAttribute`；`GetDefaultValue(propertyType, defaultValueFactory, defaultValue)` 按 `defaultValueFactory > defaultValue > 类型默认值` 优先级解析。`ObjectExtensionPropertyInfo` 构造时会自动调用前者填充其 `Attributes` |
| `ObjectExtensionPropertyInfoExtensions` | `GetValidationAttributes(this ObjectExtensionPropertyInfo)`：从 `Attributes` 中筛出 `ValidationAttribute[]`，供 `ExtensibleObjectValidator` 校验流程使用 |

## 使用示例

### 1. `IMapper` 映射 DTO ↔ 实体

```csharp
using MapsterMapper;

public class UserAppService(IMapper mapper)
{
    // 实体 -> DTO
    public UserDto ToDto(UserEntity entity) => mapper.Map<UserDto>(entity);

    // DTO -> 实体（写入已有实例）
    public UserEntity Apply(UpdateUserDto dto, UserEntity entity)
        => mapper.Map(dto, entity);
}
```

> 字段名/类型不一致的自定义映射规则用 Mapster 原生 API（`TypeAdapterConfig`）配置，本包只负责注册 `IMapper`。

### 2. 定义并读写动态扩展属性

```csharp
using XiHan.Framework.ObjectMapping;
using XiHan.Framework.ObjectMapping.Extensions;
using XiHan.Framework.ObjectMapping.Extensions.Data;

public class Customer : IHasExtraProperties
{
    public ExtraPropertyDictionary ExtraProperties { get; } = new();
}

// 启动时为 Customer 注册一个扩展属性（可挂验证/策略）
ObjectExtensionManager.Instance
    .AddOrUpdateProperty<Customer, string>("VipLevel");

// 运行时读写（SetProperty 默认触发验证）
var customer = new Customer();
customer.SetProperty("VipLevel", "Gold");
var level = customer.GetProperty<string>("VipLevel"); // "Gold"
```

### 3. 按访问策略过滤可见扩展属性

```csharp
// 在有 IServiceProvider 的上下文中，仅返回通过全局功能/功能/权限检查的属性
var visible = await ObjectExtensionManager.Instance
    .GetPropertiesAndCheckPolicyAsync<Customer>(serviceProvider);
```

## 扩展点 / 自定义

- **自定义映射规则**：用 Mapster `TypeAdapterConfig` 在应用启动时注册。
- **扩展属性验证器**：在 `AddOrUpdateProperty` 的 `configure` 委托里配置属性验证；对象级验证挂 `ObjectExtensionInfo.Validators`。
- **访问策略门控**：在 `ObjectExtensionPropertyInfo.Policy` 上配置全局功能/功能/权限，用 `GetPropertiesAndCheckPolicyAsync` 过滤。

## 注意事项与最佳实践

- **`IMapper` 是 Transient**：不要缓存为长生命周期单例字段；按需注入。
- **`GetProperty<T>` 类型限制**：仅支持原始类型、枚举、`Guid`（内部走 `Convert.ChangeType` / `Enum.Parse` / `TypeConverter`）；复杂类型请用弱类型 `GetProperty` 再自行转换，否则抛 `XiHanException`。
- **`SetProperty` 默认验证**：`validate=true` 会走 `ExtensibleObjectValidator.CheckValue`，未注册扩展属性或值不合规会抛错；确定跳过时显式传 `validate: false`。
- **`ObjectExtensionManager` 是静态单例**：扩展属性定义通常在应用启动阶段一次性注册，运行期只读写值。
- **非可空基元/枚举类型会自动挂 `[Required]`**：`AddOrUpdateProperty<TProperty>` 若 `TProperty` 是非可空基元类型（如 `int`、`bool`）或枚举，`ExtensionPropertyHelper.GetDefaultAttributes` 会自动追加 `RequiredAttribute`（枚举再加 `EnumDataTypeAttribute`）；如需允许空值，请改用可空类型（如 `int?`）或在 `configureAction` 中自行调整 `Attributes`。

## 依赖模块

- 内部依赖：[XiHan.Framework.Core](./core)、[XiHan.Framework.Localization.Abstractions](./localization-abstractions)、[XiHan.Framework.Validation.Abstractions](./validation-abstractions)。
- 第三方核心：`Mapster`。

## 相关模块

- [XiHan.Framework.Validation.Abstractions](./validation-abstractions) — 扩展属性验证依赖其验证抽象。
- [XiHan.Framework.Localization.Abstractions](./localization-abstractions) — 扩展属性可本地化显示名等能力的抽象来源。
