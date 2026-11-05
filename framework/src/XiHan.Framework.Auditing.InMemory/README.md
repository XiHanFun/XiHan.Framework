# XiHan.Framework.Auditing.InMemory

## 关于

`XiHan.Framework.Auditing.InMemory` 是审计的默认实现：用有界环形缓冲保存最近的审计事件。只在单进程内有效，重启即失，适合开发、测试与单实例应用。

## 主要特性

* 保留最近 `Capacity` 条事件，满了丢最旧的
* 每条事件可同时写一条日志
* 要持久化时换 `Auditing.Database`

## 如何使用

### 安装

```bash
dotnet add package XiHan.Framework.Auditing.InMemory --prerelease
```

### 使用

启用特性（`Profiles.Full` 已包含）：

```csharp
builder.AddXiHan(xihan => xihan
    .UseStandardProfile()
    .UseFeature<InMemoryAuditingFeature>());
```

```json
{ "XiHan": { "Auditing": { "InMemory": { "Capacity": 10000, "LogEvents": true } } } }
```

## 主要类型

此库提供的主要类型：

* `InMemoryAuditingFeature`：启用入口
* `InMemoryAuditTrail`：接收器与查询
* `InMemoryAuditingOptions`：`Capacity`、`LogEvents`

## 更多文档

* [概念文档](https://framework.docs.xihanfun.com/packages/auditing-inmemory)
* [审计指南](https://framework.docs.xihanfun.com/guide/auditing)

## 相关包

* 审计抽象：[XiHan.Framework.Auditing.Abstractions](https://www.nuget.org/packages/XiHan.Framework.Auditing.Abstractions)
* 数据库实现：[XiHan.Framework.Auditing.Database](https://www.nuget.org/packages/XiHan.Framework.Auditing.Database)

## 反馈与贡献

`XiHan.Framework.Auditing.InMemory` 以 [MIT 许可证](https://licenses.nuget.org/MIT) 开源。欢迎到 [GitHub 仓库](https://github.com/XiHanFun/XiHan.Framework) 提交问题和贡献。
