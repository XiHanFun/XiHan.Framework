# XiHan.Framework.Workflow.SqlSugar

## 概述

`XiHan.Framework.Workflow` 的定义、实例与书签的 SqlSugar 持久化提供程序。主包的三个默认存储是有界的进程内实现，进程重启即全部丢失、不跨实例；本包把它们落到四张表。

## 核心能力

- 四张表：`sys_workflow_definition`、`sys_workflow_instance`、`sys_workflow_node_instance`、`sys_workflow_bookmark`
- 以 `Replace` 替换 `IWorkflowDefinitionStore`、`IWorkflowInstanceStore`、`IWorkflowBookmarkStore` 的默认实现，生命周期 Scoped
- 每次存储操作在独立的事务型工作单元内执行并在返回前提交，不加入调用方的工作单元；引擎释放实例锁时，写入对其他节点已可见
- 书签按实例、节点实例、到期时间、种类与索引键查询均有索引；按种类与索引键匹配区分大小写，不受数据库排序规则影响
- 执行历史按开始时间与创建顺序返回，取消时的补偿逆序与内存实现一致
- 表结构由 `DbInitializer` 在应用启动时创建，**必须开启** `XiHan:Data:SqlSugarCore` 下的 `EnableDbInitialization` 与 `EnableTableInitialization`（二者默认均为 `false`）

## 依赖关系

依赖 `XiHan.Framework.Workflow`（存储契约与默认注册）与 `XiHan.Framework.Data`（SqlSugar 客户端解析、工作单元、建表初始化）。

## 配置与约定

配置节 `XiHan:Workflow:SqlSugar`，`ConfigId` 指定工作流表所在连接的配置标识，为空时使用 `XiHan:Data:SqlSugarCore:DefaultConfigId`。四张表固定落在这一个连接上，不随租户切库；表只在平台库（静态配置的连接）建出。

表名 `sys_` 前缀、全小写下划线，不分表；列名 Pascal_Snake_Case；主键为契约的字符串标识，非自增。可变状态（变量、汇聚波次、节点输入输出与私有状态、书签附加数据）以 JSON 存储，序列化选项为 `JsonSerializerOptions.Web`。

未开启建表初始化又没有手工建表时，首次写入即抛「表不存在」。自行维护表结构时按本包实体的列与索引定义建表。

**多实例部署应启用 Redis 分布式锁。** 引擎靠实例级分布式锁串行化同一实例的推进，定时器 Worker 靠分布式锁保证集群单活。默认的 `DefaultDistributedLock` 只在进程内互斥。本包的书签存储在按标识删除未删到任何行时抛 `WorkflowException`，因此即使没有 Redis 锁，同一个书签也只会被一个节点推进；但同一实例的**不同**书签（并行分支、会签的多个受理人、超时与办理）被多个节点同时恢复时，仍会并发推进、后写覆盖先写。配置 `XiHan:Caching` 的 Redis 连接后框架自动替换为 Redis 锁；使用默认锁时本包在应用初始化时记录一条警告。

**书签删除比内存实现严格。** `IWorkflowBookmarkStore.DeleteAsync` 删除不存在或已被删除的书签会抛 `WorkflowException`（内存默认实现静默成功）；`DeleteByInstanceAsync` 不受影响。

**工作流写入不随业务回滚。** 存储的每次操作独立提交，在业务事务里启动流程后业务回滚，流程实例仍在。需要「业务失败则不启动」时，在业务提交之后再启动流程。

**SQLite 与外层事务不能共用。** 外层工作单元已写过同一个 SQLite 库时，存储的独立连接会撞 `database is locked`。

**变量必须可 JSON 序列化。** 不可序列化的变量会让执行批次在写回时抛出，实例停在运行中且书签已被消费。读回后整数与小数都是 `decimal`、嵌套对象是 `JsonElement`，业务代码应经 `WorkflowVariables` / `WorkflowValueConverter` 取值。

**删除实例前先删书签。** `IWorkflowInstanceStore.DeleteAsync` 只级联删除节点实例；应先调 `IWorkflowBookmarkStore.DeleteByInstanceAsync`，否则人工任务书签成为孤儿。

**推进过程不是原子的。** 每次恢复书签先删除书签并提交，再逐个节点提交执行结果，最后提交实例状态；启动先插入实例再执行；取消、终止、重试同理。在这些提交之间进程崩溃或重新部署，会留下状态为运行中、却没有任何书签的实例，它不会再被推进，也不报错。内存实现崩溃时什么都不留下，这是持久化带来的新情况。建议定期查询 `sys_workflow_instance` 中 `Status = 1` 且在 `sys_workflow_bookmark` 中没有书签的实例，人工判断后取消或终止。

**已完成实例永久保留。** 本包不自动清理，保留策略由应用决定。

**编码、相关性等字符串比较随数据库排序规则。** 定义编码、实例列表的过滤条件交给数据库比较，MySQL 默认不区分大小写；书签匹配另做序数过滤，不受影响。

**MySQL 的时间没有小数秒。** `datetime` 写入时四舍五入到秒，读回的时间可能与写入相差不到一秒；执行历史顺序由 `Sequence` 列保证。

**插入与更新不是 upsert。** 重复主键插入抛出数据库异常；更新不存在的标识什么也不做。同一编码的定义并发创建会撞 `(Code, Version)` 唯一索引，调用方需重试。

**存储是 Scoped 服务。** 在服务作用域内解析 `IWorkflowEngine` 等服务，不要从根容器解析。

## 使用方式

在应用启动模块上声明依赖：

```csharp
[DependsOn(typeof(XiHanWorkflowSqlSugarModule))]
public class YourAppModule : XiHanModule
{
}
```

开启建表：

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

## 扩展点

需要自定义存储行为时，实现 `XiHan.Framework.Workflow.Abstractions.Stores` 下的契约并在 DI 中 `Replace`。`WorkflowDefinitionMapper`、`WorkflowInstanceMapper`、`WorkflowNodeInstanceMapper`、`WorkflowBookmarkMapper` 为公开的静态映射，可供运维工具直接读写表数据。

## 目录结构

```
Entities/                        四个工作流实体
Mapping/                         契约与实体的双向映射、JSON 列工具
Options/                         存储配置
Stores/                          执行器与三个存储实现
Extensions/DependencyInjection/  服务注册扩展
```
