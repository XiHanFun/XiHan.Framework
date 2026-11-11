# ADR-0152 标识值显式转换且 default 不是合法值

## 状态

已采纳（规则见 `rfcs/0001-primitives.md`「稳定性级别」的「标识值」）。

## 日期

2026-11-11

## 背景

API 审查 R5 指出：子系统里以字符串承载的标识都是 `readonly record struct`，构造时按文法校验，但有两个缺口。

- **隐式转换会抛出**：`CacheKey`、`BlobKey`、`ContainerName`、`JobName`、`WorkflowName`、`ActivityType`、`SecretName` 都能从 `string` 隐式转换，不合法时抛 `ArgumentException`。.NET 设计准则要求隐式转换不抛出，因为调用点看不出那里有一次可能失败的校验。
- **`default` 冒充合法值**：值类型的 `default` 绕过构造，`Value` 声明为非空 `string`，实际是 `null`。它一路传到 Provider 才以空引用失败，报错位置远离原因。内存实现若把它当字典键，还会悄悄存下一条数据，与生产的 Provider 行为分叉。
- `GrantSubject(Kind, Id)` 与 `TriggerKey(Job, Trigger)` 有同样的 `default` 问题。

## 决策

1. **从字符串的转换改为显式**：七个类型的 `implicit operator X(string)` 改为 `explicit`。调用方写 `new JobName("cleanup")`，或 `(JobName)"cleanup"`。
2. **`default` 不是合法值**：
   - 文本存进私有字段，`Value` 只在构造过的实例上返回文本，`default` 读取抛 `InvalidOperationException`。
   - `ToString` 对 `default` 返回空串，日志与调试器显示不会因此抛出。
   - 相等比较不变，`key == default` 可以判断。
3. **组合值在构造时拒收 `default` 成员**：
   - `GrantSubject` 的 `Kind` 与 `Id` 同样对 `default` 抛出。
   - `TriggerKey` 的作业名是 `default` 时构造抛 `ArgumentException`。
   - `PermissionGrant` 的主体是 `default` 时构造抛 `ArgumentException`，`Subject` 不再能经 `with` 改写，与 `Permission` 一致。
4. **入口不逐一判断**：Provider 第一次读文本时就失败，内存实现也以文本或成员为键，所以不在每个存储方法上加 `default` 检查。
5. **架构测试守护**：`IdentifierValueTests` 检查两点：
   - 正式包没有从 `string` 的公开隐式转换。
   - 凡是带 `(string)` 构造、有 `string Value` 的公开结构，`default` 读 `Value` 抛 `InvalidOperationException`、`ToString` 为空串。

不加 `TryCreate`：各类型已有 `IsValid`，来自不可信输入的文本先判断再构造，与现有文档的写法一致。

## 影响

- 源码迁移：传字符串的地方加构造，例如：
  - `AddJob<CleanupJob>("cleanup", …)` → `AddJob<CleanupJob>(new JobName("cleanup"), …)`。
  - `store.PutAsync("docs", "a.txt", …)` → `store.PutAsync(new ContainerName("docs"), new BlobKey("a.txt"), …)`。
  - `AddNode("a", ActivityTypes.Log)` → `AddNode("a", new ActivityType(ActivityTypes.Log))`。
  - 框架内约 230 处调用按此改写。
- 依赖隐式转换选中扩展方法的调用，改写前会落到批量重载上报错，加上构造后照旧选中单条扩展方法。
- 线上形状与 JSON 编码不变。
- `CacheKey`、`GrantSubject`、`PermissionGrant` 是 `Stable`，其余是 `Preview`；GA 前修改，不需要登记豁免（ADR-0131）。
