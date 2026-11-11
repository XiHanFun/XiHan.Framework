# IP 归属地

这一页讲怎样把调用方的 IP 地址解析成国家、省份、城市与运营商：契约 `IIpRegionResolver` 与结果 `IpRegion`，离线库 Provider `Security.Ip2Region` 的装配与 xdb 文件配置，怎样在操作里拿到调用来源的远端地址去解析，以及它与条件引擎的配合。读完你能在登录日志里记下归属地，并在风控条件里按国家或省份判定。阅读前先读 [RBAC 授予与条件](/security/grants)与[调用上下文](/startup/invocation-context)。

## 为什么需要

登录日志要显示「来自广东深圳 电信」，风控规则要写「境外登录需要二次验证」，异常检测要比较两次登录的归属地。这些都需要把 IP 变成地理信息，但在线查询服务会把用户 IP 发到第三方、带来延迟与配额，直接依赖某个库又会让应用和它绑死。框架把归属地做成可选能力 `security.ip-region@1`：契约只有一个方法，应用依赖契约而不是实现；参考实现 `Security.Ip2Region` 用离线库在内存里查询，不出网；不装 Provider 时应用照常运行，引用归属地的条件按「取不到」处理。

## 契约

| 类型 | 说明 |
| --- | --- |
| `IIpRegionResolver` | `Resolve(IPAddress)` 返回 `Maybe<IpRegion>`；查不到、不该查、没有数据都返回无值，不抛出 |
| `IpRegion` | 结果：`Country`、`Province`、`City`、`Isp`、`CountryCode`（两位国家代码），各段可能缺失；`Location` 是展示文本 |

`Location` 把国家、省份、城市、运营商去掉缺失与重复（不区分大小写）后以空格连接，例如 `中国 广东省 深圳市 电信`。

宿主可以不装。容器里没有 `IIpRegionResolver` 时，调用方把归属地当作取不到；条件属性 `environment.geo.*` 这类引用它的子句为无法判定，`IsNull` / `IsNotNull` 照常判定。

| 包 | 清单角色 | 说明 |
| --- | --- | --- |
| `XiHan.Framework.Security.Ip2Region` | `provider` | ip2region 离线库 Provider，IPv4 与 IPv6 各一个 xdb 文件，首次用到某个地址族时整库读进内存，之后每次查询约百纳秒 |

框架包不带数据文件，xdb 由宿主提供（下载或生成方式见 ip2region 项目）。IPv4 库约 11 MB、IPv6 库约 37 MB。

## 最小示例

```bash
dotnet add package XiHan.Framework.Security.Ip2Region --prerelease
```

xdb 文件随应用发布，例如在项目文件里复制到输出目录：

```xml
<ItemGroup>
  <None Include="IpDatabases/*.xdb" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

```csharp
using System.Net;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Hosting.AspNetCore.Endpoints;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Security.Abstractions.Network;
using XiHan.Framework.Security.Ip2Region;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<Ip2RegionSecurityFeature>());

var app = builder.Build();
app.UseXiHan();

// 调用来源的远端地址由 UseXiHan() 写进调用上下文；回环与内网地址解析为无值
app.MapGet("/where", (HttpContext http, IIpRegionResolver regions) =>
{
    var origin = http.FindXiHanContext().ValueOrThrow().FindOrigin();
    return origin.TryGetValue(out var o)
        && IPAddress.TryParse(o.RemoteAddress, out var address)
        && regions.Resolve(address).TryGetValue(out var region)
            ? region.Location
            : "unknown";
});

app.Run();
```

本机访问 `/where` 响应 `unknown`，因为回环地址不查；经反向代理部署并在 `UseXiHan()` 之前 `UseForwardedHeaders()` 后，远端地址是真实客户端地址，响应形如 `中国 广东省 深圳市 电信`。

## 配置

配置节 `XiHan:Security:Ip2Region`：

| 键 | 类型 | 缺省 | 说明 |
| --- | --- | --- | --- |
| `V4Database` | `string` | `IpDatabases/ip2region_v4.xdb` | IPv4 的 xdb 文件；相对路径按 `AppContext.BaseDirectory` 解析 |
| `V6Database` | `string` | `IpDatabases/ip2region_v6.xdb` | IPv6 的 xdb 文件；相对路径同上 |

```json
{
  "XiHan": {
    "Security": {
      "Ip2Region": {
        "V4Database": "IpDatabases/ip2region_v4.xdb",
        "V6Database": "IpDatabases/ip2region_v6.xdb"
      }
    }
  }
}
```

路径不能为空；文件在首次解析该地址族时才加载，配置错了不会让启动失败，只会让该族一律无值并记一条警告。

## 在操作里用调用来源解析

调用来源 `InvocationOrigin` 由宿主在构造调用上下文时写入，`context.FindOrigin()` 读它；HTTP 宿主填的 `RemoteAddress` 是连接的对端地址（宿主在 `UseXiHan()` 之前 `UseForwardedHeaders()` 时即为转发头改写后的客户端地址，IPv4 映射的 IPv6 还原为 IPv4）。依赖契约而不是 Provider，没装时照常工作：

```csharp
using System.Net;
using XiHan.Framework.Kernel.Pipeline;
using XiHan.Framework.Primitives;
using XiHan.Framework.Security.Abstractions.Network;

public sealed class CallerLocation(IIpRegionResolver? regions = null)
{
    // 没装归属地 Provider、没有调用来源、回环或内网地址、查不到时都返回 null
    public string? Describe(InvocationContext context)
    {
        if (regions is null || !context.FindOrigin().TryGetValue(out var origin))
        {
            return null;
        }

        return IPAddress.TryParse(origin.RemoteAddress, out var address) && regions.Resolve(address).TryGetValue(out var region)
            ? region.Location
            : null;
    }
}
```

在操作处理器里把它写进登录记录：

```csharp
using XiHan.Framework.Kernel.Operations;
using XiHan.Framework.Primitives;

[Operation(Id, Expose = Expose.Http, Permission = "sessions.record")]
public sealed class RecordLogin(CallerLocation location, ILoginLog log) : IOperation<RecordLoginRequest, RecordLoginResponse>
{
    public const string Id = "sessions.record-login";

    public async ValueTask<Result<RecordLoginResponse>> HandleAsync(RecordLoginRequest request, InvocationContext context)
    {
        // 归属地是展示信息，取不到就记空，不让登录失败
        await log.AppendAsync(context.GetSecurity().Principal.Id, location.Describe(context), context.CancellationToken);
        return new RecordLoginResponse();
    }
}
```

`ILoginLog`、`RecordLoginRequest`、`RecordLoginResponse` 是应用自己的类型。构造函数参数 `IIpRegionResolver? regions = null` 让容器里没有该服务时也能解析出 `CallerLocation`；也可以注入 `IServiceProvider` 再 `GetService<IIpRegionResolver>()`。

## 行为与限制

- 不查的地址：回环；IPv4 的 `10/8`、`172.16/12`、`192.168/16`；IPv6 的链路本地、站点本地与组播。它们直接返回无值，不访问库。
- IPv4 映射的 IPv6 地址按 IPv4 查。
- 某个地址族的文件不存在或加载失败时记一条警告，之后该族一律无值，应用照常运行；不会在启动时失败。
- 记录格式兼容两版：新版「国家|省份|城市|运营商|国家代码」，旧版「国家|区域|省份|城市|运营商」。`0` 与空段为未知；保留地址与各段全未知返回无值。
- 单次查询失败（库损坏、地址超出库的范围）按无值处理，只记调试日志，地址不进日志；`Resolve` 不抛出。
- `Ip2RegionResolver` 是单例且 `IDisposable`，应用停止时释放已加载的查询器；加载后的查询线程安全。
- 公开面不暴露第三方类型，换实现不影响调用方。

## 与条件引擎配合

条件引擎（见 [RBAC 授予与条件](/security/grants)）的属性由应用的 `IConditionAttributeSource` 提供。归属地属性约定命名为 `environment.geo.*`，取不到时返回 `ConditionAttributeValue.Missing`，子句为无法判定、按不命中计，所以「境外登录」这类规则在没装 Provider 时不会误伤：

```csharp
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Security.Abstractions.Network;

public static class GeoAttributes
{
    // 给条件属性来源用：没装归属地 Provider 或查不到时返回 null，属性按取不到处理
    public static string? CountryCode(IServiceProvider services, IPAddress address)
        => services.GetService<IIpRegionResolver>() is { } resolver && resolver.Resolve(address).TryGetValue(out var region)
            ? region.CountryCode
            : null;
}
```

把 `environment.geo.countryCode` 登记为 `ConditionValueKind.String` 属性后，条件 `environment.geo.countryCode NotEquals CN` 就表示境外来源；来源取不到时子句无法判定，由规则的其他组决定结果。

## 换成自己的实现

只依赖 `Security.Abstractions` 的 `IIpRegionResolver` 契约，实现它并在特性里登记为单例即可换成在线服务或其他离线库；清单声明提供 `security.ip-region@1`，与 `Security.Ip2Region` 不同时装配。实现要遵守同样的约定：回环与私网不查、查不到返回无值、不抛出。

## 常见问题

### 本机调试为什么总是 `unknown`

回环与私网地址不查，直接返回无值。调试时用 `IPAddress.Parse("...")` 传一个公网地址给 `Resolve`，或者在测试里用 `DictionaryConditionAttributeSource` 直接给属性值。

### 部署在反向代理后面，拿到的 IP 是代理的

`RemoteAddress` 取连接的对端地址。在 `UseXiHan()` 之前调用 `UseForwardedHeaders()` 并配置可信代理，远端地址就是 `X-Forwarded-For` 里的客户端地址，见 [ASP.NET Core 宿主](/web/hosting)。不要自己从请求头读 `X-Forwarded-For`，未经可信代理过滤的值可以被客户端伪造。

### 启动时没报错，但所有地址都是无值

多半是 xdb 文件路径不对或文件没随应用发布：文件在首次解析时才加载，缺失只记一条警告。检查日志里 `Ip2Region` 相关的警告，核对 `V4Database` / `V6Database` 相对 `AppContext.BaseDirectory` 的路径，以及项目文件里的 `CopyToOutputDirectory`。

### 数据库怎样更新

替换 xdb 文件后重启应用。库在首次使用时整库读进内存，运行中替换文件不会生效。

### gRPC 和 MCP 调用也有远端地址吗

有。它们都经 ASP.NET Core 宿主进入，`UseXiHan()` 以同样的方式填 `InvocationOrigin.RemoteAddress`。命令行宿主与进程内调用没有网络对端，`RemoteAddress` 为 `null`。

## 相关包与下一步

- 包参考：[Security.Ip2Region](/packages/security-ip2region)、[Security.Abstractions](/packages/security-abstractions)
- 规范：RFC-0008《认证与授权》
- 相关章节：[调用上下文](/startup/invocation-context)、[RBAC 授予与条件](/security/grants)、[审计](/common/auditing)
- 下一步：[出站地址策略](/security/outbound)
