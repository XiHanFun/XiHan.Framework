# XiHan.Framework.Security.Ip2Region

## 关于

`XiHan.Framework.Security.Ip2Region` 是 IP 归属地的 ip2region 离线库提供程序：按宿主提供的 xdb 文件把 IP 解析为国家、省份、城市与运营商，不访问网络。登录日志与风控条件（`environment.geo.*`）等需要归属地的地方会用到它。

## 主要特性

* 同时支持 IPv4 与 IPv6
* 回环、内网与查不到的地址返回无值，不抛异常
* 某个地址族的库文件缺失时只记警告，应用照常运行

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Security.Ip2Region --prerelease
```

### 使用

包里不带数据，xdb 文件由应用提供并随应用发布：

```csharp
builder.AddXiHan(x => x.UseFeature<Ip2RegionSecurityFeature>());
```

路径相对应用根目录，缺省如下：

```json
{ "XiHan": { "Security": { "Ip2Region": { "V4Database": "IpDatabases/ip2region_v4.xdb", "V6Database": "IpDatabases/ip2region_v6.xdb" } } } }
```

使用时注入 `IIpRegionResolver`，调用 `Resolve(IPAddress)`。

## 主要类型

此库提供的主要类型：

* `Ip2RegionSecurityFeature`：启用 ip2region 归属地的特性
* `Ip2RegionResolver`：`IIpRegionResolver` 的实现
* `Ip2RegionOptions`：配置项，对应 `XiHan:Security:Ip2Region`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/security-ip2region)

## 相关包

* 安全抽象（含 IP 归属地契约）：[XiHan.Framework.Security.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Security.Abstractions)

## 反馈与贡献

`XiHan.Framework.Security.Ip2Region` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
