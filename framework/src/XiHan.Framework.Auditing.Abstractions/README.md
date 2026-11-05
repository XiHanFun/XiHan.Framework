# XiHan.Framework.Auditing.Abstractions

## 关于

`XiHan.Framework.Auditing.Abstractions` 定义审计的契约：记录操作调用的审计中间件、在处理器里补充动作与细节的扩展，以及按条件分页查询的 `IAuditTrail`。它不存储任何东西，存储由提供程序实现。

## 主要特性

* 受保护操作自动留痕，可用 `Selection` 与 `Exclude` 调整范围
* 处理器里补充动作、资源与细节
* 按时间、动作、主体、租户、trace-id、成败过滤并分页查询

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Auditing.Abstractions --prerelease
```

### 使用

通常由实现包带入；写业务代码或自定义提供程序时才直接引用。在处理器里补充审计信息：

```csharp
public ValueTask<Result<ExportResponse>> HandleAsync(ExportRequest request, InvocationContext context)
{
    context.SetAuditAction("orders.export");
    context.AddAuditDetail("rows", 1200);
    // ...
}
```

```json
{ "XiHan": { "Auditing": { "Selection": "Protected", "IncludeSuccesses": true, "Exclude": ["profile.*"] } } }
```

## 主要类型

此库提供的主要类型：

* `AuditMiddleware`、`AuditingOptions`：审计中间件与选项
* `SetAuditAction`、`SetAuditResource`、`AddAuditDetail`：调用期间补充信息
* `IAuditTrail`、`AuditQuery`、`AuditPage`：查询契约

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/auditing-abstractions)
* [审计指南](https://framework.docs.xihanfun.com/guide/auditing)

## 相关包

* 内存实现：[XiHan.Framework.Auditing.InMemory](https://www.nuget.org/packages/XiHan.Framework.Auditing.InMemory)
* 数据库实现：[XiHan.Framework.Auditing.Database](https://www.nuget.org/packages/XiHan.Framework.Auditing.Database)
* 一致性套件：[XiHan.Framework.Auditing.Conformance](https://www.nuget.org/packages/XiHan.Framework.Auditing.Conformance)

## 反馈与贡献

`XiHan.Framework.Auditing.Abstractions` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
