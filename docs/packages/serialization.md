# XiHan.Framework.Serialization

> 序列化辅助库：核心是一套基于 `System.Text.Json` 的"动态 JSON"操作体系（API 形态对标 Newtonsoft.Json 的 JObject/JArray/JValue/JProperty）与 `JsonSerializerOptions` 组合工具；`Newtonsoft.Json` 仅作为项目文件声明的 NuGet 依赖，当前源码未实际调用。

- **NuGet**：`XiHan.Framework.Serialization`
- **模块类**：`XiHanSerializationModule`（当前不注册任何服务）
- **所在层**：基础设施层
- **关键依赖**：.NET 内置 `System.Text.Json`（唯一实际使用的序列化引擎）；`Newtonsoft.Json`（项目文件中声明的 NuGet 引用，源码未实际调用）

## 概述

本包核心交付物是一套**动态 JSON** 类型：`DynamicJsonObject` / `DynamicJsonArray` / `DynamicJsonValue` / `DynamicJsonProperty`，API 形态对标 Newtonsoft.Json 的 `JObject`/`JArray`/`JValue`/`JProperty`，但底层完全基于 .NET 内置 `System.Text.Json.Nodes`（`JsonObject`/`JsonArray`/`JsonValue`）承载——项目虽通过 NuGet 声明了 `Newtonsoft.Json` 依赖，但当前源码没有任何类型实际引用或调用它（无 `using Newtonsoft.*`）。它们继承自 `DynamicObject`，支持动态成员访问与索引，让你在不定义强类型 DTO 的前提下解析、构建、查询、合并 JSON。

围绕这些类型还提供了 `DynamicJsonHelper`（静态辅助：序列化/反序列化/路径查询/合并/扁平化）、`DynamicJsonFactory`（工厂 + 流式构建器）、`DynamicJsonExtensions`（快捷扩展方法）以及 `JsonSerializerOptionsHelper`（组合 `JsonSerializerOptions`）。

## 何时使用

- 需要处理结构不固定的 JSON（配置片段、第三方响应），不想为每种形状定义 DTO。
- 想用点分路径（如 `user.profile.name`）读写嵌套属性。
- 需要对两份 JSON 做深度合并、深度克隆或结构比较。
- 需要在运行时动态增删 `JsonConverter` 来派生 `JsonSerializerOptions`。

> 若只需常规强类型 JSON/XML/YAML 序列化，`XiHan.Framework.Utils` 的 `Serialization` 目录（`JsonHelper` 等）已够用——本包实际也复用了 `Utils` 的 `JsonHelper`。本包的价值在"动态、免 DTO"的场景。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Serialization
```

```csharp
[DependsOn(typeof(XiHanSerializationModule))]
public class MyModule : XiHanModule;
```

`XiHanSerializationModule.ConfigureServices` 当前是空实现（仅取了一次 `IConfiguration`，未注册任何服务）。因此本包能力**全部通过静态 `Helper` / `Factory` / 扩展方法直接调用**，无需 DI，也无需依赖模块类即可使用这些静态类型（`[DependsOn]` 主要用于纳入模块图与传递依赖）。

## 核心能力

- **动态 JSON 类型**：`DynamicJsonObject` / `DynamicJsonArray` / `DynamicJsonValue` / `DynamicJsonProperty`，动态成员访问 + 索引器。
- **解析与序列化**：字符串解析、对象互转、文件读写、同步 + 异步 + `Try*` 无异常版本。
- **路径查询**：`SelectToken` / `SetToken` 以点分路径读写嵌套属性（数组下标也可用）。
- **结构化操作**：`Merge`（深度合并）、`DeepClone`（深度克隆）、`DeepEquals`（深度比较）、`Flatten` / `Unflatten`（扁平化互转）。
- **构建器模式**：`DynamicJsonFactory.Object()` / `.Array()` 流式构建，支持嵌套 `AddObject` / `AddArray`。
- **序列化选项管理**：`JsonSerializerOptionsHelper` 以基础 `JsonSerializerOptions` 为模板，移除指定/匹配的 `JsonConverter` 并追加新的（去重）。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `DynamicJsonObject` | 动态 JSON 对象，类似 `JObject`；继承 `DynamicObject`，支持动态成员/索引、`Properties`、`ContainsKey`、`GetValue`/`SetValue` |
| `DynamicJsonArray` | 动态 JSON 数组，类似 `JArray`，实现 `IList<object?>`；另提供 `First`/`Last`、`Values<T>()`、`Objects()`/`Arrays()`（按类型筛选子元素）、`FirstObject(predicate?)`、`SelectTokens(propertyName)` 等 LINQ 风格辅助方法 |
| `DynamicJsonValue` | 动态 JSON 值，类似 `JValue`；继承 `DynamicObject`，实现 `IEquatable<DynamicJsonValue>`，暴露 `ValueKind`、`ToObject<T>()`、`TryGetValue<T>(out T?)`，并带大量隐式/显式转换运算符 |
| `DynamicJsonProperty` | 动态 JSON 属性（键值对），类似 `JProperty`；暴露 `Name`/`Value`、`DynamicValue`（转为 `DynamicJsonValue`）、`AsObject`/`AsArray`（值为对象/数组时的强类型视图） |
| `DynamicJsonHelper` | 静态辅助：`Serialize`/`Deserialize`（+ `Async`/`Try*`/文件版本）、`FromObject`/`ToObject<T>`、`SelectToken`/`SetToken`、`DeepClone`、`Merge`、`DeepEquals`、`Flatten`/`Unflatten`、`IsValid`/`IsEmpty`/`HasProperty` 等 |
| `DynamicJsonFactory` | 静态工厂：`CreateObject`/`CreateArray`/`CreateString`/`CreateNumber`/…、`Parse`/`TryParse`、`FromObject`，以及 `Object()`/`Array()` 返回的 `ObjectBuilder`/`ArrayBuilder` |
| `DynamicJsonExtensions` | 扩展方法：`ToDynamic`/`ToDynamicJsonObject`/`ToDynamicJsonArray`（`string`/`object`/`JsonNode`/字典/集合）、`ToDictionary`/`ToList`、`SelectToken`/`SetToken`、`DeepMerge`、`Flatten`、`HasPath`、`IsDeepEmpty` |
| `JsonSerializerOptionsHelper` | 静态：`Create(baseOptions, JsonConverter removeConverter, params JsonConverter[] addConverters)` 与 `Create(baseOptions, Func<JsonConverter,bool> removeConverterPredicate, params JsonConverter[] addConverters)` |

### `DynamicJsonHelper` 关键方法（节选）

| 方法 | 说明 |
| --- | --- |
| `string Serialize(object? dynamicJson, bool indented = true)` / `Task<string> SerializeAsync(...)` | 序列化动态 JSON（识别四种动态类型，其余走 `JsonHelper`） |
| `dynamic? Deserialize(string json)` / `DeserializeAsync(...)` / `T? DeserializeAs<T>(string json)` | 反序列化为动态对象；空串抛 `ArgumentException`，格式非法抛 `JsonException` |
| `dynamic? DeserializeFromFile(string filePath)` / `void SerializeToFile(object?, string, bool)`（+ Async） | 文件读写（UTF-8，自动建目录） |
| `dynamic? FromObject(object? obj)` / `T? ToObject<T>(object? dynamicJson)` | 普通对象 ↔ 动态 JSON 互转 |
| `dynamic? SelectToken(object?, string path, char separator = '.')` | 点分路径查询（支持数组下标） |
| `void SetToken(object?, string path, object? value, char separator = '.', bool createPath = true)` | 点分路径写入，`createPath` 控制是否自动建中间层 |
| `dynamic? DeepClone(object?)` | 序列化再反序列化实现深克隆 |
| `DynamicJsonObject? Merge(DynamicJsonObject? target, DynamicJsonObject? source, bool overwrite = true)` | 递归深度合并 |
| `bool DeepEquals(object? left, object? right)` | 结构化深度比较 |
| `Dictionary<string, object?> Flatten(object?, string separator = ".")` / `DynamicJsonObject? Unflatten(...)` | 扁平化互转（数组用 `key[i]` 形式） |
| `bool IsValid(...)` / `bool IsType<T>(object?)` / `bool IsEmpty(...)` / `bool HasProperty(object?, string)` | 校验类辅助 |
| `string Format(object?, bool indent = true)` / `string Compress(object?)` | 借助 `JsonHelper.FormatJson` 格式化/压缩；`Compress` 等价于 `Format(json, false)`；序列化异常时回退到 `ToString()` |
| `string GetTypeInfo(object?)` | 返回类型描述字符串（如 `DynamicJsonValue(String)`），便于日志/调试 |
| `(int PropertyCount, int TotalSize) GetSizeInfo(object?)` | 粗粒度体积估算（非精确字节数） |

`Try*` 系列（`TrySerialize` / `TryDeserialize` / `TryDeserializeAs<T>` / `TryFromObject` / `TryToObject<T>` / `TryDeserializeFromFile` / `TrySerializeToFile`）为不抛异常版本，以 `bool` + `out` 返回结果。

## 使用示例

### 解析、路径读写与序列化

```csharp
using XiHan.Framework.Serialization.Dynamic;

dynamic? json = DynamicJsonHelper.Deserialize("""{ "user": { "profile": { "name": "Ann" } } }""");

// 点分路径读
var name = DynamicJsonHelper.SelectToken(json, "user.profile.name"); // Ann

// 点分路径写（自动创建中间层）
DynamicJsonHelper.SetToken(json, "user.profile.age", 18);

var text = DynamicJsonHelper.Serialize(json, indented: true);
```

### 流式构建

```csharp
var obj = DynamicJsonFactory.Object()
    .Add("id", 1001)
    .AddString("name", "Ann")
    .AddArray("tags", a => a.AddString("admin").AddString("vip"))
    .AddObject("profile", p => p.AddNumber("age", 18))
    .Build();
```

### 深度合并与比较

```csharp
var merged = DynamicJsonHelper.Merge(target, source, overwrite: true);
var same = DynamicJsonHelper.DeepEquals(a, b);
```

### 组合 `JsonSerializerOptions`

```csharp
// 以既有 options 为基础，移除某个转换器并追加新的（不重复添加）
var options = JsonSerializerOptionsHelper.Create(
    baseOptions,
    removeConverterPredicate: c => c is JsonStringEnumConverter,
    addConverters: new MyDateConverter());
```

## 注意事项与最佳实践

- **无 DI 注册**：模块类不注册服务，所有能力经静态类型调用；不要期望从容器解析本包的服务。
- **底层是 `System.Text.Json.Nodes`**：动态类型基于 `JsonObject`/`JsonArray`/`JsonValue`，`DynamicJsonValue.ValueKind` 即 `JsonValueKind`。项目虽通过 NuGet 声明了 `Newtonsoft.Json` 依赖，但当前源码没有任何类型实际引用它——仅 API 形态（`JObject`/`JArray`/`JValue`/`JProperty`）对标 Newtonsoft，实现完全基于 STJ。
- **异步是包装同步**：`SerializeAsync` / `DeserializeAsync` 等以 `Task.Run` 包裹同步实现，本质是 CPU 工作转线程池，非真正的 IO 异步。
- **`Deserialize` 的异常语义**：空字符串抛 `ArgumentException`，JSON 非法抛 `JsonException`；不想处理异常时用对应 `Try*` 版本。
- **`SelectToken`/`SetToken` 的入参约束**：仅当根为 `DynamicJsonObject` 时生效（`SelectToken` 支持路径中出现数组下标）。

## 依赖模块

- 内部依赖：仅 [XiHan.Framework.Core](./core)（另运行期复用 `XiHan.Framework.Utils` 的 `JsonHelper`）。
- 第三方核心：.NET 内置 `System.Text.Json`（唯一实际使用的序列化引擎）；`Newtonsoft.Json` 为项目文件中声明的 NuGet 引用，当前源码未实际使用。

## 相关模块

- [XiHan.Framework.Utils](./utils) — 其 `Serialization` 目录提供 JSON/XML/YAML 的基础序列化封装（`JsonHelper` 等），本包的动态类型即建立其上。
- [XiHan.Framework.Core](./core)
