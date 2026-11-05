# XiHan.Framework.Configuration.Conformance

## 关于

`XiHan.Framework.Configuration.Conformance` 是配置提供程序的一致性测试套件，给写提供程序的人用：继承抽象类并返回夹具，验证远程配置客户端与密钥库的行为与框架一致。

## 主要特性

* `RemoteConfigurationClientConformance`：验证 `IRemoteConfigurationClient`
* `SecretStoreConformance`：验证 `ISecretStore`

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Configuration.Conformance --prerelease
```

### 使用

在提供程序的测试项目里引用本包与 xUnit v3：

```csharp
public sealed class MyRemoteConfigurationTests : RemoteConfigurationClientConformance
{
    protected override Task<RemoteConfigurationFixture> CreateFixtureAsync() => MyFixture.CreateAsync();
}
```

夹具提供被测的 `Client` 与向远端发布文档的 `PublishAsync`；密钥库的夹具提供 `Store` 与写入密钥的 `PutAsync`。`MyFixture` 是你自己的夹具子类。

## 主要类型

此库提供的主要类型：

* `RemoteConfigurationClientConformance`、`RemoteConfigurationFixture`
* `SecretStoreConformance`、`SecretStoreFixture`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/conformance)
* [一致性套件文档](https://framework.docs.xihanfun.com/packages/conformance)

## 相关包

* 抽象包：[XiHan.Framework.Configuration.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Configuration.Abstractions)
* Nacos 实现：[XiHan.Framework.Configuration.Nacos](https://www.nuget.org/packages/XiHan.Framework.Configuration.Nacos)

## 反馈与贡献

`XiHan.Framework.Configuration.Conformance` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
