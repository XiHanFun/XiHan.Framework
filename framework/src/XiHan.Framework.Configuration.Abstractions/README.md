# XiHan.Framework.Configuration.Abstractions

## 关于

`XiHan.Framework.Configuration.Abstractions` 定义远程配置与密钥配置的契约，并提供把它们接进 `IConfigurationBuilder` 的配置源。应用仍然读 `IConfiguration`；默认实现是 .NET 原生的文件与环境变量。

## 主要特性

* `AddXiHanRemoteConfiguration`：首次同步加载，之后轮询，变化时整体替换并触发重载
* `AddXiHanSecrets`：把配置键映射到密钥名，批量读取并可定时重读
* 失败时保留最后一份成功的数据，并回调 `OnError`

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Configuration.Abstractions --prerelease
```

### 使用

通常由实现包带入（如 `Configuration.Nacos`）；写自定义提供程序时才直接引用：

```csharp
builder.Configuration
    .AddXiHanRemoteConfiguration(
        () => NacosConfigurationClient.Create(nacosOptions),
        new RemoteConfigurationOptions("orders") { Prefix = "Orders", PollInterval = TimeSpan.FromSeconds(15) })
    .AddXiHanSecrets(vault, new SecretsConfigurationOptions(new Dictionary<string, SecretName>
    {
        ["ConnectionStrings:Orders"] = "orders/db-connection",
    }));
```

## 主要类型

此库提供的主要类型：

* `IRemoteConfigurationClient`：远程配置客户端契约
* `ISecretStore`、`SecretValue`：密钥库契约，文本表示固定遮蔽
* `RemoteConfigurationOptions`、`SecretsConfigurationOptions`：配置源选项

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/configuration-abstractions)

## 相关包

* Nacos 实现：[XiHan.Framework.Configuration.Nacos](https://www.nuget.org/packages/XiHan.Framework.Configuration.Nacos)
* 一致性套件：[XiHan.Framework.Configuration.Conformance](https://www.nuget.org/packages/XiHan.Framework.Configuration.Conformance)

## 反馈与贡献

`XiHan.Framework.Configuration.Abstractions` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
