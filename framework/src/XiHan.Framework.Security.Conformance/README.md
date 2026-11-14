# XiHan.Framework.Security.Conformance

## 关于

`XiHan.Framework.Security.Conformance` 是安全的一致性测试套件，给实现 `IPermissionGrantStore` 与授予管理 `IPermissionGrantAdministration` 的人验证实现用：继承 `PermissionGrantStoreConformance`、`PermissionGrantAdministrationConformance` 并通过全部用例。普通应用不需要它。

## 主要特性

* 覆盖用户与角色授予、平台 / 每个租户 / 租户专属三种范围互不渗透、通配授予与大小写区分
* 覆盖存储不可达时返回失败而不是空集合
* 授予管理：逐项结果、撤销不存在、过滤与分页；写入之前读过的判定在写入之后立即看见变化；另一个实例在时限内看见变化（多实例一致的实现覆写 `StartPeerAsync`）

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Security.Conformance --prerelease
```

### 使用

在提供程序的测试项目里继承基类并实现 `CreateStoreAsync`：

```csharp
public sealed class InMemoryPermissionGrantStoreConformanceTests : PermissionGrantStoreConformance
{
    protected override ValueTask<IPermissionGrantStore> CreateStoreAsync(IReadOnlyList<PermissionGrant> grants)
        => ValueTask.FromResult<IPermissionGrantStore>(new InMemoryPermissionGrantStore(new InMemorySecurityOptions { Grants = grants }));
}
```

访问外部存储的提供程序还要覆写 `CreateUnavailableStoreAsync`，给出一个背后存储不可达的实例。

授予管理继承 `PermissionGrantAdministrationConformance`，实现 `StartAsync()`（返回读空授予表的判定存储）与 `AdministerAsync(action)`（在一次调用里使用授予管理）。承诺多实例一致的实现覆写 `StartPeerAsync()`，返回与被测实例共用持久化与缓存后端的另一个实例的判定存储，另一个实例要在 `PeerVisibilityTimeout`（缺省 5 秒）内看见授予与撤销；默认返回 `null`，该用例跳过。

## 主要类型

此库提供的主要类型：

* `PermissionGrantStoreConformance`：授予存储的一致性用例基类
* `PermissionGrantAdministrationConformance`：授予管理的一致性用例基类

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/conformance)

## 相关包

* 安全抽象：[XiHan.Framework.Security.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Security.Abstractions)
* 测试基座：[XiHan.Framework.Testing](https://www.nuget.org/packages/XiHan.Framework.Testing)

## 反馈与贡献

`XiHan.Framework.Security.Conformance` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
