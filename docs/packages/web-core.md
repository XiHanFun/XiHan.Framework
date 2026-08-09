# XiHan.Framework.Web.Core

> Web 基础设施底座：托管环境接入、HttpContext 支持、当前主体桥接（`ICurrentUser`）、以及客户端信息识别（真实 IP / 归属地 / 浏览器·系统·设备）。

- **NuGet**：`XiHan.Framework.Web.Core`
- **模块类**：`XiHanWebCoreModule`
- **所在层**：Web 层
- **关键依赖**：`IP2Region.Net`（离线 IP 归属地）、`UAParser`（User-Agent 解析）；SDK 为 `Microsoft.NET.Sdk.Web`，可直接使用 ASP.NET Core 类型。

## 概述

XiHan.Framework.Web.Core 是所有 Web 相关包（Web.Api、Web.Docs、Web.Grpc、Web.RealTime、Web.Gateway）的公共底座。它把「当前 HTTP 请求」这件事在框架里落地为几件具体的基础设施：

- 把 ASP.NET Core 的 `IWebHostEnvironment` 环境名同步进框架的 `IXiHanHostEnvironment`；
- 注册 `IHttpContextAccessor`，让服务层随处可拿到当前请求；
- 用 `HttpContext.User`（JWT 认证后的主体）作为框架 `ICurrentUser` / `ICurrentPrincipalAccessor` 的数据源；
- 提供一个「客户端信息识别」服务，从请求里解析出真实客户端 IP、地理归属地、浏览器 / 操作系统 / 设备名。

你一般不直接引用本包，而是通过上层的 Web.Api 等包间接获得它。

## 何时使用

- 需要在服务里拿到「当前登录用户」——本包让框架 `ICurrentUser` 在 Web 请求中可用（读取 `HttpContext.User`）。
- 需要识别请求来源：穿透反向代理还原真实客户端 IP、解析归属地、识别浏览器与设备（登录日志、审计、风控常用）。
- 需要一个统一的 `IApplicationBuilder` / 托管环境访问入口，供模块化生命周期（`OnApplicationInitialization`）使用。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Web.Core
```

```csharp
[DependsOn(typeof(XiHanWebCoreModule))]
public class MyModule : XiHanModule { }
```

模块 `PreConfigureServices` 会把 `IWebHostEnvironment.EnvironmentName` 同步进 `IXiHanHostEnvironment`（当后者为空时）；`ConfigureServices` 调用 `AddXiHanWebCore(configuration)`，注册：`IApplicationBuilder` 的 `ObjectAccessor`、`IHttpContextAccessor`、`XiHanClientInfoOptions`（绑定配置节）、`IClientInfoProvider`（单例）、以及 `ICurrentPrincipalAccessor`（Scoped，实现为 `HttpContextCurrentPrincipalAccessor`）。

## 核心能力

- **托管环境接入**：模块前置阶段把 ASP.NET Core 环境名写入 `IXiHanHostEnvironment`。若 DI 中缺 `IWebHostEnvironment`，`GetHostingEnvironment()` 回退到 `EmptyHostingEnvironment`（默认 `Development`）。
- **HttpContext 与当前主体**：注册 `IHttpContextAccessor`；`HttpContextCurrentPrincipalAccessor` 以 `HttpContext.User` 为当前主体（无请求时回退空 `ClaimsPrincipal`），从而让框架 `ICurrentUser` 在 Web 请求中生效。
- **客户端信息识别**：`IClientInfoProvider.GetCurrent()` 从当前请求解析出 `ClientInfo`（IP / 归属地 / 原始 UA / 浏览器 / 系统 / 设备）。
- **真实 IP 解析**：优先取 `X-Forwarded-For` 第一跳，其次 `X-Real-IP`，回退 `Connection.RemoteIpAddress`；对 IPv4-mapped-IPv6 做规范化（`MapToIPv4`）。
- **IP 归属地定位**：回环 IP 记为「本机」、内网 IP 记为「局域网」；仅公网 IP 才触发 `ip2region`（`IP2Region.Net`）离线查询。数据库文件按候选路径自动探测，缺库时静默跳过、不报错。
- **UA 解析**：基于 `UAParser` 解析 User-Agent 得到浏览器 / 系统 / 设备名；桌面浏览器 `Device.Family` 常为 `Other`，会归一化为 `PC`。
- **Claims 转换（可选）**：`XiHanClaimsTransformation` 支持把标准 OIDC 声明（`sub`/`role`/`email`/`name` 等）映射为框架 `XiHanClaimTypes`；通过 `services.TransformXiHanClaims()` 显式注册后才生效。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `IClientInfoProvider` | 客户端信息提供器契约。唯一方法 `ClientInfo GetCurrent()`。 |
| `ClientInfo` | 结果模型：`IpAddress` / `Location` / `UserAgent` / `Browser` / `OperatingSystem` / `DeviceName`（均可空）。 |
| `HttpContextClientInfoProvider` | 默认实现（单例，`IDisposable`）：真实 IP 解析 + ip2region 归属地 + UAParser 解析。IP 查询器懒加载并加锁复用。 |
| `XiHanClientInfoOptions` | 客户端信息配置（配置节 `XiHan:Web:Core:ClientInfo`）。 |
| `HttpContextCurrentPrincipalAccessor` | 继承 `CurrentPrincipalAccessorBase`，以 `HttpContext.User` 作为当前主体，桥接框架 `ICurrentUser`。 |
| `XiHanClaimsTransformation` | `IClaimsTransformation` 实现，按映射表补加声明标识。 |
| `XiHanClaimsMapOptions` | 声明映射表 `Dictionary<string, Func<string>>`，默认映射 `sub/role/email/name/family_name/given_name`。 |
| `EmptyHostingEnvironment` | `IWebHostEnvironment` 空实现，DI 缺失环境时的回退。 |

扩展方法（`XiHanWebCoreServiceCollectionExtensions`）：

| 方法 | 说明 |
| --- | --- |
| `services.AddXiHanWebCore(configuration)` | 注册本包全部核心服务（模块内部调用）。 |
| `services.GetHostingEnvironment()` | 取 `IWebHostEnvironment`，缺失时回退 `EmptyHostingEnvironment`。 |
| `services.TransformXiHanClaims()` | 注册 `IClaimsTransformation` = `XiHanClaimsTransformation`（声明映射，需显式调用）。 |

`ApplicationInitializationContext` 扩展（`ApplicationInitializationContextExtensions`）：`GetApplicationBuilder()` / `GetApplicationBuilderOrNull()` / `GetEnvironment()` / `GetEnvironmentOrNull()` / `GetConfiguration()` / `GetLoggerFactory()`——供模块在 `OnApplicationInitialization` 里取到 `IApplicationBuilder` 与环境。

`WebApplicationBuilder` 扩展（`WebApplicationBuilderExtensions`）：`AddApplicationAsync<TStartupModule>(...)` / `AddApplicationAsync(startupModuleType, ...)`——在 `WebApplicationBuilder` 上装配模块化应用，自动接管 `Configuration` 与 `EnvironmentName`。

`IApplicationBuilder` 扩展（`XiHanApplicationBuilderExtensions`）：`InitializeApplicationAsync()` / `InitializeApplication()`——驱动模块 `OnApplicationInitialization`，并挂接应用生命周期的关闭与释放。

## 配置

配置节：`XiHan:Web:Core:ClientInfo`（`XiHanClientInfoOptions`）。

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `EnableIpRegion` | `bool` | `true` | 是否对公网 IP 启用 ip2region 归属地解析。 |
| `Ip2RegionDbPath` | `string?` | `IpDatabases/ip2region.xdb` | ip2region xdb 文件路径，支持相对路径（相对 `ContentRootPath`）。 |

数据库候选路径顺序：配置的 `Ip2RegionDbPath` → `IpDatabases/ip2region.xdb` → `ip2region.xdb`；逐个探测存在的文件，都不存在时静默跳过归属地解析。

示例 `appsettings.json`：

```json
{
  "XiHan": {
    "Web": {
      "Core": {
        "ClientInfo": {
          "EnableIpRegion": true,
          "Ip2RegionDbPath": "IpDatabases/ip2region.xdb"
        }
      }
    }
  }
}
```

## 使用示例

在任意服务里注入 `IClientInfoProvider` 拿到当前请求的客户端信息：

```csharp
public class LoginLogWriter
{
    private readonly IClientInfoProvider _clientInfoProvider;

    public LoginLogWriter(IClientInfoProvider clientInfoProvider)
        => _clientInfoProvider = clientInfoProvider;

    public void Record(string userName)
    {
        var info = _clientInfoProvider.GetCurrent();
        // info.IpAddress / info.Location / info.Browser / info.OperatingSystem / info.DeviceName
    }
}
```

启用 OIDC 声明映射（把 `sub`/`role`/`email` 等映射为框架声明类型）：

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    context.Services.TransformXiHanClaims();
}
```

## 扩展点 / 自定义

- **替换客户端信息提供器**：`IClientInfoProvider` 以 `AddSingleton`（非 `TryAdd`）注册默认实现；如需自定义解析逻辑，可在模块配置阶段用自己的实现覆盖注册（后注册者胜出），或在此基础上包装。
- **替换当前主体来源**：`ICurrentPrincipalAccessor` 以 Scoped 注册 `HttpContextCurrentPrincipalAccessor`；非标准场景可替换实现改变「当前主体」的取值来源。
- **自定义声明映射**：`Configure<XiHanClaimsMapOptions>` 修改 `Maps` 字典（键=源声明类型，值=返回目标 `XiHanClaimTypes` 的委托），再 `TransformXiHanClaims()` 注册。

## 注意事项与最佳实践

- **归属地依赖离线库**：`ip2region.xdb` 需随应用部署（默认放 `IpDatabases/`）。缺库不会报错，但 `ClientInfo.Location` 对公网 IP 为 `null`。
- **真实 IP 依赖转发头还原**：X-Forwarded-For 由上层 Web.Api 的 `UseForwardedHeaders` 在管线最前还原；反向代理需正确透传该头，否则解析到的是反代地址。
- **`ICurrentUser` 生效前提**：本包只桥接 `HttpContext.User`；用户主体的真实内容由认证中间件（JWT）填充，须在 Web.Api 中启用认证。
- **声明转换不默认启用**：`XiHanClaimsTransformation` 只有显式调用 `TransformXiHanClaims()` 才注册；未调用时不做任何声明映射。

## 依赖模块

- 内部依赖：[XiHan.Framework.Core](./core)、[XiHan.Framework.Authentication](./authentication)、[XiHan.Framework.Security](./security)。
- 第三方核心：`IP2Region.Net`（离线 IP 归属地）、`UAParser`（User-Agent 解析）。

## 相关模块

- [XiHan.Framework.Web.Api](./web-api) — 在本包之上装配完整的 REST API 中间件管道。
- [XiHan.Framework.Web.Docs](./web-docs)、[XiHan.Framework.Web.RealTime](./web-realtime)、[XiHan.Framework.Web.Grpc](./web-grpc)、[XiHan.Framework.Web.Gateway](./web-gateway) — 其它 Web 层包，均以本包为底座。
- [模块生命周期](../guide/lifecycle) — `PreConfigureServices` / `ConfigureServices` / `OnApplicationInitialization` 钩子。
