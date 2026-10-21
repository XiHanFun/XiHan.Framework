# 错误码

```text
ErrorCode = Domain "_" Kind "_" Sequence
Domain    = 术语表错误码域（大写）或应用自定义的大写标识
Kind      = VALIDATION | NOTFOUND | CONFLICT | DENIED | UNAVAILABLE | TIMEOUT | CANCELLED | UNEXPECTED | 领域自定义
Sequence  = 三位数字，域内唯一，一经发布不复用
```

示例：`PERSISTENCE_CONFLICT_001`、`SECURITY_DENIED_003`、`KERNEL_CAPABILITY_001`。

## 规则

1. 框架错误码在本文登记，新增走 RFC 附件。
2. 错误码进入 `Error.Code`，HTTP 适配器把它放进 RFC 9457 Problem Details 的 `type` 或扩展字段，gRPC 放进 trailing metadata，插件协议放进响应元数据。
3. 消息面向人，错误码面向程序；客户端只应依赖错误码。

## 已登记的框架错误码

| 错误码 | 含义 |
| --- | --- |
| `PRIMITIVES_VALIDATION_001` | 原语参数非法（空 Code、空 Message、清单跨字段约束等） |
| `PRIMITIVES_VALIDATION_002` | 使用了未初始化的 Result（default 值） |
| `KERNEL_CAPABILITY_001` | 能力解析失败：缺失依赖 |
| `KERNEL_CAPABILITY_002` | 能力解析失败：循环依赖 |
| `KERNEL_CAPABILITY_003` | 能力解析失败：版本约束冲突 |
| `KERNEL_CAPABILITY_004` | 同一能力有多个候选且未指定 |
| `KERNEL_CAPABILITY_005` | 清单 ID 重复 |
| `KERNEL_PIPELINE_001` | 中间件顺序约束成环 |
| `KERNEL_PIPELINE_002` | 阶段名未知 |
| `KERNEL_PIPELINE_003` | 中间件重复注册，或顺序约束引用了未注册或跨阶段的中间件 |
| `KERNEL_HOSTING_001` | 必需绑定缺失或类型不匹配 |
| `KERNEL_HOSTING_002` | 特性启动或停止失败 |
| `KERNEL_HOSTING_003` | 特性停止超时 |
| `KERNEL_HOSTING_004` | 应用状态不允许该操作（重复构建、未就绪时调用） |
| `KERNEL_UNAVAILABLE_001` | 应用未就绪或正在停止，暂不接收调用（HTTP 503） |
| `KERNEL_TIMEOUT_001` | 调用超过截止时间 |
| `KERNEL_CANCELLED_001` | 调用被取消 |
| `KERNEL_UNEXPECTED_001` | 调用期间发生未处理异常 |
| `KERNEL_SECURITY_001` | 权限码重复 |
| `KERNEL_SECURITY_002` | 权限注册表已满 |
| `PLUGINS_VALIDATION_001` | 插件清单、种类或隔离级别不合法 |
| `PLUGINS_VALIDATION_002` | 插件入口类型未实现 IPlugin，或实例清单与文件清单不一致 |
| `PLUGINS_NOTFOUND_001` | 插件清单文件、程序集或入口类型不存在 |
| `PLUGINS_DENIED_001` | 插件声明的权限未被宿主授予 |
| `PLUGINS_CONFLICT_001` | 同一清单 ID 的插件已加载 |
| `PLUGINS_UNEXPECTED_001` | 插件加载、配置、启动或停止时发生未处理异常 |
| `PERSISTENCE_VALIDATION_001` | 多租户实体在没有当前租户的调用中新增 |
| `PERSISTENCE_VALIDATION_002` | 工作单元状态不允许该操作（重复开始、未开始就提交、只读却写入） |
| `PERSISTENCE_NOTFOUND_001` | 更新或删除的实体不存在（含已软删除、不属于当前租户） |
| `PERSISTENCE_CONFLICT_001` | 乐观并发冲突：并发戳不匹配 |
| `PERSISTENCE_CONFLICT_002` | 新增的标识已存在 |
| `PERSISTENCE_UNAVAILABLE_001` | 没有活动的工作单元（仓储在 Transaction 阶段之外使用；以异常抛出） |
| `PERSISTENCE_UNAVAILABLE_002` | 存储容量已满（内存实现的每类实体上限） |
| `PERSISTENCE_UNAVAILABLE_003` | 存储不可达 |
| `CACHING_VALIDATION_001` | 缓存键或幂等键不合法 |
| `CACHING_VALIDATION_002` | 条目选项不合法（绝对与滑动过期同时设置等） |
| `CACHING_UNAVAILABLE_001` | 缓存存储不可达或操作失败 |
| `CACHING_CONFLICT_001` | 分布式锁被其他持有者占用 |
| `CACHING_CONFLICT_002` | 同一幂等键的调用仍在处理中 |
| `SECURITY_DENIED_001` | 未认证 |
| `SECURITY_DENIED_002` | 策略拒绝 |
| `MULTITENANCY_NOTFOUND_001` | 租户不存在 |
