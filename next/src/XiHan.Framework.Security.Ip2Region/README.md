# XiHan.Framework.Security.Ip2Region

`security.ip-region@1` 的 ip2region 离线库 Provider（Ring 3，`role: provider`）：按宿主提供的 xdb 文件把 IP 解析为国家、省份、城市与运营商，不访问网络。条件属性 `environment.geo.*`、登录日志与命中记录的归属地列用它（ADR-0047）。

```csharp
builder.AddXiHan(x => x.UseFeature<Ip2RegionSecurityFeature>());
```

```json
{ "XiHan": { "Security": { "Ip2Region": { "V4Database": "IpDatabases/ip2region_v4.xdb", "V6Database": "IpDatabases/ip2region_v6.xdb" } } } }
```

## 语义

- `IIpRegionResolver.Resolve(IPAddress)` 返回 `Maybe<IpRegion>`：IPv4 映射的 IPv6 地址按 IPv4 查；回环与私有网段（IPv4 的 10/8、172.16/12、192.168/16，IPv6 的链路本地、站点本地、组播）、保留地址、查不到都是无值，不抛出。
- xdb 文件由宿主提供（框架包不带数据，下载或生成见 ip2region 项目），相对路径按 `AppContext.BaseDirectory` 解析；缺省 `IpDatabases/ip2region_v4.xdb` 与 `IpDatabases/ip2region_v6.xdb`。某个地址族的文件不存在或加载失败时记一条警告，之后该族一律无值，应用照常运行。
- 首次用到某个地址族时整库读进内存（`CachePolicy.Content`，线程安全，IPv4 约 11 MB、IPv6 约 37 MB），之后每次查询约百纳秒。
- 记录格式兼容两版：新版"国家|省份|城市|运营商|国家代码"，旧版"国家|区域|省份|城市|运营商"；`0` 与空段为未知。
- 不装本特性时容器里没有 `IIpRegionResolver`，调用方把归属地当作取不到。

## 依赖方向

依赖 `Security.Abstractions` 与 `IP2Region.Net`；公开面不暴露第三方类型。

## 状态

P5：可用。
