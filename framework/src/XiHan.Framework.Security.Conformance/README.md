# XiHan.Framework.Security.Conformance

## 关于

`XiHan.Framework.Security.Conformance` 是安全的一致性测试套件，给实现 `IPermissionGrantStore` 的人验证实现用：继承 `PermissionGrantStoreConformance` 并通过全部用例。普通应用不需要它。

## 主要特性

* 覆盖用户与角色授予、全局与租户专属授予、通配授予与大小写区分
* 覆盖存储不可达时返回失败而不是空集合

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

## 主要类型

此库提供的主要类型：

* `PermissionGrantStoreConformance`：授予存储的一致性用例基类

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/conformance)

## 相关包

* 安全抽象：[XiHan.Framework.Security.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Security.Abstractions)
* 测试基座：[XiHan.Framework.Testing](https://www.nuget.org/packages/XiHan.Framework.Testing)

## 反馈与贡献

`XiHan.Framework.Security.Conformance` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
