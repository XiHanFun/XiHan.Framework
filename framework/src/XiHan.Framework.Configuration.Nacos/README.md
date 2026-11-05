# XiHan.Framework.Configuration.Nacos

## 关于

`XiHan.Framework.Configuration.Nacos` 是远程配置的 Nacos 提供程序，直接使用 Nacos 的 HTTP 配置接口（v1，Nacos 2.x 兼容），不依赖 SDK。把 Nacos 的 `dataId` 挂到配置树上，变化时自动重载。

## 主要特性

* 长轮询感知变化，整份配置替换并触发重载
* 支持 `Json` 与 `Properties` 格式
* 支持用户名密码登录与多个服务地址按序尝试
* 地址与凭据可来自资源绑定 `nacos`

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Configuration.Nacos --prerelease
```

### 使用

在构建配置时挂接；`dataId` 为 `orders` 的内容出现在 `Orders` 节下：

```csharp
builder.Configuration.AddXiHanNacos(
    new NacosOptions { ServerAddresses = ["http://localhost:8848"], Username = "nacos", Password = "<password>" },
    new RemoteConfigurationOptions("orders") { Prefix = "Orders", PollInterval = TimeSpan.FromSeconds(15) });

// 需要在容器里取用 IRemoteConfigurationClient 时
builder.AddXiHan(x => x.UseFeature<NacosConfigurationFeature>());
```

```json
{ "XiHan": { "Configuration": { "Nacos": { "ServerAddresses": "http://nacos-a:8848,http://nacos-b:8848", "Group": "DEFAULT_GROUP", "Format": "Json" } } } }
```

## 主要类型

此库提供的主要类型：

* `AddXiHanNacos`：配置源扩展方法
* `NacosOptions`：服务地址、分组、命名空间、格式、凭据与超时
* `NacosConfigurationFeature`：登记 `IRemoteConfigurationClient`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/configuration-nacos)

## 相关包

* 配置抽象：[XiHan.Framework.Configuration.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Configuration.Abstractions)
* 一致性套件：[XiHan.Framework.Configuration.Conformance](https://www.nuget.org/packages/XiHan.Framework.Configuration.Conformance)

## 反馈与贡献

`XiHan.Framework.Configuration.Nacos` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
