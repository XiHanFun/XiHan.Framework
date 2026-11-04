# XiHan.Framework.Security.Ip2Region

IP 归属地的 ip2region 离线库 Provider：按宿主提供的 IPv4 / IPv6 xdb 文件把 IP 地址解析为国家、省份、城市与运营商，不访问网络，提供 `security.ip-region@1`。适用于登录日志、风控条件（`environment.geo.*`）等需要归属地的场景。

| 项 | 内容 |
| --- | --- |
| 包 ID | `XiHan.Framework.Security.Ip2Region` |
| 环层 | Ring 3（实现） |
| 能力 | 提供 `security.ip-region@1`；依赖 `kernel.hosting@1` |
| 特性入口 | `Ip2RegionSecurityFeature`（清单 `xihan.security.ip2region`，`role: provider`） |
| 配置节 | `XiHan:Security:Ip2Region` |
| 依赖 | `XiHan.Framework.Security.Abstractions`、`IP2Region.Net` 3.0.2、`Microsoft.Extensions.Logging.Abstractions` 10.0.13；公开面不暴露第三方类型 |
| AOT | 兼容 |
| 稳定性 | Preview |

## 概述

`Ip2RegionSecurityFeature` 以单例 `Ip2RegionResolver` 实现 `IIpRegionResolver`（ADR-0047）。IPv4 与 IPv6 各一个 xdb 文件，首次用到某个地址族时整库读进内存（`CachePolicy.Content`，线程安全），之后的查询都在内存里完成；按 README 的记载，IPv4 库约 11 MB、IPv6 库约 37 MB。

框架包不带数据文件，xdb 由宿主提供（下载或生成方式见 ip2region 项目）。某个地址族的文件不存在或加载失败时记一条警告，之后该族一律无值，应用照常运行。

归属地是可选能力：不装本特性时容器里没有 `IIpRegionResolver`，调用方把归属地当作取不到，引用它的条件子句为无法判定。

## 何时使用

- 需要离线、不出网的 IP 归属地：用本包。
- 想换成在线服务或其他离线库：只依赖 `Security.Abstractions` 的 `IIpRegionResolver` 契约，自己实现并登记为单例。

## 安装与启用

```xml
<PackageReference Include="XiHan.Framework.Security.Ip2Region" Version="5.0.0-preview.1" />
```

5.0 尚未发布到 nuget.org，预览包的取得方式见[快速上手](../quickstart#获取预览包)。

```csharp
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;
using XiHan.Framework.Security.Ip2Region;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<Ip2RegionSecurityFeature>());

var app = builder.Build();
app.UseXiHan();
app.Run();
```

xdb 文件随应用发布，例如在项目文件里复制到输出目录：

```xml
<ItemGroup>
  <None Include="IpDatabases/*.xdb" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

## 配置

| 键（`XiHan:Security:Ip2Region`） | 类型 | 缺省 | 说明 |
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

路径不能为空；文件在首次解析该地址族时才加载，配置错了不会让启动失败，只会让该族一律无值并记警告。

## 主要类型

| 类型 | 说明 |
| --- | --- |
| `Ip2RegionSecurityFeature` | 特性入口 |
| `Ip2RegionOptions` | `V4Database`、`V6Database`；`FromConfiguration` 读取上表 |
| `Ip2RegionResolver` | `IIpRegionResolver` 的实现，`IDisposable`，释放已加载的查询器 |

结果类型 `IpRegion`（`Country`、`Province`、`City`、`Isp`、`CountryCode`，展示文本 `Location`）在 [Security.Abstractions](./security-abstractions) 的 `Network` 命名空间。

## 使用示例

登录日志里记归属地，依赖契约而不是本包，没装 Provider 时照常工作：

```csharp
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Security.Abstractions.Network;

public sealed class LoginLocation(IServiceProvider services)
{
    // 没装归属地 Provider、回环或内网地址、查不到时都返回 null
    private readonly IIpRegionResolver? _resolver = services.GetService<IIpRegionResolver>();

    public string? Describe(string? remoteAddress)
        => _resolver is not null
            && IPAddress.TryParse(remoteAddress, out var address)
            && _resolver.Resolve(address).TryGetValue(out var region)
                ? region.Location
                : null;
}
```

远端地址可以从调用来源 `context.GetOrigin()` 的 `RemoteAddress` 取。

## 行为与限制

- IPv4 映射的 IPv6 地址按 IPv4 查。
- 不查的地址：回环；IPv4 的 10/8、172.16/12、192.168/16；IPv6 的链路本地、站点本地与组播。它们直接返回无值。
- 记录格式兼容两版：新版"国家|省份|城市|运营商|国家代码"，旧版"国家|区域|省份|城市|运营商"。`0` 与空段为未知；保留地址（`Reserved`）与各段全未知返回无值。
- 单次查询失败（库损坏、地址超出库的范围）按无值处理，只记调试日志，地址不进日志；`Resolve` 不抛出。
- `Location` 把国家、省份、城市、运营商去掉缺失与重复（不区分大小写）后以空格连接，例如 `中国 广东省 深圳市 电信`。

## 相关

- [授权 · IP 归属地](../guide/authorization#ip-归属地)
- [Security.Abstractions](./security-abstractions)
- 规范：RFC-0008《认证与授权》；ADR-0047
