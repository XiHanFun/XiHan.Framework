# XiHan.Framework.Timing

> 时间策略：统一时钟抽象 `IClock`（UTC/Local 模式 + 规范化）、Windows↔IANA 时区互转、请求级当前时区，以及 UTC↔用户时间双向转换。

- **NuGet**：`XiHan.Framework.Timing`
- **模块类**：`XiHanTimingModule`
- **所在层**：基础设施层
- **关键依赖**：`TimeZoneConverter`（Windows / IANA 时区互转）

## 概述

XiHan.Framework.Timing 为框架提供统一的「时间」抽象。核心是 `IClock`：用它取代散落的 `DateTime.Now` 调用，可在 UTC 与本地两种模式间切换（由 `XiHanClockOptions.Kind` 控制），并把不同 `DateTimeKind` 的时间值规范化到统一基准。配合时区提供器，`IClock` 能在 UTC 与「用户所在时区」之间双向转换；而 `ITimezoneProvider` 基于 TimeZoneConverter 做 Windows 时区 ID 与 IANA 时区名的互转与列表获取。「当前时区」由 `CurrentTimezoneProvider` 用 `AsyncLocal` 存储，天然按请求隔离。

## 何时使用

- 想让整个应用统一走一个时钟抽象，而非分散地调用 `DateTime.Now`（便于测试替换与统一 UTC/Local 策略）。
- 后端存 UTC、按用户时区展示，需要 UTC ↔ 用户时间的双向转换。
- 需要在 Windows 时区 ID 与 IANA 时区名之间互转，或获取可选时区列表。
- 需要按请求隔离「当前时区」（请求级作用域）。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Timing
```

```csharp
[DependsOn(typeof(XiHanTimingModule))]
public class MyModule : XiHanModule { }
```

模块在 `ConfigureServices` 中调用 `AddXiHanTiming()`，注册：

| 服务 | 实现 | 生命周期 |
| --- | --- | --- |
| （Options） | `XiHanClockOptions` | 通过 `AddOptions` |
| `IClock` | `Clock` | Singleton |
| `ITimezoneProvider` | `TZConvertTimezoneProvider` | Singleton |
| `ICurrentTimezoneProvider` | `CurrentTimezoneProvider` | Transient（内部 `AsyncLocal`，请求级隔离） |

启用后即可注入 `IClock` 使用。

## 工作原理

- **模式由 `Kind` 决定**：`Clock.Now` 在 `Kind == Utc` 时返回 `DateTime.UtcNow`，否则 `DateTime.Now`；`SupportsMultipleTimezone` 也等价于 `Kind == Utc`。**默认 `Kind` 为 `Unspecified`**，此时不启用多时区，`ConvertToUserTime` / `ConvertToUtc` 直接原样返回——要启用时区换算须显式把 `Kind` 配为 `Utc`。
- **换算依赖当前时区**：`ConvertToUserTime` / `ConvertToUtc` 会读 `ICurrentTimezoneProvider.TimeZone`（为空则不换算），经 `ITimezoneProvider.GetTimeZoneInfo(...)` 得到 `TimeZoneInfo` 后用 `TimeZoneInfo.ConvertTime*` 完成换算。
- **请求级时区来源**：Web 层可在管道中读取请求头（如 `X-Timezone`）写入 `ICurrentTimezoneProvider.TimeZone`；因其基于 `AsyncLocal`，同一请求链路内一致、请求间隔离。

## 核心能力

- **统一时钟抽象**：`IClock` 提供当前时间与 `Kind`，支持 UTC / Local 模式。
- **DateTime 规范化**：`Clock.Normalize(dateTime)` 处理不同 `DateTimeKind` 之间的转换（Utc↔Local↔Unspecified）。
- **UTC ↔ 用户时间转换**：UTC 模式下 `ConvertToUserTime` / `ConvertToUtc` 双向换算，支持 `DateTime` 与 `DateTimeOffset`。
- **时区标准互转**：`TZConvertTimezoneProvider` 基于 TimeZoneConverter 做 Windows / IANA 互转与列表获取。
- **请求级当前时区**：`CurrentTimezoneProvider` 用 `AsyncLocal<string?>` 存储当前请求时区。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `IClock` | 时钟接口。`DateTime Now`、`DateTimeKind Kind`、`bool SupportsMultipleTimezone`、`DateTime Normalize(DateTime)`、`DateTime ConvertToUserTime(DateTime)`、`DateTimeOffset ConvertToUserTime(DateTimeOffset)`、`DateTime ConvertToUtc(DateTime)` |
| `Clock` | `IClock` 实现，成员 `virtual` 可继承覆盖；换算逻辑依赖当前时区与 `TimezoneProvider` |
| `ITimezoneProvider` | `List<NameValue> GetWindowsTimezones()`、`List<NameValue> GetIanaTimezones()`、`string WindowsToIana(string)`、`string IanaToWindows(string)`、`TimeZoneInfo GetTimeZoneInfo(string windowsOrIanaTimeZoneId)` |
| `TZConvertTimezoneProvider` | `ITimezoneProvider` 实现，基于 `TimeZoneConverter.TZConvert` |
| `ICurrentTimezoneProvider` | 当前时区。`string? TimeZone { get; set; }` |
| `CurrentTimezoneProvider` | 用 `AsyncLocal<string?>` 存储，请求级隔离 |
| `XiHanClockOptions` | 时钟配置。`DateTimeKind Kind { get; set; }`，默认 `Unspecified` |

DI 入口：`ServiceCollectionExtensions.AddXiHanTiming(this IServiceCollection)`。

## 配置

`XiHanClockOptions` 无独立配置节常量，通过 `AddOptions<XiHanClockOptions>()` 注册，默认 `Kind = Unspecified`。如需启用 UTC 存储 + 多时区换算，在模块中代码配置：

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    context.Services.Configure<XiHanClockOptions>(o => o.Kind = DateTimeKind.Utc);
}
```

## 使用示例

### 注入 IClock 取当前时间

```csharp
public class TokenService
{
    private readonly IClock _clock;

    public TokenService(IClock clock) => _clock = clock;

    public bool IsExpired(DateTime expirationTime)
        // 先规范化到时钟 Kind，再比较
        => _clock.Normalize(expirationTime) < _clock.Now;
}
```

### UTC ↔ 用户时区换算（需 Kind = Utc 且已设当前时区）

```csharp
public class DisplayService
{
    private readonly IClock _clock;
    private readonly ICurrentTimezoneProvider _currentTz;

    public DisplayService(IClock clock, ICurrentTimezoneProvider currentTz)
    {
        _clock = clock;
        _currentTz = currentTz;
    }

    public DateTime ToUserTime(DateTime utcTime)
    {
        _currentTz.TimeZone = "Asia/Shanghai";   // 通常由请求头 X-Timezone 中间件写入
        return _clock.ConvertToUserTime(utcTime); // Kind=Utc 时按当前时区换算
    }
}
```

### Windows / IANA 时区互转与列表

```csharp
public class TimezoneOptionsService
{
    private readonly ITimezoneProvider _tz;

    public TimezoneOptionsService(ITimezoneProvider tz) => _tz = tz;

    public string GetIana() => _tz.WindowsToIana("China Standard Time"); // => "Asia/Shanghai"
    public List<NameValue> ListIana() => _tz.GetIanaTimezones();
}
```

## 扩展点 / 自定义

- **自定义时钟**：`Clock` 的成员为 `virtual`，可继承覆盖 `Now`、换算逻辑等，并在 DI 中替换 `IClock`（例如测试用固定时钟）。
- **自定义时区源**：实现 `ITimezoneProvider` 替换默认的 `TZConvertTimezoneProvider`。
- **自定义当前时区注入**：在 Web 管道中读取请求头写入 `ICurrentTimezoneProvider.TimeZone`，即可让 `IClock` 的换算按请求生效。

## 注意事项与最佳实践

- **默认不换算**：`XiHanClockOptions.Kind` 默认 `Unspecified`，此时 `SupportsMultipleTimezone` 为 `false`，`ConvertToUserTime` / `ConvertToUtc` 原样返回。需要时区换算必须显式配为 `Utc`。
- **换算前置条件**：只有在 `Kind = Utc`、输入 `DateTime.Kind = Utc`（`ConvertToUserTime`）且 `ICurrentTimezoneProvider.TimeZone` 非空时才会真正换算；任一不满足则原样返回。
- **存 UTC、显示按时区**：推荐后端统一存 UTC，展示层用当前时区换算，避免夏令时与跨时区歧义。
- `GetTimeZoneInfo` 同时接受 Windows ID 与 IANA 名，跨平台（Windows/Linux）一致由 TimeZoneConverter 兜底。

## 依赖模块

- 内部依赖：仅 [XiHan.Framework.Core](./core)。
- 第三方核心：`TimeZoneConverter`（Windows / IANA 时区互转）。

## 相关模块

- [XiHan.Framework.Utils](./utils) — 其 `Timing` 目录提供日期时间区间、农历、计时等基础时间工具。
- [XiHan.Framework.Threading](./threading) — 同样用 `AsyncLocal` 做请求级隔离，思路一致。
