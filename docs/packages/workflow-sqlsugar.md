# XiHan.Framework.Workflow.SqlSugar

> 工作流定义、实例与书签的 SqlSugar 持久化提供程序：替换 [Workflow](./workflow) 的进程内默认存储后，流程实例跨进程重启、跨实例共享。

- **NuGet**：`XiHan.Framework.Workflow.SqlSugar`
- **模块类**：`XiHanWorkflowSqlSugarModule`
- **所在层**：基础设施层
- **关键依赖**：[Workflow](./workflow)（存储契约与引擎）、[Data](./data)（SqlSugar 客户端、工作单元、建表）

## 概述

[Workflow](./workflow) 的三个存储端口——定义、实例（含执行历史）、书签——默认是有界的进程内字典：进程重启即全部丢失，也不跨实例。本包把它们落到四张表，并保证引擎的并发模型在数据库上依然成立。

## 何时使用

- 流程需要跨进程重启存活（审批挂起数天、定时器等待数小时）
- 应用以多实例部署，任一实例都要能办理任务、接收信号
- 已在用 [Data](./data)，希望工作流数据与业务数据用同一套连接配置

不需要本包的场景：只在单进程内跑短流程，重启丢失可以接受。

## 安装与启用

```bash
dotnet add package XiHan.Framework.Workflow.SqlSugar
```

```csharp
[DependsOn(typeof(XiHanWorkflowSqlSugarModule))]
public class YourAppModule : XiHanModule
{
}
```

建表需要开启 [Data](./data) 的初始化，**两个开关默认都是关闭的**：

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

::: warning 多实例应启用 Redis 分布式锁
引擎靠实例级分布式锁串行化同一实例的推进，定时器 Worker 靠分布式锁保证集群单活。默认的进程内锁不跨实例。本包的书签删除守卫保证同一书签只被一个节点推进，但同一实例的不同书签被多个节点同时恢复时仍会相互覆盖。配置 [Caching](./caching) 的 Redis 连接后框架自动换成 Redis 锁；使用默认锁时本包在启动时记录警告。
:::

## 表结构

四张表，**都不分表**，只建在平台库。

| 表 | 内容 | 主要索引 |
| --- | --- | --- |
| `sys_workflow_definition` | 定义：编码、版本、状态；节点、连线、变量声明、扩展属性各一个 JSON 列 | `(Code, Version)` 唯一 |
| `sys_workflow_instance` | 实例：状态、相关性、父子关系、故障信息；变量与汇聚波次为 JSON 列 | 状态、定义编码、相关性、父实例 |
| `sys_workflow_node_instance` | 执行历史：每次节点执行一行；输入、输出、私有状态为 JSON 列；`Sequence` 为创建顺序 | `(Instance_Id, Start_Time, Sequence)` |
| `sys_workflow_bookmark` | 书签：种类、索引键、到期时间、相关性；附加数据为 JSON 列 | 实例、节点实例、到期时间、`(Kind, Bookmark_Key, Creation_Time)` |

主键为契约的字符串标识。租户列 `Tenant_Id` 原样保存契约给的值，不参与全局租户过滤——契约规定存储不做租户过滤，隔离由引擎与任务服务在结果上执行。

## 工作原理

### 每次操作独立提交

存储的每次读写都在一个新开的事务型工作单元里执行，并在返回前提交；调用方若已在一个工作单元里，本包另开一条连接，不加入它。

原因是引擎的锁协议：同一实例的推进在实例锁内完成，锁一释放，下一个持锁者必须能读到上一个持锁者的全部写入。若存储加入请求的工作单元，写入要等请求结束才提交，另一节点在锁释放之后、提交之前拿到锁，会读到旧状态并重复推进。读同样走独立事务，保证读到最新提交、且在配置了从库时走主库。

代价是：在业务事务里启动流程后业务回滚，流程实例仍在。

### 书签消费守卫

引擎消费书签时先删除书签、再开始执行批次。本包的 `DeleteAsync` 在未删到任何行时抛 `WorkflowException`：两个节点竞争同一书签时，后到者在批次开始之前放弃，定时器 Worker 与信号投递把它当作「已被并发处理」跳过，人工办理的调用方得到「书签不存在或已被处理」。

### 书签匹配

| 查询 | 调用方 | 条件 |
| --- | --- | --- |
| 到期书签 | 定时器 Worker 每轮 | `Due_Time <= now`，按到期时间升序，取配置的条数 |
| 信号 | `PublishSignalAsync` | 种类为信号、键为信号名；相关性非空时再要求书签相关性为空或相等 |
| 种类 + 键 | 待办列表、子流程回调 | 种类与键相等，按创建时间升序 |

按种类与键匹配的两个查询在数据库条件之后再做一次区分大小写的比较，因此在 MySQL 默认的不区分大小写排序规则下，`alice` 也不会看到 `Alice` 的待办。

### 执行历史顺序

取消实例时，引擎按执行历史逆序补偿。MySQL 的 `datetime` 没有小数秒，同一秒开始的节点只能靠 `Sequence`（由雪花标识解析）排序，本包按 `Start_Time, Sequence` 返回。

## 配置

配置节 `XiHan:Workflow:SqlSugar`（`XiHanWorkflowSqlSugarOptions.SectionName`）。

| 配置项 | 类型 | 默认值 | 说明 |
| --- | --- | --- | --- |
| `ConfigId` | `string?` | 空 | 工作流表所在连接的配置标识；为空时用 `XiHan:Data:SqlSugarCore:DefaultConfigId` |

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanWorkflowSqlSugarModule` | 模块类，声明依赖即启用 |
| `SqlSugarWorkflowDefinitionStore` | `IWorkflowDefinitionStore` 的 SqlSugar 实现 |
| `SqlSugarWorkflowInstanceStore` | `IWorkflowInstanceStore` 的 SqlSugar 实现 |
| `SqlSugarWorkflowBookmarkStore` | `IWorkflowBookmarkStore` 的 SqlSugar 实现 |
| `WorkflowSqlSugarExecutor` | 三个存储共用的独立事务执行器 |
| `SysWorkflowDefinition` / `SysWorkflowInstance` / `SysWorkflowNodeInstance` / `SysWorkflowBookmark` | 四个实体 |
| `XiHanWorkflowSqlSugarOptions` | 连接配置 |

## 注意事项与最佳实践

- **变量必须可 JSON 序列化**。变量字典的值类型是 `object`，读回后每个值都是 `JsonElement`（对象属性名与字典键保持写入时的原样），需经 `WorkflowVariables.Get<T>` / `WorkflowValueConverter` 取值；表达式求值会自动归一化。
- **JSON 列按原属性名写入**。此前写入的数据保留 camelCase 属性名：强类型读取不区分大小写，照常读回；此前写入的变量里的 POCO 值，键仍是 camelCase。
- **删除实例前先删书签**：先 `IWorkflowBookmarkStore.DeleteByInstanceAsync`，再 `IWorkflowInstanceStore.DeleteAsync`。
- **推进过程不是原子的**：恢复、启动、取消、重试都由多次独立提交组成，进程在中途崩溃或重新部署会留下运行中却没有书签的实例；定期查询这类实例并人工处理。
- **书签删除比内存实现严格**：`DeleteAsync` 删除不存在的书签抛 `WorkflowException`。
- **书签 `UpdateAsync` 在行不存在时不插入**，与内存实现（按标识覆盖写入）不同。
- **已完成实例永久保留**，清理策略由应用实现。
- **按编码的查询在数据库条件之后再做区分大小写的比较**，`Leave` 与 `leave` 是两个编码；但唯一索引 `(Code, Version)` 在不区分大小写的排序规则（MySQL 默认）下会让 `Leave` 与 `leave` 互相冲突，Code 请保持大小写一致。
- **SQLite 不适合与外层事务共用**：外层已写同一个库时，存储的独立连接会撞 `database is locked`。
- **在服务作用域内解析引擎**：存储是 Scoped。

## 扩展点 / 自定义

实现 `XiHan.Framework.Workflow.Abstractions.Stores` 下的契约并在 DI 中 `Replace`。四个 `*Mapper` 静态类公开契约与实体的映射，可供运维工具直接读写表数据。

## 依赖模块

- [Workflow](./workflow)：存储契约、引擎、定时器 Worker
- [Data](./data)：SqlSugar 客户端解析、工作单元、建表初始化

## 相关模块

- [Workflow.Abstractions](./workflow-abstractions)：存储端口与运行时模型
- [Caching](./caching)：Redis 分布式锁
