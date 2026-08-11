# XiHan.Framework.Utils

> 框架最底层的零依赖通用工具库：加密/哈希、序列化、反射、集合/LINQ、文件 IO、网络、时间、本地化、缓存、日志、诊断、控制台工具等，全部以静态 Helper 与扩展方法形式提供。

- **NuGet**：`XiHan.Framework.Utils`
- **模块类**：—（无模块类，直接引用静态方法即可）
- **所在层**：公共层
- **关键依赖**：仅 .NET 原生 BCL（编译期引用 `XiHan.Framework.Analyzers` 做文件头规范检查），运行时零第三方依赖

## 概述

`XiHan.Framework.Utils` 是整个曦寒框架的地基。它把日常开发中反复要写的杂活——加密、序列化、反射、集合处理、文件读写、时间换算、字符串脱敏、重试、缓存等——封装成一组**公共静态 Helper 类**与**扩展方法**。

它的两个核心特征：

- **零运行时依赖**：`csproj` 里没有任何 `PackageReference`，所有能力都构建在 BCL 之上（`System.Text.Json`、`System.Xml`、`System.Security.Cryptography`、`System.Net` 等）。连 YAML 也是手写解析器而非 YamlDotNet。这让它能被任何层安全引用，不会引入版本冲突。
- **无模块类、无 DI**：它不是一个 ABP 风格的框架模块，没有 `XiHanUtilsModule`，不需要 `[DependsOn]`，也不需要注册服务。直接调用静态方法或扩展方法即可。

在框架内部它被 `XiHan.Framework.Core`、`XiHan.Framework.Domain.Shared`、`XiHan.Framework.Localization`、`XiHan.Framework.Templating` 直接引用；由于 `Core` 又被几乎所有上层包引用，因此 Utils **事实上被框架各层间接引用**，是使用频率最高的基础包。

## 何时使用

- 需要现成的加密/哈希（AES/RSA/ECDSA/HMAC/SHA/MD5）、GUID、OTP、掩码脱敏、密码强度校验。
- 想用扩展方法简化字符串、集合、字典、枚举、日期时间、类型反射的常见操作。
- JSON / XML / YAML 的序列化与反序列化（BCL 底座，带丰富 Options 与自定义转换器）。
- 处理文件、目录、路径、流、压缩，或做 DNS / Ping / HTTP / WebSocket / SSE 等网络辅助。
- 时间区间、农历换算、计时打点、本地化文化/货币/时区处理。
- 进程内轻量缓存、控制台日志、参数守卫（Guard）、重试策略、硬件信息采集。

不该用它的场景：需要 DI 生命周期、分布式缓存、结构化日志管道、多租户等基础设施能力时，应使用对应的框架基础设施包（`Caching`、`Logging`、`Data` 等），而不是这里的进程内静态工具。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Utils
```

纯工具库，**直接引用即可**，无需 `[DependsOn]`、无需注册服务。引入命名空间后调用静态 Helper 或扩展方法：

```csharp
using XiHan.Framework.Utils.Reflections;
using XiHan.Framework.Utils.Security.Cryptography;
using XiHan.Framework.Utils.Extensions;

var version = ReflectionHelper.GetEntryAssemblyVersion();
var hash = HashHelper.Sha256("hello");
var camel = "user_name".ToCamelCase();
```

> 项目全局启用了 `ImplicitUsings` 与 `Nullable`（可空引用类型），目标框架 `net10.0`。签名中的 `?` 均表示可空。

## 包结构总览

以功能分类组织，各目录下均为公共静态类或扩展类（少数为可实例化类型，已在下文标注）：

| 分类目录 | 主题 | 代表类型 |
| --- | --- | --- |
| `Core` | 核心杂项工具 | `ConvertHelper`、`StringHelper`、`ValidateHelper`、`TypeHelper`、`EnumHelper`、`RandomHelper`、`RegexHelper`、`DateTimeHelper`、`MathHelper`、`ArrayHelper`、`CloneHelper`、`CompareHelper`、`LogicHelper`、`SystemInfoManager` |
| `Extensions` | 通用扩展方法 | `StringExtensions`、`EnumExtensions`、`DateTimeExtensions`、`TypeExtensions`、`EncodingExtensions`、`ComparableExtensions`、`ConverterExtensions`、`GenericExtensions`、`LockExtensions`、`StreamExtensions` |
| `Collections` | 集合扩展 | `EnumerableExtensions`、`ListExtensions`、`DictionaryExtensions`、`CollectionExtensions`、`QueueExtensions`、`StackExtensions`、`LinkedListExtensions`、`TreeExtensions` |
| `Linq` | 表达式/谓词 | `PredicateComposer`、`ExpressionExtensions`、`ExpressionBuilder<T>`、`QueryableExtensions` |
| `Reflections` | 反射辅助 | `ReflectionHelper`、`MemberInfoExtensions`、`PropertyInfoExtensions`、`MethodInfoExtensions`、`FieldInfoExtensions` |
| `Objects` | 对象辅助 | `ObjectExtensions`、`DeepMergeHelper` |
| `Security/Cryptography` | 加解密/哈希 | `AesHelper`、`DesHelper`、`RsaHelper`、`EcdsaHelper`、`DsaHelper`、`EciesHelper`、`HashHelper`、`HmacHelper` |
| `Security` | 安全杂项 | `GuidHelper`、`MaskHelper`、`OtpHelper`、`PasswordStrengthChecker`、`RandomCoder`、`MorseHelper`、`TextWatermarkHelper`、`ErrorObfuscationHelper` |
| `Serialization` | 序列化 | `JsonHelper`、`XmlHelper`、`YamlHelper` 及各自 `*Extensions` / `*Options` |
| `IO` | 文件/目录/流 | `FileHelper`、`DirectoryHelper`、`PathHelper`、`StreamHelper`、`CompressHelper`、`FileFormatExtensions` |
| `Net` | 网络 | `DnsHelper`、`PingHelper`、`HttpClientHelper`、`MimeTypes`、`IpFormatExtensions`、`SseClient`/`SseServer`、`WebSocketClient` |
| `Timing` | 时间 | `DateTimeRange`、`LunarCalendarHelper`、`StopwatchHelper` |
| `Localization` | 本地化 | `CultureHelper`、`CurrencyHelper`、`TimeZoneHelper`、`I18nFormatHelper`、`LanguageHelper` |
| `Maths` | 数学 | `NumberExtensions`、`MoneyFormatExtensions`、`PredictHelper` |
| `Converters` | 进制编码 | `Base32`、`Base36`、`Base58`、`Base62`、`Base95`、`CustomRadix` |
| `Caching` | 进程内缓存 | `CacheHelper`、`CacheOptions`、`CacheStatistics`、`CacheItem<T>` |
| `Logging` | 控制台/文件日志 | `LogHelper`、`LogOptions`、`LogFileHelper`、`ILogFormatter` 及格式化器 |
| `Diagnostics` | 守卫/重试/硬件 | `Guard`、`RetryPolicy`/`RetryPolicyFactory`、`CpuHelper`/`RamHelper`/`DiskHelper`/`GpuHelper`/`BoardHelper`/`NetworkHelper`、`EventHandlerExtensions` |
| `Threading` | 异步原语 | `Debouncer`、`AsyncBarrier`、`AsyncReaderWriterLock`、`DisposeAction`、`AsyncDisposeFunc`、`NullDisposable`、`NullAsyncDisposable` |
| `ConsoleTools` | 控制台 UI | `ConsoleTable`、`ConsoleProgressBar`、`ConsoleSpinner`、`ConsoleMenu`、`ConsolePrompt`、`ConsoleColorWriter` |
| `CommandLine` | 命令行 | `ShellHelper`、`ScriptExecutor` |
| `Runtime` | 运行时 | `OsPlatformHelper`、`RuntimeMonitor` |
| `Enums` | 枚举元数据 | `EnumInfo`、`EnumItem`、`EnumAttributes`、`EnumThemes` |
| `Constants` / `Exceptions` | 常量/异常 | `DefaultConsts`、`CustomException` |

## 核心能力（按分类详解）

### 一、核心杂项 `Core`

框架里最常用的一批基础 Helper，全部为 `public static`。

**`ConvertHelper`** — 带默认值兜底的类型转换：

```csharp
public static T ConvertTo<T>(object? value, T defaultValue = default!)
public static bool TryConvertTo<T>(object? value, out T result)
public static object? ConvertTo(object? value, Type targetType, object? defaultValue = null)
public static bool ToBool(object? value, bool defaultValue = false)
public static int ToInt(object? value, int defaultValue = 0)
public static double ToDouble(object? value, double defaultValue = 0.0)
public static decimal ToDecimal(object? value, decimal defaultValue = 0m)
```

**`StringHelper`** — 字符串分割/拼接/清洗：

```csharp
public static List<string> GetStrList(string sourceStr, char sepeater = ',', bool isAllowsDuplicates = true)
public static string GetListStr(IEnumerable<string> sourceList, char sepeater = ',', bool isAllowsDuplicates = true)
public static string ClipString(string inputString, int len)
public static string HtmlToTxt(string strHtml)
public static string FirstToUpper(string value)
```

**`ValidateHelper`** — 一站式输入校验（内部走 `RegexHelper`）：

```csharp
public static bool IsGuid(string checkValue)
public static bool IsEmail(string checkValue)
public static bool IsNumber(string checkValue)     // 另有 IsInt / IsNumberIntOrDouble
public static bool IsUrl(string checkValue)         // 另有 IsUri
public static bool IsIp(string checkValue)          // 另有 IsIpv4 / IsIpv6
public static bool IsJson(string checkValue)
public static bool IsNumberPeople(string checkValue) // 15/18 位身份证号
```

**`TypeHelper`** — 类型判定与泛型处理：

```csharp
public static bool IsNullable(Type type)
public static bool IsPrimitiveExtended(Type type, bool includeNullables = true, bool includeEnums = false)
public static bool IsEnumerable(Type type, out Type? itemType, bool includePrimitives = true)
public static bool IsDictionary(Type type, out Type? keyType, out Type? valueType)
public static string GetFullNameHandlingNullableAndGenerics(Type type)
public static object? ConvertFromString(Type targetType, string? value)
```

**`EnumHelper`** — 枚举反射 + 元数据 + 缓存（返回下文 `Enums` 目录的 `EnumItem` / `EnumInfo`，内部以 `ConcurrentDictionary` 缓存反射结果）：

```csharp
public static List<EnumItem> GetEnumItems<TEnum>(bool includeHidden = false, bool ordered = true) where TEnum : struct, Enum
public static List<EnumItem<TEnum>> GetTypedEnumItems<TEnum>(bool includeHidden = false, bool ordered = true) where TEnum : struct, Enum
public static EnumInfo GetEnumInfo<TEnum>() where TEnum : struct, Enum
public static TEnum Parse<TEnum>(string value, bool ignoreCase = true) where TEnum : struct, Enum
public static bool TryParse<TEnum>(string? value, out TEnum result, bool ignoreCase = true) where TEnum : struct, Enum
public static string GetDescription<TEnum>(TEnum value) where TEnum : struct, Enum
public static string? GetLocalizationKey<TEnum>(TEnum value) where TEnum : struct, Enum
public static string? GetLocalizationResourceName<TEnum>(TEnum value) where TEnum : struct, Enum
public static void ClearCache<TEnum>() where TEnum : struct, Enum
```

**`RandomHelper`** — 基于 `Random.Shared` 的**非加密**随机（加密安全场景请用 `RandomCoder`）：

```csharp
public static string GetRandom(int length, string source)
public static int GetRandom(int minValue, int maxValue)
public static T GetRandomOf<T>(params T[] objs)
public static List<T> GenerateRandomizedList<T>(IEnumerable<T> items)   // Fisher-Yates
```

**`RegexHelper`**（`public static partial`，源生成器编译正则）暴露 65 个 `partial Regex` 工厂属性，如 `EmailRegex()`、`IpRegex()`、`Ipv6Regex()`、`UrlRegex()`、`StrongPasswordRegex()`、`HexColorRegex()`、`Iso8601DateTimeRegex()`、`WindowsPathRegex()` 等，另有 `IsMatch(string input, string pattern, RegexOptions options = RegexOptions.IgnoreCase)`。

其余：`DateTimeHelper`（工作日/年龄/友好时间/月边界）、`MathHelper`（几何/统计/质数/斐波那契）、`ArrayHelper`（`Find`/`Insert`/`Remove`/`Shuffle`）、`CloneHelper`（`DeepCopy` / `CopyProperties` / `MapObject`）、`CompareHelper`（`DeepEquals` / `GetHashCodeDeep`）、`LogicHelper`（函数式 `If`/`Switch`/`TryExecute`/`RetryAsync`）、`SystemInfoManager`（`GetSystemInfo()` 汇总硬件+运行时）。

### 二、扩展方法 `Extensions`

**`StringExtensions`** — 使用频率最高的一组：

```csharp
public static bool IsNullOrEmpty([NotNullWhen(false)] this string? str)
public static string EnsureEndsWith(this string str, char c, StringComparison comparisonType = StringComparison.Ordinal)
public static string Left(this string str, int len)
public static string Right(this string str, int len)
public static string ToCamelCase(this string str, bool useCurrentCulture = false, bool handleAbbreviations = false)
public static string ToSnakeCase(this string str)
public static string ToSentenceCase(this string str, bool useCurrentCulture = false)
```

**`EnumExtensions`** — 枚举实例上的便捷方法：

```csharp
public static string GetDescription<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
public static string GetDisplayName<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
public static int ToInt<TEnum>(this TEnum enumValue) where TEnum : struct, Enum
public static TEnum AddFlag<TEnum>(this TEnum enumValue, TEnum flag) where TEnum : struct, Enum
public static TEnum? GetNext<TEnum>(this TEnum enumValue, bool loop = false) where TEnum : struct, Enum
```

**`DateTimeExtensions`**：`GetDateToTimeStamp`、`GetDayMinDate` / `GetDayMaxDate`、`GetDayDateRange`、`GetWeekByDate`、`FormatDateTimeToString` / `FormatDateTimeToEasyString`。

**`EncodingExtensions`**：`Base64Encode` / `Base64Decode`、`HtmlEncode`、`UrlEncode`、`UnicodeEncode`。

**`ComparableExtensions`**：`Clamp<T>`、`IsInRange<T>`、`IsBetween<T>`、`Max<T>` / `Min<T>`（`where T : IComparable<T>`）。

其余：`ConverterExtensions`（`this` 版转换）、`TypeExtensions`（`IsNullableType` / `IsAssignableTo<T>` / `GetBaseClasses`）、`GenericExtensions`（`GetGenericTypeName` / 反射读写属性）、`LockExtensions`（`Lock` / `ReadLock` / `TryLock`）、`StreamExtensions`（`GetAllBytesAsync` / `CreateMemoryStreamAsync`）。

### 三、集合与 LINQ `Collections` / `Linq`

**`EnumerableExtensions`** — 高频：

```csharp
public static string JoinAsString<T>(this IEnumerable<T> source, string separator)
public static IEnumerable<T> WhereIf<T>(this IEnumerable<T> source, bool condition, Func<T, bool> predicate)
public static T GetRandom<T>(this IEnumerable<T> source)
public static List<T> SortByDependencies<T>(this IEnumerable<T> source, Func<T, IEnumerable<T>> getDependencies, IEqualityComparer<T>? comparer = null) where T : notnull   // 拓扑排序
```

**`DictionaryExtensions`**：`GetOrDefault<TKey,TValue>`、`GetOrAdd<TKey,TValue>(..., Func<TKey,TValue> factory)`、`BuildQueryString`、`RemoveByKeys`。

**`ListExtensions`**：`InsertRange`、`AddFirst` / `AddLast`、`InsertAfter`、`ReplaceWhile`、`MoveItem`、`GetOrAdd`。

**`TreeExtensions`**：`ToTree<T>(..., Func<T,object> keySelector, Func<T,object> parentKeySelector)` 把扁平列表建成树，配 `DepthFirstTraversal` / `BreadthFirstTraversal`。

`Linq/Expressions` 提供动态谓词组合，常用于仓储/查询构造：

```csharp
// PredicateComposer
public static Expression<Func<T, bool>> True<T>()
public static Expression<Func<T, bool>> And<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
public static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)

// QueryableExtensions
public static IQueryable<T> WhereIf<T>(this IQueryable<T> source, bool condition, Expression<Func<T, bool>> predicate)

// ExpressionExtensions
public static IOrderedQueryable<T> OrderBy<T>(this IQueryable<T> source, string propertyName, bool ascending = true)
```

`ExpressionBuilder<T>` 为**可实例化**的流式表达式构建器（`Property(...)`.`Equal(...)` 链式构造 `Expression<Func<T,bool>>`）。

### 四、反射 `Reflections`

**`ReflectionHelper`**（`public static`）是框架各处程序集/类型扫描的入口：

```csharp
public static Assembly? GetEntryAssembly()
public static Version? GetEntryAssemblyVersion()
public static IEnumerable<Assembly> GetAllAssemblies()
public static IEnumerable<Type> GetXiHanTypes()
public static bool IsAssignableToGenericType(Type givenType, Type genericType)
public static List<Type> GetImplementedGenericTypes(Type givenType, Type genericType)
public static TAttribute? GetSingleAttributeOrDefault<TAttribute>(MemberInfo memberInfo, TAttribute? defaultValue = default, bool inherit = true) where TAttribute : Attribute
public static IEnumerable<Type> GetSubClasses<T>() where T : class
```

扩展类：`MemberInfoExtensions`（`GetDescription` / `HasAttribute<T>` / `GetSingleAttributeOrNull<T>`）、`PropertyInfoExtensions`（`IsVirtual` / `GetPropertyName` / `GetPropertyInfo` 支持 PascalCase/snake_case/kebab-case 灵活匹配）、`MethodInfoExtensions`（`IsAsync` / `IsOverridden`）、`FieldInfoExtensions`（`GetDescriptionValue` / `GetDisplayValue`）。

### 五、对象 `Objects`

**`ObjectExtensions`**（`public static`）：

```csharp
public static T As<T>(this object obj) where T : class            // 简单强转
public static T To<T>(this object obj) where T : struct           // Convert.ChangeType
public static bool IsIn<T>(this T item, params T[] list)
public static T If<T>(this T obj, bool condition, Func<T, T> func) // 条件式链式转换
public static bool IsNullOrEmpty(this object? data)
```

**`DeepMergeHelper`**：`DeepMerge<T>(params T[]? configs) where T : class, new()`，按优先级深度合并多个配置对象（保留高优先级非空值，递归合并嵌套对象/集合/字典）。

### 六、安全与加密 `Security`

加密类均为 `public static`，构建于 `System.Security.Cryptography`。

**`HashHelper`** — 哈希/摘要：

```csharp
public static string Md5(string input)
public static string Sha1(string data)
public static string Sha256(string data)
public static string Sha512(string data)
public static string StreamMd5(string inputPath)
public static string StreamHash(Stream data)
```

**`AesHelper`** — 对称加密（口令派生或显式 key/iv）：

```csharp
public static string Encrypt(string plainText, string password)
public static string Encrypt(string plainText, string key, string iv)
public static string Decrypt(string cipherText, string password)
public static byte[] EncryptBytes(byte[] plainBytes, byte[] keyBytes, byte[] ivBytes)
```

**`RsaHelper`** — 非对称加密 + 签名（含 RSA+AES 混合加密解决长文本限制）：

```csharp
public static (string publicKey, string privateKey) GenerateKeys(int keySize = 2048)
public static string Encrypt(string plainText, string publicKey, RSAEncryptionPadding? padding = null, int? blockSize = null)
public static string Decrypt(string cipherText, string privateKey, RSAEncryptionPadding? padding = null)
public static string EncryptWithAes(string plainText, string publicKey)   // 混合加密
public static string SignData(string data, string privateKey, HashAlgorithmName? hashAlgorithm = null, RSASignaturePadding? padding = null)
```

**`HmacHelper`**：`HmacSha1` / `HmacSha256`（`string` 与 `byte[]` 双重载）、`ComputeHmac(string algorithm, string key, string data)`。

签名专用：**`EcdsaHelper`** / **`DsaHelper`** 均提供 `GenerateKeys()` / `SignData()` / `VerifyData()`（仅签名验签，不加密）；**`EciesHelper`** 提供 `GenerateKeyPair()` / `Encrypt()` / `Decrypt()`（ECC 密钥交换 + AES 的集成加密）；**`DesHelper`** 为遗留算法，不建议生产使用。

安全杂项：

- **`GuidHelper`**：`NewGuid()`、`NewCryptoGuid()`、`NewTimeBasedGuid()`、`NewDeterministicGuid(string input, Guid? namespaceGuid = null)`（确定性 GUID）、`IsValidGuid(string)`。
- **`MaskHelper`**（脱敏，注意 `Mask` 是**扩展方法**）：

  ```csharp
  public static string Mask(this string input, int frontCount, int endCount, char? maskChar = '*')
  public static string MaskPhone(string phone)
  public static string MaskIdCard(string idCard)
  public static string MaskEmail(string email)
  public static string MaskChineseName(string name)
  ```

- **`OtpHelper`**（TOTP/HOTP 双因子）：`GenerateSecretKey(...)`、`GenerateTotp(string secretKey, int digits = 6, int step = 30, bool useBase32 = true)`、`VerifyTotp(...)`、`GenerateTotpUri(...)`（生成可扫码 URI）。
- **`RandomCoder`**（**加密安全**随机码，验证码/临时密码用它而非 `RandomHelper`）：`GetNumber(...)`、`GetLetter(...)`、`GetStrongPassword(...)`、`GetCustom(...)`、`GetChineseCharacters(...)`。
- **`PasswordStrengthChecker`**：`CheckPasswordStrength(string password, IEnumerable<string>? customBlacklist = null)`、`GeneratePassword(int length = 12, bool includeSpecialChars = true)`。
- **`MorseHelper`**：`Encode` / `Decode` 摩斯电码。
- **`TextWatermarkHelper`**：用零宽字符在文本中嵌入不可见水印，`EmbedWatermark` / `ExtractWatermark` / `ContainsWatermark`，另有泛型 `EmbedMetadata<T>` / `ExtractMetadata<T>`。
- **`ErrorObfuscationHelper`**（`Security/ErrorObfuscation`）：在检测到可疑 IP 时生成伪造错误页迷惑攻击者，支持 JSON/XML/HTML/纯文本多格式与多语言（C#/Java/PHP/Go/Python/Node.js/Ruby/Rust）伪堆栈模板。

### 七、序列化 `Serialization`

三种格式（JSON/XML/YAML）API 高度对称，均提供 `Helper` 静态类、`*Extensions` 扩展类、以及独立的序列化/反序列化 `*Options`。**全部基于 BCL**：JSON 走 `System.Text.Json`，XML 走 `System.Xml` / `System.Xml.Linq`，YAML 为**手写解析器**（内部借道 `System.Text.Json` 做类型转换，无 YamlDotNet 依赖）。

统一的 `Try` / 非 `Try` 双风格（后者失败抛异常，前者返回 `bool`），并带文件 I/O 重载：

```csharp
// JsonHelper（XmlHelper / YamlHelper 同构）
public static string Serialize<T>(T obj, JsonSerializeOptions? options = null)
public static T Deserialize<T>(string json, JsonDeserializeOptions? options = null)
public static void SerializeToFile<T>(T obj, string filePath, JsonSerializeOptions? options = null)
public static T DeserializeFromFile<T>(string filePath, JsonDeserializeOptions? options = null)
public static bool TrySerialize<T>(T obj, out string? result, JsonSerializeOptions? options = null)
public static bool TryDeserialize<T>(string json, out T? result, JsonDeserializeOptions? options = null)
```

扩展方法风格（`JsonExtensions` / `XmlExtensions` / `YamlExtensions`）：

```csharp
public static string ToJson(this object obj, JsonSerializeOptions? options = null)
public static T FromJson<T>(this string json, JsonDeserializeOptions? options = null)
public static bool IsValidJson(this string json)
public static string FormatJson(this string json, bool indent = true)  // 美化
public static string CompressJson(this string json)                    // 压缩
```

`XmlExtensions` 额外提供 XPath 查询（`QueryNode` / `QueryNodes` / `QueryNodeAttribute`）与 XML→Dictionary / XML→JSON 转换；`YamlExtensions` 提供 `ParseYaml` / `ParseNestedYaml` / YAML↔JSON 转换。

`Json/Converters` 目录内置 15 个 `System.Text.Json` 转换器（`BooleanJsonConverter`、`DateTimeJsonConverter`、`DateTimeOffsetJsonConverter`、`DecimalJsonConverter`、`GuidJsonConverter`、`NumericEnumConverter` 等，均含可空变体），可通过 `JsonSerializeOptions.CustomConverters` 挂载或经 `JsonConverterFactory` 批量取用。

`JsonSerializeOptions` 常用字段（节选，均为源码内联默认值）：

| 字段 | 类型 | 默认值 |
| --- | --- | --- |
| `WriteIndented` | `bool` | `true` |
| `PropertyNamingPolicy` | `JsonNamingPolicy?` | `CamelCase` |
| `PropertyNameCaseInsensitive` | `bool` | `true` |
| `AllowTrailingCommas` | `bool` | `true` |
| `Encoder` | `JavaScriptEncoder?` | `UnsafeRelaxedJsonEscaping` |
| `MaxDepth` | `int` | `64` |
| `NumberHandling` | `JsonNumberHandling` | `AllowReadingFromString` |
| `CustomConverters` | `List<JsonConverter>?` | `null` |

### 八、文件与 IO `IO`

**`FileHelper`**（`public static`）：

```csharp
public static Task<string> ReadAllTextAsync(string filePath)
public static Task<byte[]> ReadAllBytesAsync(string filePath)
public static Task<string> ReadWithoutBomAsync(string path)     // 去 BOM 读取
public static void CreateIfNotExists(string filePath)
public static void DeleteIfExists(string filePath)
public static string GetHash(string filePath)                   // MD5
public static string GetUniqueName(string fileName)             // 时间戳+随机防重名
public static bool IsUnlocked(string filePath)                  // 是否被其它进程占用
```

**`DirectoryHelper`**：`CreateIfNotExists` / `DeleteIfExists` / `Clear` / `Copy`、`GetFiles(path, pattern, isSearchChild)`、`GetSize(dirPath)`、`GetBaseDirectory()` / `GetWwwrootDirectory()`、`IsEmpty(path)`。

**`PathHelper`** — 路径校验/规范化/安全（防目录穿越）：`IsValidPath`、`IsPathSafe(string path, string basePath)`、`NormalizePath`、`ToUnixPath` / `ToWindowsPath`、`SanitizeFileName`、`GetRelativePath` / `GetAbsolutePath`、`CombinePaths(params string[])`、`GetPathComponents`（返回 `PathComponents` record）。

**`StreamHelper`** — 全面的流工具：读写（`ReadAllBytesAsync` / `WriteAllTextAsync`）、拷贝（`CopyToAsync`、带进度 `CopyToWithProgressAsync`）、内存流/Base64 互转、GZip 压缩（`CompressBytes` / `DecompressBytes`）、哈希（`ComputeMD5Hash` / `ComputeSHA256Hash`）、流对比（`CompareStreamsAsync`）。

**`CompressHelper`**：`Compress` / `Extract`，支持 `CompressionFormat`（`Zip` / `GZip` / `Deflate`）。**`FileFormatExtensions`**：`FormatFileSizeToString(this long bytes)` 转人类可读大小。

### 九、网络 `Net`

- **`DnsHelper`**（`public static`，带缓存）：`ResolveAsync` / `ResolveIPv6Async` / `ReverseLookupAsync`、`QueryMxRecordsAsync` / `QueryTxtRecordsAsync` / `QueryAllRecordsAsync`、批量 `BatchResolveAsync(IEnumerable<string> hostnames, ..., int maxConcurrency = 10)`、`IsValidDomain` / `IsHostReachableAsync`、自定义 DNS 服务器与缓存管理。
- **`PingHelper`**：`Ping(string host, int timeout = 4000, ...)`、`IsHostReachable(string host, int timeout = 1000)`。
- **`HttpClientHelper`**（`public static`，内部持有单例 `HttpClient`，默认超时 30s）：`GetAsync<T>` / `PostAsync<T>` / `PutAsync<T>` / `DeleteAsync<T>`，以及 `GetStringAsync` / `PostStreamAsync` 等，统一支持 `Dictionary<string,string>? headers`。
- **`MimeTypes`**：分组常量（`MimeTypes.Application.Json`、`MimeTypes.Image.Png`、`MimeTypes.Video.Mp4` …）。
- **`IpFormatExtensions`**：`IPAddress` / `byte[]` / `string` 间的 IP 格式互转。
- **`SseClient`**（可实例化）：`ConnectAsync(url, headers, ct)` + `OnMessage` / `OnClosed` 事件；**`SseServer`**（静态）：`SendEventAsync(Stream, SseMessage)` 服务端推送。
- **`WebSocketClient`**（可实例化）：`ConnectAsync()` / `SendTextAsync(string)` / `SendBinaryAsync(byte[])` + `OnMessage` / `OnOpen` / `OnClose` / `OnError` 事件。

### 十、时间 `Timing`

- **`DateTimeRange`**（**可实例化类**，非静态）：表示时间区间，`StartTime` / `EndTime` 属性，构造 `new DateTimeRange(start, end)`；提供大量静态便捷区间：`DateTimeRange.Today` / `Yesterday` / `ThisWeek` / `LastMonth` / `Last7Days` / `Last30DaysExceptToday` 等。
- **`LunarCalendarHelper`**（`public static`，支持 1900–2100）：`ConvertToLunar(DateTime)` / `ConvertToSolar(LunarDate)`、生肖/天干/地支（`GetZodiac` / `GetTiangan` / `GetDizhi`）、节气（`GetSolarTerm`）、农历节日与农历日期字符串。
- **`StopwatchHelper`**（`public static`）：命名计时（`StartNamed` / `StopNamed`）、`Measure(Action)` / `MeasureAsync` / `MeasureWithResult<T>`、性能统计（`GetStatistics` 返回 `TimingStatistics`）、`Benchmark(name, action, iterations = 1000)`、`CreateDisposable(...)` 得到 `using` 即计时的一次性计时器。

### 十一、本地化 `Localization`

- **`CultureHelper`**：`IsRtl` 属性、`Use(string culture, string? uiCulture = null)` 返回 `IDisposable` 临时切换文化、`IsValidCultureCode` / `IsCompatibleCulture`。
- **`CurrencyHelper`**：`GetAllCurrencies()`、`GetCurrencyInfo("CNY")`、`FormatCurrency(decimal amount, string currencyCode)`。
- **`TimeZoneHelper`**：`GetTimeZone(id)`、`ConvertTime(...)`、`GetTimeZoneOffsetString`（如 `"+08:00"`）、`IsTimeZoneExists`。
- **`I18nFormatHelper`** / **`LanguageHelper`**：文化相关的日期/数字格式化与 `ResourceManager` 多语言字符串获取。

### 十二、数学 `Maths`

- **`NumberExtensions`**：面向 `INumber<T>` 泛型的丰富扩展 —— `Clamp` / `IsInRange` / `Mod` / `FloorDiv` / `Gcd` / `Lcm` / `Factorial` / `IsEven` / `IsOdd`、安全运算 `SafeAdd` / `TrySafeMultiply(out T)`、泛型转换 `ConvertTo<TSource,TTarget>`。
- **`MoneyFormatExtensions`**：`FormatMoneyToString(this decimal num)` 金额千/万分位格式化。
- **`PredictHelper`**：线性回归（`LinearRegression` / `GetLinearRegressionCoefficients` / `CalculateCorrelationCoefficient`）、移动平均（`SimpleMovingAverage` / `WeightedMovingAverage`）等时间序列预测。

### 十三、进制编码 `Converters`

`Base32`（RFC 4648）、`Base36`、`Base58`、`Base62`、`Base95`、`CustomRadix` 均为 `public static`，统一签名 `Encode(byte[]) -> string` / `Decode(string) -> byte[]`。用途各异：Base58 适合钱包地址、Base62 适合短链/邀请码、Base95 适合最紧凑可打印编码。

### 十四、进程内缓存 `Caching`

**`CacheHelper`**（`public static`）—— 无需 DI、直接可用的内存缓存，惰性清理 + 支持 LRU/LFU/FIFO 淘汰、统计与事件：

```csharp
public static void Configure(Action<CacheOptions> configure)
public static T? Get<T>(string key)
public static bool TryGetValue<T>(string key, out T? value)
public static void Set<T>(string key, T value, TimeSpan expiration)
public static void SetSliding<T>(string key, T value, TimeSpan slidingExpiration, DateTimeOffset? absoluteExpiration = null)
public static T GetOrAdd<T>(string key, Func<T> factory, TimeSpan expiration)
public static Task<T> GetOrAddAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan expiration, CancellationToken cancellationToken = default)
public static int RemoveByPrefix(string prefix)
public static void Clear()
```

`CacheOptions` 字段：`MaxCacheSize`（默认 10000，0=不限）、`EnableStatistics`（默认 `false`）、`EnableEvents`（默认 `false`）、`CleanupBatchSize`（默认 64）、`EvictionPolicy`（默认 `Lru`）。统计经 `GetStatistics()` 返回 `CacheStatistics`（`HitCount` / `MissCount` / `HitRate` …）。

> 这是**进程内**静态缓存，与需要 DI/分布式的 `XiHan.Framework.Caching` 不是一回事，勿混用。

### 十五、日志 `Logging`

**`LogHelper`**（`public static`）—— 同时输出控制台与文件的首选日志接口，带彩色、级别、统计、异步 Channel 写入：

```csharp
public static void Configure(Action<LogOptions> configure)
public static void Info(string? message)
public static void Info(string? formattedMessage, params object[] args)
public static void Success(string? message)
public static void Warn(string? message)
public static void Error(string? message)
public static void Error(Exception ex)
public static void Error(Exception ex, string? errorMessage)
public static void InfoTable(ConsoleTable table)   // 直接打印表格
public static void FlushToFile()
public static void Shutdown()
```

`LogOptions` 字段（节选）：`MinimumLevel`（默认 `Info`）、`EnableConsoleOutput`（默认 `true`）、`EnableFileOutput`（默认 `false`）、`LogDirectory`（默认 `BaseDirectory/logs`）、`MaxFileSize`（默认 10MB）、`RotationPolicy`（`Size`/`Daily`/`Hourly`/`Hybrid`，默认 `Size`）、`RetentionDays`（默认 30）、`LogFormat`（`Text`/`Json`/`Structured`，默认 `Text`）。格式化器可插拔：`ILogFormatter` 契约 + `TextLogFormatter`（默认）/ `JsonLogFormatter` / `StructuredLogFormatter`。`LogFileHelper` 提供 Channel 队列化的高性能异步文件写入与轮转/清理。

### 十六、诊断 `Diagnostics`

**`Guard`**（`public static`，标注 `[DebuggerStepThrough]`）—— 参数守卫，校验失败即抛异常并返回原值，便于 `field = Guard.NotNull(value, nameof(value))`：

```csharp
public static T NotNull<T>(T? value, string parameterName)
public static string NotNullOrWhiteSpace(string? value, string parameterName, int maxLength = int.MaxValue, int minLength = 0)
public static ICollection<T> NotNullOrEmpty<T>(ICollection<T>? value, string parameterName)
public static T NotDefaultOrNull<T>(T? value, string parameterName) where T : struct
public static int Range(int value, string parameterName, int minimumValue, int maximumValue)  // 另有 short/long/float/double/decimal 及泛型重载
public static Type AssignableTo<TBaseType>(Type type, string parameterName)
```

**重试策略** `Diagnostics/RetryPolicys`：`RetryPolicy` 是可实例化的编排器（`Execute` / `Execute<T>` / `ExecuteAsync(Func<Task> action, CancellationToken)` / `ExecuteAsync<T>(Func<Task<T>> func, CancellationToken)`，返回 `RetryResult` / `RetryResult<T>`）；配套 `RetryPolicyFactory` 工厂：

```csharp
public static RetryPolicy WithFixedDelay(int maxRetries, TimeSpan delay)
public static RetryPolicy WithExponentialBackoff(int maxRetries, TimeSpan baseDelay, double backoffMultiplier = 2.0, TimeSpan? maxDelay = null)
public static RetryPolicy WithLinearBackoff(int maxRetries, TimeSpan baseDelay, TimeSpan increment)
public static RetryPolicy ForException<TException>(int maxRetries, IRetryStrategy? strategy = null) where TException : Exception
```

延迟策略经 `IRetryStrategy.GetDelay(int retryCount)`、重试条件经 `IRetryCondition.ShouldRetry(Exception, int)`，均可自定义扩展。

**硬件信息** `Diagnostics/HardwareInfos`：`CpuHelper` / `RamHelper` / `DiskHelper` / `GpuHelper` / `BoardHelper` / `NetworkHelper` 均为 `public static`，跨平台（Windows/Linux/macOS）采集并带 5 秒缓存，形如 `CpuHelper.GetCpuInfos()` / `CpuHelper.CpuInfos`。**`EventHandlerExtensions`**：`InvokeSafely(...)` 吞异常式安全触发事件。

### 十七、异步原语 `Threading`

- **`Debouncer`**（可实例化，`IDisposable`）：`new Debouncer(interval).Debounce(action)`，间隔内重复调用会取消前次。
- **`DisposeAction`** / **`AsyncDisposeFunc`**：把任意 `Action` / `Func<Task>` 包装成 `using` 释放时执行（`AsyncDisposeFunc` 走 `DisposeAsync`）。
- **`NullDisposable`** / **`NullAsyncDisposable`**：单例空实现（`NullDisposable.Instance`），用于返回"什么都不做"的可释放对象。
- **`AsyncBarrier`** / **`AsyncReaderWriterLock`**：异步屏障与异步读写锁（`AcquireReadLockAsync()` 返回 `IDisposable`）。

### 十八、控制台工具 `ConsoleTools`

`ConsoleTable`（带边框/彩色/自适应列宽的表格，`new ConsoleTable(params string[] headers)`）、`ConsoleProgressBar`（`new ConsoleProgressBar(total, ...)` + `Update(current, message)`）、`ConsoleSpinner`（`IDisposable` 动画加载）、`ConsoleMenu`（交互式菜单选择）、`ConsolePrompt`（`Input(...)` 带校验的输入）、`ConsoleColorWriter`（彩色输出，被 `LogHelper` 使用）。

### 十九、命令行 `CommandLine`

```csharp
// ShellHelper（跨平台执行）
public static string Bash(string command)          // Unix/Linux
public static string Cmd(string fileName, string args)  // Windows

// ScriptExecutor（脚本文件执行 .sh/.ps1/.bat）
public static string ExecuteScript(string scriptFilePath, string arguments = "")
```

### 二十、运行时 / 枚举元数据 / 常量 / 异常

- **`OsPlatformHelper`**（`Runtime`）：`IsWindows` / `IsLinux` / `IsMacOs` / `IsUnixSystem` 判定属性、`RuntimeInfos`（带缓存）、`GetEnvironmentVariable(name, target)`；`RuntimeMonitor` 采集运行时指标。
- **`Enums`**：`EnumItem`（record，字段 `Key` / `Value` / `Description` / `Theme` / `Icon` / `Order` / `Hidden` / `Disabled` / `LocalizationKey` / `ResourceName` / `Extra`，均带 JSON 命名，另有强类型版本 `EnumItem<T>`）、`EnumInfo`（枚举整体元数据 + 本地化资源信息）、`EnumAttributes`（`[EnumTheme]` / `[EnumOrder]` 特性）。这是后端枚举元数据下发前端的底层结构。
- **`Constants/DefaultConsts`**：字符集常量 `UppercaseLetters` / `LowercaseLetters` / `Letters` / `Digits` / `SpecialCharacters` 等。
- **`Exceptions/CustomException`**：继承 `Exception`，构造时自动经 `LogHelper.Error(...)` 记录（注意：其消息会前置固定前缀"自定义异常。"）。

## 使用示例

```csharp
using XiHan.Framework.Utils.Security.Cryptography;
using XiHan.Framework.Utils.Security;
using XiHan.Framework.Utils.Serialization.Json;
using XiHan.Framework.Utils.Extensions;
using XiHan.Framework.Utils.Diagnostics;

// 1) 哈希 + 脱敏 + 命名转换
var digest = HashHelper.Sha256("payload");
var masked = "13800138000".MaskPhone();          // 138****8000
var col = "UserName".ToSnakeCase();               // user_name

// 2) 序列化：对象 <-> JSON（camelCase、Try 风格）
var json = new { Id = 1, Name = "曦寒" }.ToJson();
if ("{\"id\":1}".TryFromJson<Dictionary<string, int>>(out var dict))
{
    // ...
}

// 3) 参数守卫
public void Register(string name)
{
    name = Guard.NotNullOrWhiteSpace(name, nameof(name), maxLength: 32);
}

// 4) 带指数退避的重试
var policy = RetryPolicyFactory.WithExponentialBackoff(maxRetries: 3, baseDelay: TimeSpan.FromMilliseconds(200));
var result = await policy.ExecuteAsync(async () => await httpCall());
```

```csharp
using XiHan.Framework.Utils.Caching;
using XiHan.Framework.Utils.Timing;

// 5) 进程内缓存：命中即返回，未命中执行工厂并缓存
var user = await CacheHelper.GetOrAddAsync(
    key: $"user:{id}",
    factory: async _ => await LoadUserAsync(id),
    expiration: TimeSpan.FromMinutes(5));

// 6) 时间区间便捷属性
var lastWeek = DateTimeRange.LastWeek;   // StartTime / EndTime
```

## 扩展点 / 自定义

- **序列化**：`JsonSerializeOptions.CustomConverters` 挂自定义 `JsonConverter`；或实现自己的转换器后经 `JsonConverterFactory` 复用内置转换器集合。
- **日志格式**：实现 `ILogFormatter`（`Format(DateTimeOffset, LogLevel, string, Dictionary<string, object>?)`）替换默认 `TextLogFormatter`，或直接把 `LogOptions.LogFormat` 切到 `Json` / `Structured`。
- **重试**：实现 `IRetryStrategy`（延迟算法）与 `IRetryCondition`（是否重试）注入 `RetryPolicy`，或用 `RetryPolicyFactory` 的预设。
- **缓存策略**：`CacheOptions.EvictionPolicy` 在 `Lru` / `Lfu` / `Fifo` 间切换。
- **枚举元数据**：用 `[EnumTheme]` / `[EnumOrder]` 特性 + `[Description]` 标注枚举字段，`EnumHelper.GetEnumItems<T>()` 会读出。

## 注意事项与最佳实践

- **加密安全 vs 非加密随机**：验证码、临时密码、密钥类场景必须用 `RandomCoder`（`RandomNumberGenerator` 底座）或 `GuidHelper.NewCryptoGuid()`；`RandomHelper` 基于 `Random.Shared`，仅适合打乱/抽样等非安全场景。
- **`MaskHelper.Mask` 是扩展方法**：以 `input.Mask(2, 2)` 形式调用；`MaskPhone` / `MaskEmail` 等则是普通静态方法。
- **`DateTimeRange` 是可实例化类而非静态 Helper**：通过静态属性（`DateTimeRange.Today` 等）或构造函数创建实例。
- **`CustomException` 有副作用**：构造即写日志且给消息加固定前缀，普通业务异常不建议用它。
- **两个"缓存/日志"不要混淆**：`Utils.Caching.CacheHelper` / `Utils.Logging.LogHelper` 是**进程内静态**工具；需要 DI、分布式或结构化管道时用 `XiHan.Framework.Caching` / `XiHan.Framework.Logging`。
- **YAML 为手写解析器**：满足配置级键值/嵌套映射足够，但不等于完整 YAML 1.2 规范，复杂文档请自行验证。
- **零依赖是硬约束**：向该包新增能力时应尽量只用 BCL，避免引入 `PackageReference` 破坏"可被任意层安全引用"的前提。

## 依赖模块

- 编译期：[XiHan.Framework.Analyzers](./analyzers)（文件头规范检查，运行时不参与）。
- 运行时：无框架内部业务依赖，仅 .NET BCL。

## 相关模块

- [XiHan.Framework.Core](./core) — 核心库，构建在 Utils 之上。
- [XiHan.Framework.Metadata](./metadata) — 框架元数据，同为公共/元数据层基础包。
- [XiHan.Framework.Caching](./caching) — 需要 DI/分布式的缓存基础设施（区别于 `Utils.Caching`）。
- [XiHan.Framework.Logging](./logging) — 结构化日志基础设施（区别于 `Utils.Logging`）。
- [XiHan.Framework.Serialization](./serialization) — 更上层的序列化抽象与集成。
