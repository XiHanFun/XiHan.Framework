# 对象映射与序列化

实体和 DTO 之间怎么搬数据，对象和 JSON 之间怎么往返。这两件事分散在三个包里，找错地方是最常见的浪费时间点。

## 三个包，各管一段

| 你要做的事 | 去哪 | 形态 |
| --- | --- | --- |
| 实体 ↔ DTO 批量赋值 | `XiHan.Framework.ObjectMapping` | 集成 Mapster，注册 `IMapper` |
| 不改类定义给对象挂动态属性 | `XiHan.Framework.ObjectMapping` | `IHasExtraProperties` + `ObjectExtensionManager` |
| JSON 转换器、序列化选项、`JsonHelper` | `XiHan.Framework.Utils` 的 `Serialization/Json` | 全静态，无需 DI |
| 结构不固定的 JSON（免 DTO） | `XiHan.Framework.Serialization` | `DynamicJsonObject` 等动态类型 |
| Web 出入参的 JSON 约定 | `XiHan.Framework.Web.Api` | `XiHanWebCoreMvcOptions` |

::: warning 序列化选项不在 Serialization 包里
`JsonConverterFactory`、`JsonSerializeOptions`、`JsonHelper` 全部位于 **`XiHan.Framework.Utils`** 的 `Serialization/Json` 目录。

`XiHan.Framework.Serialization` 包提供的是**动态 JSON** 那一套；它的模块类 `XiHanSerializationModule.ConfigureServices` 是空实现，不注册任何服务，能力全部通过静态类调用。
:::

## 安装与启用

对象映射需要引模块：

```bash
dotnet add package XiHan.Framework.ObjectMapping
```

```csharp
[DependsOn(typeof(XiHanObjectMappingModule))]
public class MyModule : XiHanModule;
```

`XiHanObjectMappingModule.ConfigureServices` 里只做一件事：调 `services.AddXiHanMapster()`，把 `MapsterMapper.IMapper` 注册为 **Transient**（实现类 `MapsterMapper.Mapper`）。`ExtensionPropertyPolicyChecker` 实现 `ITransientDependency`，由约定注册自动接入。`ObjectExtensionManager` 是静态单例，不走 DI。

JSON 那部分不需要额外装包：`XiHan.Framework.Utils` 是全框架的基础依赖，直接 `using XiHan.Framework.Utils.Serialization.Json;` 即可。

## 对象映射

### 框架默认走的是 Mapster 静态扩展

`CrudApplicationServiceBase` 的映射方法用的是 Mapster 的静态扩展 `Adapt<T>()`，不是注入的 `IMapper`：

```csharp
protected virtual Task<TEntityDto> MapEntityToDtoAsync(TEntity entity)
{
    var dto = entity.Adapt<TEntityDto>();
    return Task.FromResult(EnsureNotNullMapping(dto));
}
```

这一组方法都是 `protected virtual`，字段对不上时**重写它们**是最直接的做法：

| 方法 | 时机 |
| --- | --- |
| `MapEntityToDtoAsync(TEntity)` | 单条实体转 DTO |
| `MapEntitiesToDtosAsync(IEnumerable<TEntity>)` | 列表转 DTO 列表 |
| `MapDtoToEntityAsync(TEntityDto)` | DTO 转新实体 |
| `MapDtoToEntityAsync(TEntityDto, TEntity)` | DTO 写入已有实体 |
| `MapDtoToEntityAsync(TCreateDto)` | 创建 DTO 转新实体 |
| `MapDtoToEntityAsync(TUpdateDto, TEntity)` | 更新 DTO 写入已有实体 |

```csharp
protected override Task<UserDto> MapEntityToDtoAsync(SysUser entity)
{
    var dto = entity.Adapt<UserDto>();
    dto.DisplayName = $"{entity.NickName}（{entity.UserName}）";
    return Task.FromResult(dto);
}
```

### 注入 IMapper

自己的应用服务里想用 DI 版本，直接注入：

```csharp
using MapsterMapper;

public class UserAppService(IMapper mapper)
{
    public UserDto ToDto(SysUser entity) => mapper.Map<UserDto>(entity);
}
```

::: tip 两条路走的是同一份配置
框架只注册了 `IMapper`，没有向 DI 注册任何 `TypeAdapterConfig`。因此 `Adapt<T>()` 与注入的 `IMapper` 解析的是同一份 Mapster 全局配置——自定义映射规则用 Mapster 原生 API 在应用启动阶段注册一次，两边都生效。

`IMapper` 是 **Transient**，按需注入即可，不要缓存成长生命周期字段。
:::

## 动态扩展属性

不改类定义、给对象挂额外属性并参与序列化与持久化。

### 三步

```csharp
using XiHan.Framework.ObjectMapping;
using XiHan.Framework.ObjectMapping.Extensions;
using XiHan.Framework.ObjectMapping.Extensions.Data;

// 1. 类实现 IHasExtraProperties
public class Customer : IHasExtraProperties
{
    public ExtraPropertyDictionary ExtraProperties { get; } = new();
}

// 2. 启动阶段注册扩展属性定义（TObject 必须实现 IHasExtraProperties）
ObjectExtensionManager.Instance
    .AddOrUpdateProperty<Customer, string>("VipLevel", property =>
    {
        property.DefaultValue = "Normal";
    });

// 3. 运行期读写
var customer = new Customer();
customer.SetProperty("VipLevel", "Gold");
var level = customer.GetProperty<string>("VipLevel");
```

`SetProperty` / `RemoveProperty` 返回源对象，可以链式调用。

### 关键机制

| 机制 | 行为 |
| --- | --- |
| 定义存放 | `ObjectExtensionManager.Instance` 静态单例，`ConcurrentDictionary` 按类型索引 |
| 写入验证 | `SetProperty` 的 `validate` 默认 `true`，走 `ExtensibleObjectValidator.CheckValue`，失败抛 `XiHanValidationException` |
| 类型自动特性 | `ExtensionPropertyHelper.GetDefaultAttributes` 对非可空基元类型和枚举自动追加 `RequiredAttribute`，枚举再追加 `EnumDataTypeAttribute` |
| 默认值优先级 | `DefaultValueFactory` > `DefaultValue` > 类型默认值 |
| 代理穿透 | 验证前用 `ProxyHelper.UnProxy` 取真实类型，AOP 代理过的对象也能命中定义 |

::: warning `GetProperty<T>` 只吃扩展原始类型
泛型版按 `TypeHelper.IsPrimitiveExtended(includeEnums: true)` 放行：基元类型、枚举、`string`、`decimal`、`DateTime`、`DateTimeOffset`、`TimeSpan`、`Guid`，以及它们的可空版本。转换分三路：`Guid` 走 `TypeConverter`，枚举走 `Enum.Parse`，其余走 `Convert.ChangeType`。类型不在此列会抛 `XiHanException`——复杂类型请用弱类型 `GetProperty(name)` 自己转。
:::

::: warning 没注册过的属性名不会报错
`ExtensibleObjectValidator` 在两处直接返回：类型没在 `ObjectExtensionManager` 注册过、或该属性名没定义过。也就是说 `SetProperty("Typo", value)` 即使 `validate: true` 也**静默写进字典**，不会抛异常。属性名建议定为常量集中管理。
:::

### 访问策略当前是骨架

`ExtensionPropertyPolicyChecker.CheckPolicyAsync` 依次校验全局功能、功能开关、权限三段，每段支持「要求全部」与「要求任一」。但三个具体判定方法都是 `protected virtual` 且**默认实现恒返回 `true`**：

```csharp
protected virtual Task<bool> CheckGlobalFeaturesAsync(string featureName) => Task.FromResult(true);
protected virtual Task<bool> CheckFeaturesAsync(string featureName) => Task.FromResult(true);
protected virtual Task<bool> CheckPermissionsAsync(string permissionName) => Task.FromResult(true);
```

所以 `GetPropertiesAndCheckPolicyAsync` 开箱即用时会**返回全部属性**。要真正门控，必须派生 `ExtensionPropertyPolicyChecker` 重写这三个方法并替换注册。

```csharp
var visible = await ObjectExtensionManager.Instance
    .GetPropertiesAndCheckPolicyAsync<Customer>(serviceProvider);
```

## JSON 转换器

`JsonConverterFactory`（命名空间 `XiHan.Framework.Utils.Serialization.Json`）按用途分组提供转换器，共 28 个（每个类型都配了可空版本）。

| 方法 | 覆盖类型 |
| --- | --- |
| `GetNumericConverters()` | `int` `long` `float` `double` `decimal` `byte` `short` `uint` 及其可空版 |
| `GetDateTimeConverters(dateFormat, timeFormat, isUtc)` | `DateOnly` `TimeOnly` `DateTime` `DateTimeOffset` 及其可空版 |
| `GetCommonConverters()` | `bool` `Guid` 及其可空版 |
| `GetAllConverters(...)` | 上面三组合并 |
| `ConfigureConverters(this JsonSerializerOptions, ...)` | 把全部转换器加进已有选项，返回该选项 |
| `CreateOptions(writeIndented, camelCase, dateFormat, timeFormat, isUtc)` | 新建一份带全部转换器的选项 |

默认格式参数：`dateFormat = "yyyy-MM-dd"`、`timeFormat = "HH:mm:ss"`、`isUtc = false`。

### 写出端的行为差异

| 类型 | 输出 |
| --- | --- |
| `long` / `long?` | **JSON 字符串**，避免 JS Number 精度溢出 |
| 其余数值类型 | JSON 数字 |
| `bool` | JSON 布尔 |
| `Guid` | 字符串 |
| `DateOnly` | `dateFormat`，默认 `2026-08-05` |
| `TimeOnly` | `timeFormat`，默认 `12:30:00` |
| `DateTime` | `"{dateFormat} {timeFormat}"`，默认 `2026-08-05 12:30:00` |
| `DateTimeOffset` | 固定 ISO 8601 带偏移 `yyyy-MM-ddTHH:mm:sszzz`，**不受 dateFormat / timeFormat 影响** |

### 读入端一律容错

所有转换器的读取端都接受字符串形式的数字与布尔：`"123"` 能读成 `int`，`"true"` 和 `1` 都能读成 `bool`。

::: danger 解析失败不报错，落默认值
读取端匹配不上时返回的是 `default`：`IntJsonConverter` 给 `0`，`GuidJsonConverter` 给 `Guid.Empty`，`DateTimeJsonConverter` 给 `default(DateTime)`，可空版给 `null`。

脏数据不会抛 `JsonException`，而是变成一个看起来正常的零值悄悄进入业务。对外部来源的 JSON，别指望这层帮你挡格式错误，该校验就在 DTO 上加验证特性。
:::

## Web 出入参约定

`XiHanWebCoreMvcOptions.ConfigureJsonOptionsDefault()` 是全框架 Web 接口的 JSON 事实标准，由 `XiHanWebApiServiceCollectionExtensions` 在装配 MVC 时调用。

| 设置 | 值 | 对接影响 |
| --- | --- | --- |
| `PropertyNamingPolicy` | `CamelCase` | `UserName` → `userName`；`OAuthProviders` → `oAuthProviders`（只有首字符变小写） |
| `DictionaryKeyPolicy` | `CamelCase` | 字典键同样转驼峰 |
| `DefaultIgnoreCondition` | `WhenWritingNull` | null 字段整个不出现，客户端按可选处理 |
| `PropertyNameCaseInsensitive` | `false` | **请求体属性名必须精确匹配驼峰**，大小写写错等于没传 |
| `ReferenceHandler` | `IgnoreCycles` | 循环引用不抛异常，输出 null |
| `NumberHandling` | `Strict` | 见下方说明 |
| `AllowTrailingCommas` | `true` | 请求体允许尾随逗号 |
| `ReadCommentHandling` | `Skip` | 请求体允许注释 |
| `Encoder` | `UnsafeRelaxedJsonEscaping` | 中文不转义成 `\uXXXX` |
| `WriteIndented` | `true` | 响应带缩进 |

转换器按这个顺序注册（顺序决定优先级）：

1. `DateTimeJsonConverter` / `DateTimeNullableConverter` / `DateTimeOffsetJsonConverter` / `DateTimeOffsetNullableConverter`，均传入时区解析委托；
2. `JsonStringEnumConverter`；
3. `ConfigureConverters()` 补齐其余全部转换器（含 `long` 转字符串）。

::: warning 时区版必须先注册
`System.Text.Json` 取**首个匹配类型**的转换器。时区感知的四个时间转换器必须排在 `ConfigureConverters()` 之前，否则会被后面的普通时间转换器抢先，换时区静默失效。自己往这份选项里加时间转换器时，注意别插到前面去。
:::

::: tip `NumberHandling = Strict` 为什么还能收字符串数字
自定义转换器完全接管了读取过程，`NumberHandling` 对被转换器覆盖的类型不起作用。所以 `int` / `long` / `decimal` 等仍然同时接受 `123` 和 `"123"`。
:::

## 时区感知的时间输出

存储统一 UTC，输出按请求头 `X-Timezone`（IANA 标识，如 `Asia/Shanghai`）换算。解析入口：

```csharp
private static string? ResolveUserTimeZone()
{
    return HttpContextAccessor.HttpContext?.Request.Headers["X-Timezone"].ToString();
}
```

| 类型 | 带 `X-Timezone` | 无 `X-Timezone` 或时区非法 |
| --- | --- | --- |
| `DateTime` | 值按 UTC 标记后 `ConvertTimeFromUtc`，输出 `2026-08-05 20:30:00` | 按原值输出 `yyyy-MM-dd HH:mm:ss` |
| `DateTimeOffset` | `ConvertTime` 后输出**无偏移墙钟** `2026-08-05 20:30:00` | 输出 ISO 8601 带偏移 `2026-08-05T12:30:00+00:00` |

两种类型换算后的输出格式一致，都是无偏移墙钟——前端拿到直接按本地显示即可，不会被浏览器时区二次偏移。时区标识解析失败时（`TimeZoneInfo.FindSystemTimeZoneById` 抛异常）静默回退到无换算行为，不会让请求失败。

::: warning 业务时间多为 `DateTimeOffset`
只覆盖 `DateTime` 是不够的。审计字段、业务时间字段大量使用 `DateTimeOffset`，它默认自带偏移，前端会按浏览器时区渲染，看上去"换时区没生效"。四个转换器要一起挂。
:::

## 枚举

全局挂了 `JsonStringEnumConverter`，枚举默认按**成员名**输出：`Status.Enabled` → `"Enabled"`。

需要某个枚举输出数字时用 `NumericEnumConverter<TEnum>`，但**标注位置决定它到底生效不生效**。

::: danger 转换器优先级：类型特性打不过全局集合
System.Text.Json 的优先级从高到低是：

1. 标在**属性**上的 `[JsonConverter]`
2. `JsonSerializerOptions.Converters` **集合**里的转换器
3. 标在**类型**（枚举/类）上的 `[JsonConverter]`

框架把 `JsonStringEnumConverter` 加进了第 2 层的集合，所以**标在枚举类型上的 `NumericEnumConverter` 会被压过、完全不生效**。

```csharp
// ❌ 标在类型上：被全局集合压过，仍输出成员名字符串
[JsonConverter(typeof(NumericEnumConverter<MyStatus>))]
public enum MyStatus { Ok = 200 }

// ✅ 标在属性上：优先级最高，确实输出数字
public class MyDto
{
    [JsonConverter(typeof(NumericEnumConverter<MyStatus>))]
    public MyStatus Status { get; set; }
}
```

要在整个应用范围内让某个枚举恒为数字，另一条路是把 `NumericEnumConverter<TEnum>` 也加进 `Converters` 集合，并确保它**排在 `JsonStringEnumConverter` 之前**（集合内先匹配者胜）——但顺序依赖脆弱，优先用属性级标注。
:::

框架的 `ApiResponse.Code` 就是属性级标注的实例：枚举 `ApiResponseCodes` 类型上也标了同一个转换器（供不经 Web 管道的场景使用），属性上再标一次保证 Web 管道下同样输出数字。

`NumericEnumConverter` 的读取端兼容数字、数字字符串和成员名三种来源，且解析不了时**会抛 `JsonException`**（与其他转换器的静默默认值不同）。

## 脱离 Web 管道用

`JsonHelper` 提供序列化、反序列化、节点操作、结构比较等静态方法，配套两个选项类：

```csharp
using XiHan.Framework.Utils.Serialization.Json;

var json = JsonHelper.Serialize(dto, JsonSerializeOptions.WebApi);
var back = JsonHelper.Deserialize<UserDto>(json, JsonDeserializeOptions.WebApi);

// 不抛异常的版本
if (JsonHelper.TryDeserialize<UserDto>(json, out var result)) { /* … */ }
```

| 类 | 预设 |
| --- | --- |
| `JsonSerializeOptions` | `Default` / `Compact` / `Formatted` / `Strict` / `WebApi` |
| `JsonDeserializeOptions` | `Default` / `Strict` / `Lenient` / `WebApi` |

两个 `WebApi` 预设都会带上 `JsonConverterFactory.GetAllConverters()`，与 Web 管道的类型行为对齐（但不含时区换算，那部分需要 HTTP 上下文）。

::: danger 部分选项属性当前不生效
`ToSystemOptions()` 只映射它显式列出的字段。以下属性**声明了但没有被消费**，设了也没有效果：

| 类 | 无效属性 |
| --- | --- |
| `JsonSerializeOptions` | `IgnoreNullValues`、`Encoding`（仅 `SerializeToFile` 用它写文件编码） |
| `JsonDeserializeOptions` | `IgnoreUnknownProperties`、`UseDefaultValues`、`MaxStringLength`、`MaxArrayLength` |

要省略 null，请直接设 `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`，不要依赖 `IgnoreNullValues`。未知属性 `System.Text.Json` 本来就默认忽略。

另外 `JsonErrorHandling.Log` 当前与 `Ignore` 行为相同（返回默认值，不写日志）。
:::

## 动态 JSON

结构不固定、不想为每种形状定义 DTO 时，用 `XiHan.Framework.Serialization` 的动态类型：

```csharp
using XiHan.Framework.Serialization.Dynamic;

dynamic node = DynamicJsonObject.Parse(payload);
var name = node.user.name;

// 点分路径读写嵌套属性
var city = DynamicJsonHelper.SelectToken(node, "user.profile.city");
DynamicJsonHelper.SetToken(node, "user.profile.city", "杭州");

// 流式构建
var built = DynamicJsonFactory.Object()
    .AddString("name", "曦寒")
    .AddNumber("level", 3)
    .AddObject("profile", b => b.AddString("city", "杭州"))
    .Build();
```

底层完全基于 `System.Text.Json.Nodes`，四个类型分别对应对象、数组、值、属性：`DynamicJsonObject` / `DynamicJsonArray` / `DynamicJsonValue` / `DynamicJsonProperty`。还提供深度合并 `Merge`、深度克隆 `DeepClone`、深度比较 `DeepEquals`、扁平化 `Flatten` / `Unflatten`，以及全套 `Try*` 无异常版本。

需要在已有选项基础上换掉某个转换器时，用 `JsonSerializerOptionsHelper`：

```csharp
var options = JsonSerializerOptionsHelper.Create(
    baseOptions,
    converter => converter is DateTimeJsonConverter,   // 移除谓词
    new DateTimeJsonConverter("yyyy/MM/dd HH:mm", false));  // 追加（已存在则跳过）
```

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 前端拿到的 `long` 是字符串 | 有意为之，`LongJsonConverter` 防 JS Number 精度溢出；反序列化数字与字符串都收 |
| 响应里某个字段整个不见了 | 值为 null，`DefaultIgnoreCondition = WhenWritingNull` 省略了它 |
| 请求体某字段收不到 | `PropertyNameCaseInsensitive = false`，属性名大小写必须与驼峰完全一致 |
| `oAuthProviders` 这种怪命名 | camelCase 策略只把首字符变小写，不拆词 |
| 换了 `X-Timezone` 时间没变 | 只覆盖了 `DateTime` 没覆盖 `DateTimeOffset`；或自加的时间转换器排到了时区版前面 |
| 时间格式忽然带上 `+08:00` | 请求没带 `X-Timezone`（或时区标识非法），`DateTimeOffset` 回退到 ISO 8601 带偏移 |
| 脏数据变成 0 / `Guid.Empty` / 默认时间 | 转换器读取失败静默落默认值，不抛异常 |
| 枚举返回的是数字不是名字 | 该 DTO **属性**上标了 `[JsonConverter(typeof(NumericEnumConverter<T>))]`——属性级优先级最高。标在枚举类型上则无效 |
| `IgnoreNullValues = true` 没效果 | 该属性未被 `ToSystemOptions()` 消费，改用 `DefaultIgnoreCondition` |
| `SetProperty` 写错属性名没报错 | 类型或属性名未在 `ObjectExtensionManager` 注册时，验证直接跳过 |
| `GetPropertiesAndCheckPolicyAsync` 没过滤掉任何属性 | `ExtensionPropertyPolicyChecker` 三个判定方法默认恒 `true`，需派生重写 |
| `GetProperty<T>` 抛 `XiHanException` | 泛型版只放行扩展原始类型（基元类型、枚举、`string`、`decimal`、`DateTime`、`DateTimeOffset`、`TimeSpan`、`Guid`） |
| 改了 DTO 字段名映射就空了 | Mapster 按名匹配，重写 `MapEntityToDtoAsync` 或注册 Mapster 映射规则 |

## 下一步

- [Web 应用开发](./web)：出入参约定在整条管道里的位置
- [动态 API](./dynamic-api)：应用服务怎么变成 REST 接口
- [数据访问](./data)：实体与仓储，映射的上游
- [ObjectMapping 包](../packages/objectmapping)：扩展属性完整 API 与类型清单
- [Serialization 包](../packages/serialization)：动态 JSON 完整 API
- [Utils 包](../packages/utils)：`JsonHelper` 与全部转换器清单
