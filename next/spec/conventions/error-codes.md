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
4. 文案不写在代码里：错误携带消息键（`<领域>Messages` 常量）与参数（`Error.Details`），文案在所属包的 `Localization/<区域>.json`（10 种区域各一个文件，RFC-0010）；一个错误码可以对应多个消息键。

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
| `KERNEL_NOTFOUND_001` | 操作未登记 |
| `KERNEL_VALIDATION_001` | 操作载荷不合法（非 JSON、null 或类型不匹配） |
| `KERNEL_VALIDATION_002` | 类型化调用的请求或响应类型与登记不符 |
| `KERNEL_OPERATION_001` | 操作 ID 重复登记 |
| `KERNEL_OPERATION_002` | 操作登记表已满 |
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
| `MESSAGING_VALIDATION_001` | 主题不合法 |
| `MESSAGING_VALIDATION_002` | 信封不合法或无法解码 |
| `MESSAGING_UNAVAILABLE_001` | 消息代理不可达或操作失败 |
| `MESSAGING_UNAVAILABLE_002` | 队列已满 |
| `MESSAGING_UNEXPECTED_001` | 处理器抛出未处理异常（代理侧记录） |
| `LOCALIZATION_VALIDATION_001` | 区域名不合法 |
| `LOCALIZATION_VALIDATION_002` | 消息目录不合法（JSON、键或区域名错误） |
| `LOCALIZATION_CONFLICT_001` | 同名消息目录重复注册 |
| `SECURITY_DENIED_001` | 未认证 |
| `SECURITY_DENIED_002` | 策略拒绝 |
| `SECURITY_DENIED_003` | 凭据无效、过期或签名不符 |
| `SECURITY_DENIED_004` | 缺少权限 |
| `SECURITY_VALIDATION_001` | 不支持的认证方案或凭据格式不合法 |
| `SECURITY_NOTFOUND_001` | 策略不存在或权限码未登记 |
| `PROTOCOLS_VALIDATION_001` | MCP：消息不是合法的 JSON 或不是合法的 JSON-RPC 2.0 请求 |
| `PROTOCOLS_VALIDATION_002` | MCP：协议版本不受支持 |
| `PROTOCOLS_VALIDATION_003` | MCP：tool 参数不是 JSON 对象 |
| `PROTOCOLS_VALIDATION_004` | MCP：推导或配置的 tool 名不合法（启动失败） |
| `PROTOCOLS_NOTFOUND_001` | MCP：JSON-RPC 方法不受支持 |
| `PROTOCOLS_NOTFOUND_002` | MCP：tool 未登记 |
| `PROTOCOLS_CONFLICT_001` | MCP：两个操作得到同一个 tool 名（启动失败） |
| `PROTOCOLS_VALIDATION_005` | gRPC：请求不是合法的 protobuf 消息或与 Schema 不符 |
| `PROTOCOLS_VALIDATION_006` | gRPC：暴露给 gRPC 的操作没有 Schema，或 Schema 无法映射为 protobuf（启动失败） |
| `PROTOCOLS_VALIDATION_007` | gRPC：推导或配置的服务 / 方法名不合法（启动失败） |
| `PROTOCOLS_CONFLICT_002` | gRPC：两个操作得到同一个服务 / 方法名（启动失败） |
| `PROTOCOLS_CONFLICT_003` | gRPC：同名 protobuf 消息在不同操作里形状不同（启动失败） |
| `PROTOCOLS_UNEXPECTED_001` | gRPC：操作的响应无法按 Schema 编码为 protobuf |
| `SECURITY_UNAVAILABLE_001` | 身份或授予存储不可达 |
| `SECURITY_UNAVAILABLE_002` | 身份或授予存储容量已满 |
| `MULTITENANCY_VALIDATION_001` | 租户 ID 不合法 |
| `MULTITENANCY_VALIDATION_002` | 调用必须带租户但没有解析到 |
| `MULTITENANCY_NOTFOUND_001` | 租户不存在 |
| `MULTITENANCY_DENIED_001` | 租户已禁用 |
| `MULTITENANCY_UNAVAILABLE_001` | 租户存储容量已满 |
