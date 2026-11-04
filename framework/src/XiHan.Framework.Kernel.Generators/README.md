# XiHan.Framework.Kernel.Generators

## 关于

`XiHan.Framework.Kernel.Generators` 是 Roslyn 源生成器：扫描标注 `[Operation]` 且实现 `IOperation<TRequest, TResponse>` 的类，在编译期生成操作登记代码与每个操作的请求、响应 JSON Schema，不需要运行期反射。

## 主要特性

* 生成 `GeneratedOperations`：每个操作一个 `Add<类名>`，以及登记全部操作的 `AddGeneratedOperations`
* 生成 JSON Schema（draft 2020-12），供 HTTP 的 OpenAPI、MCP 的 tool 描述与内核的契约校验使用
* `[Required]`、`[Range]`、`[StringLength]`、XML 文档注释折进 Schema
* `XHG001`–`XHG006` 诊断：缺接口、缺序列化登记、操作 ID 文法等

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Kernel.Generators --prerelease
```

### 使用

项目里用 `[JsonSerializable]` 登记请求与响应类型，在特性的 `Configure` 里调用生成的登记方法：

```csharp
[Operation("orders.create", Expose = Expose.Http | Expose.Mcp, Permission = "orders.write")]
public sealed class CreateOrder(...) : IOperation<PlaceOrderRequest, OrderResponse> { ... }

public void Configure(FeatureContext context) => context.AddGeneratedOperations();
```

用 NuGet 包时按分析器引用，不进入运行时依赖。程序集级 `[assembly: OperationDefaults(Expose = Expose.Http)]` 设置没写 `Expose` 的操作的缺省暴露协议。

## 主要类型

此库提供的主要类型：

* `OperationAttribute`：声明操作的 ID、稳定性、暴露协议与权限
* `OperationDefaultsAttribute`：程序集级的暴露协议缺省
* `GeneratedOperations`（生成）：`AddGeneratedOperations(this FeatureContext)`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/kernel-generators)

## 相关包

* 内核：[XiHan.Framework.Kernel](https://www.nuget.org/packages/XiHan.Framework.Kernel)
* 命名与稳定性规则分析器：[XiHan.Framework.Kernel.Analyzers](https://www.nuget.org/packages/XiHan.Framework.Kernel.Analyzers)
* 实体与仓储登记的源生成器：[XiHan.Framework.Persistence.Generators](https://www.nuget.org/packages/XiHan.Framework.Persistence.Generators)

## 反馈与贡献

`XiHan.Framework.Kernel.Generators` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
