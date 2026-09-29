# XiHan.Framework.Traffic.SqlSugar

> 灰度规则的 SqlSugar 只读仓储：从 `sys_gray_rule` 表加载规则并缓存在内存里，到期自动刷新，替换 [Traffic](./traffic) 的内存实现。

- **NuGet**：`XiHan.Framework.Traffic.SqlSugar`
- **模块类**：`XiHanTrafficSqlSugarModule`
- **所在层**：基础设施层
- **关键依赖**：[Traffic](./traffic)（`IGrayRuleRepository` 契约与 `GrayRule` 模型）、[Data](./data)（SqlSugar 客户端、建表）

## 概述

[Traffic](./traffic) 的灰度引擎在每次路由决策时经 `IGrayRuleRepository.GetEnabledRulesAsync` 取回启用的规则。主包的默认实现 `DefaultGrayRuleRepository` 把规则存在进程内：重启即丢、多实例之间不共享。

本包把规则的来源换成数据库表 `sys_gray_rule`。仓储是**只读**的：规则的增删改由应用层直接对这张表操作，仓储只负责查询与缓存。

## 何时使用

- 灰度规则需要由运营后台或其他服务维护，而不是写在代码或配置文件里
- 多实例部署，需要各实例读到同一份规则

## 安装与启用

```bash
dotnet add package XiHan.Framework.Traffic.SqlSugar
```

```csharp
[DependsOn(typeof(XiHanTrafficSqlSugarModule))]
public class YourAppModule : XiHanModule
{
}
```

建表需要开启 [Data](./data) 的建表初始化，**默认是关闭的**：

```json
{
  "XiHan": {
    "Data": {
      "SqlSugarCore": {
        "EnableDbInitialization": true,
        "EnableTableInitialization": true
      }
    }
  }
}
```

## 表结构

单张表 `sys_gray_rule`，不分表、单库：

| 列 | 类型 | 说明 |
| --- | --- | --- |
| `Basic_Id` | `string`，主键 | 规则标识，由应用层赋值 |
| `Row_Version` | `long` | 版本标识列 |
| `Rule_Name` | `string(128)`，非空 | 规则名称 |
| `Rule_Type` | 整数，非空 | 规则类型（`GrayRuleType`） |
| `Is_Enabled` | 布尔，非空 | 是否启用 |
| `Priority` | 整数，非空 | 优先级 |
| `Target_Version` | `string(64)`，可空 | 目标版本 |
| `Target_Service_Id` | `string(128)`，可空 | 目标服务标识 |
| `Configuration` | 大文本，可空 | 规则配置，按字符串原样存取 |
| `Effective_Time` / `Expiry_Time` | 时间，可空 | 生效区间，读回时按 UTC 解释 |
| `Created_Time` | 时间，非空 | 创建时间 |
| `Updated_Time` | 时间，可空 | 更新时间 |
| `Remark` | `string(512)`，可空 | 备注 |

## 工作原理

- `GetEnabledRulesAsync` / `GetRuleByIdAsync` 先检查缓存是否过期：距上次加载超过 `RefreshInterval` 时，重新从库里读全部规则替换缓存；未过期直接返回缓存内容。到期后并发的读取只有一个去查库，其余等它加载完直接用新缓存
- `RefreshAsync` 立即重新加载，不看间隔；加载失败时保留原缓存并向调用方抛出异常
- 仓储注册为单例，每次加载新建一个服务作用域解析 `ISqlSugarClientResolver`，加载期间在独立的非事务工作单元里切换到平台（0 号租户）上下文：规则从平台布局的库读取，与触发刷新的请求属于哪个租户无关
- 返回的规则对象运行时类型是 `GrayRule`：灰度引擎按 `(rule as GrayRule)?.TargetVersion` 读取目标版本

## 配置

配置节 `XiHan:Traffic:SqlSugar`：

| 配置项 | 默认值 | 说明 |
| --- | --- | --- |
| `RefreshInterval` | `00:00:30` | 缓存刷新间隔，必须大于零，否则启动校验失败 |

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanTrafficSqlSugarModule` | 模块类，声明依赖即启用 |
| `SqlSugarGrayRuleRepository` | `IGrayRuleRepository` 的只读实现，单例 |
| `SysGrayRule` | 灰度规则实体 |
| `XiHanTrafficSqlSugarOptions` | 缓存刷新配置 |

## 注意事项与最佳实践

- **多实例缓存不同步**：每个实例各自刷新缓存，规则变更最坏要等一个 `RefreshInterval` 才在所有实例生效；需要立即生效时在各实例上调用 `RefreshAsync`
- **库不可用时保留上次成功加载的规则并退避重试**：自动刷新失败会记一条警告，继续返回旧规则，并在 `RefreshInterval` 与 5 秒中较小的时间后再试；从未成功加载过时返回空集合
- **仓储不提供写方法**：直接对 `sys_gray_rule` 表增删改，改完后等待刷新或调用 `RefreshAsync`
- **`Configuration` 不做 JSON 校验**：内容格式错误要到规则匹配时才会暴露
- **生效区间按 UTC 比较**：经映射写入时，`Local` 时间换算为同一时刻的 UTC，未标注时区的时间按 UTC 解释；直接写表时请写 UTC

## 扩展点 / 自定义

注册经主包的 `ReplaceGrayRuleRepository<TRepository>()` 完成；需要其他来源时，实现 `IGrayRuleRepository` 并同样用它替换。

## 依赖模块

- [Traffic](./traffic)：灰度引擎、`IGrayRuleRepository` 契约与 `GrayRule` 模型
- [Data](./data)：SqlSugar 客户端解析、建表初始化

## 相关模块

- [Settings.SqlSugar](./settings-sqlsugar)：同一套落库范式
