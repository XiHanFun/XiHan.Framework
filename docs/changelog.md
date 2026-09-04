# 更新日志 · XiHan.Framework

本文件记录 XiHan.Framework 各版本的变更。每条标注 **新增 / 修复 / 优化 / 调整 / 升级 / 移除** 类别。只收录使用者可感知的变更，仓库自身的配置、CI、测试工程与构建脚本不列入。框架以 NuGet 包形式发布，升级前请留意「调整」类中的破坏性变更。

## v4.2.0 (2026-09-04)

::: warning 升级须知
`XiHan.Framework.Serialization` 不再引用 `Newtonsoft.Json`——该包只用 `System.Text.Json`，那条引用一直是死引用。此前靠它传递拿到 `Newtonsoft.Json` 的下游工程将拿不到，需自行声明。用到 `XiHan.Framework.Data` 的应用不受影响，`Newtonsoft.Json` 仍由 SqlSugarCore 传递引入。

另有两处产物与运行平台脱钩，输出可能与上一版不同：`NormalizeLineEndings` 固定产出 `\n`（此前在 Windows 上产出 `\r\n`），`SanitizeFileName` / `IsValidFileName` 改按各平台限制的并集处理（此前在 Linux 上只挡 `\0` 与 `/`）。
:::

- **新增** `PathHelper.PathComparison` / `PathComparer`：本机文件系统路径的大小写口径改由运行平台决定，`PathEquals` / `IsSubPath` / `GetCommonPath` 与虚拟文件系统的物理路径去重键、变更追踪缓存都改走它——此前硬编码 `OrdinalIgnoreCase`，在 Linux 上会把 `/app/Data` 与 `/app/data` 两个不同目录判成同一个；虚拟路径与嵌入资源名是逻辑标识不是本机路径，一律 `Ordinal`
- **新增** 建表扫描不再要求实体继承 `IEntityBase`，标了 `[SugarTable]` 即是候选
- **修复** `IsPathSafe` 与 `IsSubPath` 用裸前缀匹配判目录穿越：`basePath` 为 `/app/data` 时 `/app/database/secret` 前缀成立即被判为安全，改为卡在分隔符边界上
- **修复** 要跨平台流转的数据不再跟着运行平台走：净化后的文件名按各平台限制的并集处理（保留名与末尾的点、空格无条件判定），对象存储的键只认 `/`（`\` 在 S3 / OSS / COS 的键里是合法字符），`SplitToLines` 三种行尾都认、`NormalizeLineEndings` 产物固定为 `\n`
- **调整** `XiHan.Framework.Serialization` 移除 `Newtonsoft.Json` 引用
- **升级** 升级依赖，发布 v4.2.0

## v4.1.0 (2026-08-31)

::: warning 升级须知
本次含多处破坏性变更：

- 默认节点命名统一为 `Default`：EventBus 的 RabbitMQ `ExchangeName=Default`、`QueueName` / `ClientProvidedName=Default.EventBus`，Kafka `TopicName` / `GroupId=Default.EventBus`，Redis `StreamKey=Default:EventBus:Stream`、`ConsumerGroup=Default.EventBus`；Tasks 的 Redis 作业存储 `KeyPrefix=Default:BackgroundJobs`；Caching / Authentication / Workflow 内部 Redis 键前缀由 `xihan:` 改为 `default:`；AI 知识库默认集合名改为 `default_knowledge`。升级后旧队列里未消费的消息、旧键上的锁与待处理作业不会自动迁移，需要沿用旧名的在 `appsettings` 里显式配置回去；知识库集合改名后须重建或重新摄取
- 模块分库配置从顶层 `ConnectionConfigs` 条目改为父连接下的 `ModuleDataSourceConfigs`，条目只写模块名与连接串、其余字段继承父连接，`ConfigId` 由框架派生为 `{父连接}_{模块名}`（原 `Erp` 变为 `Default_Erp`）；实体特性 `[DataSource]` 更名为 `[ModuleDataSource]`
- 库隔离租户的模块表落点从共享模块库（`Default_{模块名}`）改为该租户自己的模块库（库名 `{租户库名}_{模块名}`）。升级后这些是新建的空库，共享模块库里属于这些租户的存量行需自行搬迁；不想迁的把 `XiHan:Data:SqlSugarCore:EnableTenantModuleDatabaseConvention` 置为 `false` 即可维持原状
- 三个数据访问接口新增成员，自定义实现需补齐：`ISqlSugarClientResolver.GetCurrentLayoutConfigIds()`、`ISqlSugarTenantConnectionResolver.GetModuleDataSourceNames()`、`IDbInitializer.InitializeCurrentLayoutAsync()`
- `EnableAutoCheckOnStartup` 现在真的会执行迁移。此前 `UpdateScripts` 下的脚本一条都没跑过，升级到本版后它们会在启动时按序全部执行，升级前请先备份数据库
:::

- **新增** 实体按模块数据源分库路由：实体标 `[ModuleDataSource("Erp")]` 即固定落在该模块库上，租户上下文保持统一；模块维度与租户维度正交，落到哪条连接由「模块名 + 当前租户」共同决定，模块名不再占用顶层 `ConfigId` 命名空间。仓储 `DbClient` 与 `CreateQueryable<T>` 改为按实体解析，声明的库无对应连接时 fail-closed；建表初始化同口径收窄（声明了模块库的实体只在自己的库建表），`DataSeederBase` 增加 `DbClientFor<T>()`
- **新增** 库隔离租户按约定自带整套模块库：租户连接描述符没声明模块库时，按默认布局逐条镜像给它，库名由租户主库名派生；显式声明优先于约定，认不出库名字段（如 Oracle）直接抛而不静默回落公共模块库；开关 `EnableTenantModuleDatabaseConvention` 默认开
- **新增** SignalR 会话闸门 `SessionStateHubFilter`：建连与方法调用走与 HTTP 侧同一个 `ISessionStateGate`，已登出、被踢下线、已过期的会话不再能收实时推送；会话抽象由 `Web.Api/Session` 下沉到 `Web.Core/Session`，HTTP 中间件与选项仍留在 `Web.Api`
- **新增** `GetClaimsIgnoringLifetime`：只放过有效期，签名与发行者 / 受众照验；刷新令牌只在令牌过期后才被调用，挂在旧解析路径上的会话有效性与模仿态判定此前恒空转
- **新增** 模仿态原语 `ICurrentUser` / `ClaimsPrincipal` 的 `IsImpersonating()` 与 `XiHanClaimsIdentityExtensions.BuildImpersonatorClaims(...)`，调用方不必手拼声明字符串
- **修复** 启动自检只建版本记录、从不执行迁移：`IUpgradeEngine.ExecuteAsync` 全仓零调用方，`UpdateScripts` 下的脚本写下去就没跑过、`sys_version.db_version` 恒停在 `0.0.0`，给既有实体加字段后部署即 42703 且不报任何错。改为自检之后同步执行迁移，失败抛出中断启动；未注册引擎时安静跳过
- **修复** 建库建表遍历漏掉模块库：`DbInitializer` 自己拼的名单只有顶层 `ConfigId`，派生出的模块库整批掉出循环且全程零异常，改为走 `ISqlSugarClientResolver.GetAllConfigIds()`；库隔离租户开通时只初始化主库，补 `IDbInitializer.InitializeCurrentLayoutAsync()` 按当前布局把主库与模块库一起建
- **修复** 模块另指一个物理库却没声明从库时照搬父库从库，读会落到别的库上；改为只在模块不分库时才继承
- **修复** 模仿者用户标识三处用 `Guid.TryParse` 解析，而系统用户主键全链路是 `long`，写进去后恒解析成 `null`——不抛异常、不报编译错，模仿态判定静默失效
- **修复** 模块库派生出的 SQLite 库文件名跟着运行平台走：连接串是配置数据，却按 `Path.*` 的平台语义解析——Linux 上反斜杠不算分隔符，`C:\data\qqq.db` 被整串当文件名，派生成 `C:\data\qqq_Erp.db`；`Path.Combine` 还会把分隔符换成当前平台的，改掉原连接串的风格。改为按字符切分，两种分隔符都认，目录段连分隔符一起原样保留
- **升级** 发布 v4.1.0

## v4.0.1 (2026-08-28)

- **调整** 模块加载树不再写入应用日志管道，仅保留启动期控制台输出
- **升级** 升级依赖，发布 v4.0.1

## v4.0.0 (2026-08-28)

::: warning 升级须知
本次为主版本升级，含多处破坏性变更：

- `XiHan.Framework.SearchEngines.Abstractions` 包内类型的命名空间补上 `Abstractions` 段，与程序集名一致（如 `XiHan.Framework.SearchEngines.ISearchEngine` → `XiHan.Framework.SearchEngines.Abstractions.ISearchEngine`）
- `YamlHelper` 由 `XiHan.Framework.Utils.Text.Yaml` 移到 `XiHan.Framework.Utils.Serialization.Yaml`，与同组 Yaml 类型归并；未提供过渡类型转发，按旧命名空间 `using` 的代码需同步调整
- Base36 / Base58 / Base62 / Base95 与自定义进制的 `Encode` 改为大端读入，多字节编码结果与此前不同，已落库的旧编码需按旧规则重算（单字节编码不受影响）
- `Stack` 扩展 `Clone` / `Where` / `Select` 的返回栈顺序由「与源栈相反」改为「与源栈一致」；属性差异比较由「恒返回空列表」变为返回真实差异
- 分布式与混合缓存的 `KeyPrefix` 由死配置变为真正生效。未配置该项的部署产出的键与此前逐字节一致；已配过该项的部署键名会多出前缀，旧键读不回来、等其自然过期
:::

- **修复** Castle 拦截器上真异步 `Task<T>` 方法被拦截即死锁：包装任务写回 Castle 返回值槽位后又从同一槽位读回 await，等的是它自己
- **修复** 本地对象存储的分片上传每次都失败：独占写入流未释放即对同一路径读回算哈希，异常被兜底 catch 吞成 `Success=false`
- **修复** Scriban 模板上下文桥接从未生效：类型别名遮蔽导致交给 Scriban 的是框架自己的上下文对象，模板里取不到任何变量、渲染结果全空
- **修复** 脚本执行的 `TimeoutMs` 形同虚设，改为竞速等待并抛 `ScriptTimeoutException`
- **修复** Sqids 默认配置下编码不可逆（补位处 `char + char` 走整数加法，拼进去的是码点之和的十进制文本），`SnowflakeIdOptions.Clone` 直接返回自身的假克隆会让两个生成器共用 WorkerId
- **修复** 缓存 5 处缺陷：`GetMany` / `GetOrAddMany` 在工作单元部分命中时结果与入参键错位、`KeyPrefix` 全 src 无读取点、Redis 启用后容器里存在两条 `IDistributedCache` 注册、`CacheAspect` 不识别 `ValueTask`、`XiHanHybridCache` 构造函数的无用形参
- **修复** 集合扩展的谓词重载解析回自身导致无限递归（调用即 `StackOverflowException`），进制编解码字节序不一致，`CustomRadix` 缓冲区容量公式的底数与真数写反，属性差异比较因重载绑错恒返回空列表
- **修复** `IsNullOrEmpty` 泛型重载把非 null 的空集合判成「非空」，调用方的空集合短路全部失效
- **修复** 工具库一批公共 API 实际不可用：`FormatXml` / `CompressXml` 恒返回空串、二进制文本编解码不可逆致水印往返拿回乱码、`CompareJson` 对标量比原文、`MaskEmail` 正则无捕获组把邮箱脱敏成 `@.`、确定性 GUID 版本位写错字节、ECIES 解密按错误曲线推算临时公钥长度、JSON 选项 `IgnoreNullValues` 从未被读取与 `MergeJson` 的类型降级
- **修复** 六个机器人子包（DingTalk / Email / Lark / Sms / Telegram / WeCom）的注册扩展不传 `configure` 时选项服务无人登记，容器构建期即失败；飞书 `TagButton.Type` 默认值拼写为 `defult`
- **修复** 事件总线自定义处理器工厂解析失败不再让整条触发链连坐，本地事件总线的处理器生命周期缺陷
- **修复** Serilog 异步管道的 `EnableAsyncLogging` / `AsyncBufferSize` / `BlockWhenFull` 三个选项接入实现，此前全仓零读取点、配了等于没配
- **修复** 文件日志编码与日志配置构建器缺陷、后台服务统计与优雅停止缺陷、配置扩展与异常扩展及日志扩展缺陷
- **修复** 运行时长改用单调时钟，NTP 校时回拨后不再算出负的运行时长
- **修复** 分页异步入口的取消令牌不再被忽略（`PageQueryExecutor.ExecuteAsync`、`PageExtensions.ToPageResultAsync`、`PageConverter.ConvertItemsAsync`）
- **修复** Web.Docs 生成的 XML 文档 ID 缺方法泛型参数个数标记，泛型方法的注释在 Swagger 上永远查不到
- **修复** Web.Core 的声明转换改为幂等，`IClaimsTransformation` 被多次调用不再累积重复声明
- **修复** 审计日志队列容量补下界校验，`capacity=0` 不再造出「永远是满的」队列（日志静默丢弃、反压卡死请求线程）
- **修复** `Workflow.Abstractions` 的 `ConvertTo<T>` 传入空值不再对值类型抛空引用
- **调整** `SearchEngines.Abstractions` 与 `YamlHelper` 的命名空间归位
- **升级** 升级依赖，发布 v4.0.0

## v3.14.0 (2026-08-26)

::: warning 升级须知
八家 OAuth 提供商改为框架自研实现，认证包不再引用 `AspNet.Security.OAuth.GitHub` / `.Gitee` / `.QQ` 与 `Microsoft.AspNetCore.Authentication.Google`，NuGet 依赖只剩 JwtBearer 与 ASP.NET Core 共享框架。配置节的既有键名保持不变，直接引用过这些第三方包类型的代码需改到框架的 `XiHanOAuthHandler` 一侧。
:::

- **新增** 八家 OAuth 提供商全部自研，新增微信、企业微信、飞书、钉钉登录；每家支持账号授权与扫码两种登录方式，同一家可用不同 `Name`、相同 `Provider` 注册成两个方案
- **新增** `OAuthProviderConfig` 增加 `Provider`、`Mode`、`AgentId`、`CorpId`、`LoadMemberProfile`、`AuthorizationEndpoint` 与 `AuthorizationParameters`；端点与声明类型集中登记为 `OAuthProviderEndpoints` 与 `OAuthClaimTypes`
- **调整** 移除四个第三方 OAuth 提供商包，企业微信扫码改用新版端点，资料改走 `auth/getuserdetail`，不再强依赖通讯录权限
- **修复** OAuth 令牌响应体不再原文进日志，按字段名抹掉取值并截断至 512 字
- **修复** 状态串还原改为与搬运同样门控，扫码回调上多传一个 `_oauthstate` 不再顶掉真实 state 让登录失败
- **修复** 微信不再按远端回显的权限范围决定是否拉取资料，不再静默产生资料全空的账号
- **修复** 飞书重写的令牌请求补回 `code_verifier`，启用 PKCE 时不再被直接拒绝，校验串也不再随票据进登录 Cookie
- **修复** `Scopes` 回到追加语义（微信与企业微信的两种登录方式权限范围互斥，仍整体替换），显式配置不再挤掉提供商默认值导致邮箱补取被静默关闭
- **修复** 钉钉令牌响应改为在原字段上补标准字段名并写出 corpId 声明；企业微信通讯录占位值不再覆盖真实资料；GitHub 与 Gitee 的邮箱补取失败不再中断登录
- **修复** 日志滚动选名改按已分配字节数，磁盘大小不足以判断
- **修复** `IXiHanMethodInvocation.ReturnValue` 改回可空，修正错误的不可空标注
- **升级** 升级依赖，发布 v3.14.0

## v3.13.1 (2026-08-17)

- **修复** 分页执行器未应用 Skip/Take，分页接口返回整表匹配记录而非当前页（Domain.Shared）
- **修复** 分页克隆丢失关键字匹配模式且浅拷贝共享实例、超页时当前页计数为负、关键字字段先去重后 trim、验证器死分支等五处（Domain.Shared）
- **修复** `Sm2Helper` 静态构造崩溃与签名 / 验签把 DER 当密钥误用，SM2 签名验签全链路重写（Security，此前公开 API 完全不可用）
- **修复** 命令行解析器停止标记后参数丢失前缀、未知选项被静默接受、大小写配置不生效、Command 恒为空（DevTools）
- **修复** TypeFinder 静默吞掉程序集扫描异常，改为留痕日志后跳过（Core）
- **修复** 应用释放时模块关闭钩子兜底执行，非主机路径不再静默丢失（Core）
- **修复** 钉住 SQLitePCLRaw 2.1.13，修复 SqlSugarCore 传递依赖高危漏洞 CVE-2025-6965
- **优化** NuGet 包接入 SourceLink，可直接调试进框架源码
- **升级** 升级依赖，发布 v3.13.1

## v3.13.0 (2026-08-16)

- **新增** 动态连接注册器支持注销，外部数据源改口令或停用后无需重启
- **修复** `[UnitOfWork]` 在 HTTP 入口生效，动作抛异常时事务回滚
- **修复** `[Cacheable]` 与 `[CacheEvict]` 在 HTTP 入口生效
- **升级** 升级依赖，发布 v3.13.0

## v3.12.1 (2026-08-14)

- **新增** 建表与种子支持按需选取，可按分组、目标库与连接配置范围细分
- **升级** 升级依赖，发布 v3.12.1

## v3.12.0 (2026-08-13)

- **修复** 类级 `[DynamicApi(Name)]` 生效为控制器名，插件与宿主的同简名服务可共存
- **修复** 控制器名判重改为忽略大小写，与方法级路由判重一致
- **升级** 升级依赖，发布 v3.12.0

## v3.11.1 (2026-08-12)

- **修复** Redis 未启用时 `IRedisDelayQueue<>` 不注册导致应用起不来，改为默认注册进程内实现

## v3.11.0 (2026-08-11)

- **新增** 新增严格隔离多租户实体标记 `IStrictMultiTenantEntity`，读写口径都按相等收紧
- **优化** 383 处 `<inheritdoc />` 替换为完整 XML 文档注释，IntelliSense 上不再是空白
- **升级** 升级依赖，发布 v3.11.0

## v3.10.1 (2026-08-06)

::: warning 升级须知
本版把目录类配置的默认值统一改为帕斯卡命名：升级脚本目录 `migrations` → `UpdateScripts`，签名密钥 `keys/` → `Keys/`，日志 `logs/` → `Logs/`，本地存储 `wwwroot/uploads` → `wwwroot/Uploads`。只改默认值、不动读取逻辑，已在配置中显式写死路径的部署不受影响；**依赖默认值且部署在区分大小写的文件系统上的，需要一并迁移这些目录**。`wwwroot` 保持小写不变，它由 ASP.NET 固定解析。
:::

- **调整** 目录类配置默认值统一帕斯卡命名，升级脚本目录由 `migrations` 改为 `UpdateScripts`
- **升级** 升级依赖，发布 v3.10.1

## v3.10.0 (2026-08-05)

::: warning 破坏性变更
本版三处。动态 API 的路由段只认显式 `[FromRoute]`，此前按参数名自动生成的 Id 路由段全部消失（`GET /api/User/User/{id}` 变为 `GET /api/User/User?id=1`），需保持原 URL 的请显式标注。`IDistributedCache` 移除两个 Lua 脚本成员，改用 `ICacheSupportsLuaScript` 的中立签名。工作单元回滚后再调 `CompleteAsync` 由静默返回改为抛出，自行实现 `IUnitOfWork` 的需补 `IsRolledback` 成员。
:::

- **新增** 新增 `Web.Mcp` 包，MCP Server 接入下沉至框架，应用只需声明模块依赖
- **新增** 新增 OpenID Connect 协议层，含签名密钥、id_token 签发与发现文档
- **新增** 搜索引擎从空壳改为可用抽象，拆出契约包与进程内实现
- **新增** 新增 Elasticsearch 搜索实现，同一套契约在两种实现上行为一致
- **新增** Redis 事件总线接管滞留消息并引入死信，消除待处理列表的永久孤儿
- **新增** 动态 API 新增控制器名唯一性校验，装配错误在启动期暴露
- **调整** 破坏性变更：路由段只由显式 `[FromRoute]` 产生，参数名不再决定 URL
- **调整** 破坏性变更：缓存抽象移除 Lua 脚本成员并改用中立签名，不再出现 StackExchange.Redis 类型
- **调整** 破坏性变更：工作单元回滚后再提交改为抛出，`IUnitOfWork` 新增 `IsRolledback`
- **修复** 修复约定注册使 Scoped / Singleton 服务的拦截器全体静默失效
- **修复** 回滚后的连接不再被复用，消除接口返回 200 但一行没写
- **修复** 分布式事件改到事务提交成功之后才发布，消除幽灵事件
- **修复** 修复收件箱幂等三处失效，重复消息不再被内联再处理一遍
- **修复** 动态 API 装配失败改为 fail-fast，不再静默丢掉整个服务的端点
- **修复** 动词前缀按词边界匹配，方法名不再被腰斩成错误路由
- **修复** 修复日志缓存容量检查清空全表，`Flush` 改为真正等到落盘
- **修复** 切换日志目录与清空日志时重置滚动状态，滚动按已分配字节判断
- **修复** `ApiResponse.Code` 的数字转换器改标在属性上，code 恒为数字
- **移除** 移除 `Observability` 的数据库与 Redis 健康检查，两者从不实际探测
- **升级** 升级依赖，发布 v3.10.0

## v3.9.0 (2026-07-30)

- **新增** 知识向量集合的维度与集合名可配置，不再锁定 1536 维嵌入模型
- **新增** 向量库不可达翻译为 503 并纳入健康检查，覆盖检索与摄取全部 I/O 点
- **新增** 嵌入调用失败翻译为可操作消息，带上提供方与模型名
- **修复** `requiresNew` 工作单元改用独立物理连接与事务，修复内层提交静默失效
- **升级** 升级依赖，发布 v3.9.0

## v3.8.0 (2026-07-23)

- **新增** 新增动态连接注册器，支持运行期登记外部数据库连接并按 `ConfigId` 解析
- **调整** 数据库元数据读取显式指定连接时改为直连连接作用域，不再登记进当前工作单元
- **修复** 动态连接注册器改注入单例 `SqlSugarScope`，修复启动期依赖注入生命周期校验崩溃
- **升级** 升级依赖，发布 v3.8.0

## v3.7.0 (2026-07-18)

- **新增** 新增工作流引擎，含契约包 `Workflow.Abstractions` 与实现包 `Workflow`
- **新增** 新增会话状态闸门中间件，会话失效返回 401、锁定返回 423，在全部已认证端点生效
- **新增** 数据层接线行版本乐观锁，修复并发丢失更新与软删行被并发复活
- **新增** 软删仓储新增含软删写路径与物理清除通路，恢复操作可用化、批量操作幂等化
- **新增** 开放接口日志记录完整请求与响应内容，并补记凭证归属用户与 API 名称
- **调整** 会话「锁屏」正名为「锁定」，并透传锁定原因
- **调整** 解耦 ADO 命令超时与慢 SQL 日志阈值
- **修复** 修复工作单元事务与 `SqlSugarScope` 异步上下文脱钩导致的静默丢写
- **修复** 写路径租户边界，禁止租户态改写平台全局行与异租户行，并防护插入劫持
- **修复** 从库全灭时回退主库，消除全零权重导致的读路径崩溃
- **修复** 重建额外全局过滤器机制 `GlobalFilters`，修复原实现一经使用即抛异常
- **修复** 差异日志按主键对齐逐行落库，修复批量写审计只记录首行
- **修复** 修复按审计查询的仅软删选项恒返回空集
- **修复** 仓储写操作移除显式查询过滤器，修复与差异日志叠加的参数同名冲突
- **修复** 令牌会话过期时间优化
- **升级** 升级依赖，发布 v3.7.0

## v3.6.0 (2026-07-15)

::: warning 破坏性变更
本版移除 `QueryBehavior`、`PageRequestDtoBase.Behavior` 与 `WithoutPaging()`，不分页查询请改走仓储既有的 List 方法（`GetAllAsync` / `GetListAsync`）。设置加密需配置 `XiHanAesOptions.Key`，未配置将抛出异常。
:::

- **新增** 缓存 `CacheEvict` 真正执行失效，授权评估 `RequiredClaims`，新增设置定义管理器 `ISettingDefinitionManager`
- **新增** 流量灰度新增 IP 匹配器 `IpAddressGrayMatcher`，支持精确 IP 与 CIDR
- **新增** 安全新增国密 SM4 对称加密 `Sm4Helper`
- **新增** 分析器新增 `XHFA001` 规则，禁止直接 new `HttpClient`
- **新增** 开发工具 `DevTools` 升为一等模块，命令行接入依赖注入
- **调整** 破坏性变更：移除 `QueryBehavior`、`PageRequestDtoBase.Behavior` 与 `WithoutPaging()`，不分页查询改走仓储 List 方法
- **调整** 破坏性变更：设置加密改走 `XiHanAesOptions.Key`，移除硬编码占位密钥，未配置密钥即抛异常
- **调整** 元数据字段改为 const / readonly，`TargetFramework` 由程序集特性派生
- **修复** 分布式 ID 生成器改为从配置构建，修复多节点重复 ID
- **修复** 审计日志字段级脱敏与请求头脱敏
- **修复** 差异日志软删除 / 恢复被误记为更新，超长快照产出非法 JSON
- **修复** Blowfish 改用随机 IV，修复相同明文产出相同密文及解密未按实际长度截断
- **修复** 审计日志在 `DropOnFull` 开启时重复入队
- **移除** 移除 `EntityChangeInterceptor`，差异日志统一为单一通道
- **升级** 升级依赖，发布 v3.6.0

## v3.5.0 (2026-07-10)

- **新增** 链路追踪与可观测性对齐 OpenTelemetry 与 W3C 标准
- **新增** `Observability` 接入 OTel SDK，支持 OTLP 与 Console 导出，新增配置节 `XiHan:Observability`
- **新增** SqlSugar DB span，每条 SQL 产出挂在请求 span 下的子 span
- **新增** `EventBus` 消费端 Consumer span、Redis 缓存操作 span，异常记录到当前 span
- **新增** Serilog 日志携带 `TraceId` 与 `SpanId`，可与链路关联查询
- **调整** `TraceId` 与 `EventBus` `CorrelationId` 收敛为同一 W3C `TraceId`
- **调整** 网关 `TraceId` 对齐 W3C 标准
- **调整** Metrics 改用 `System.Diagnostics.Metrics.Meter`，支持 OTLP 与 Prometheus 导出
- **优化** 控制台与文件日志模板将链路 ID 移至日志级别之后
- **升级** 升级依赖，发布 v3.5.0

## v3.4.0 (2026-07-08)

- **新增** 新增 Gitee 第三方 OAuth 登录 Provider
- **修复** 多租户基类补齐 `IMultiTenantEntity`，修复租户行过滤全程失效
- **升级** 升级依赖，发布 v3.4.0

## v3.3.0 (2026-07-08)

- **新增** 分布式事件总线新增 RabbitMQ / Kafka / Redis 三种 Broker Provider
- **新增** 新增后台作业管理器 `IBackgroundJobManager`，支持一次性即发即忘作业
- **新增** 后台作业新增 Redis 持久化存储
- **调整** 审计日志通用件下沉至新包 `XiHan.Framework.Auditing`
- **调整** 密码哈希器与选项收归 `Security` 包并由其自注册
- **修复** `CurrentPrincipalAccessor` 匿名请求兜底，修复匿名访问空引用
- **升级** 升级依赖，发布 v3.3.0

## v3.2.0 (2026-07-06)

- **新增** 新增 AI 抽象包 `XiHan.Framework.AI.Abstractions`，支持 OpenAI 兼容 Provider 解析与会话门面
- **新增** AI Provider 解析支持 Invalidate 配置热切换
- **新增** 新增 RAG 检索增强底座，含嵌入 Provider 与向量抽象
- **新增** 新增 Agent 门面与 MCP 工具投影，技能注册表自动收纳
- **新增** AI 新增护栏中间件、遥测与缓存管道开关、提示词库默认源
- **新增** SqlSugar 主从读写分离配置完整暴露，新增连接配置构建前钩子与追加式 AOP
- **新增** 从库支持健康探针，租户连接支持从库
- **调整** 移除 MySQL 存量表 utf8mb4 兜底转换
- **修复** 从库读权重 `HitRate` 绑定失效导致从库不分担读
- **修复** MySQL 初始化强制 utf8mb4，修复 emoji 写入报错
- **升级** 升级依赖，发布 v3.2.0

## v3.1.0 (2026-07-03)

- **新增** 新增 `Bot.Sms` 短信子包
- **新增** Telegram 支持多机器人平台，含 Webhook 中间件与内置 /start /help /myid 命令
- **新增** 新增入站限流与三态熔断，默认关闭
- **新增** 支持运行时按租户注册 SqlSugar 连接
- **调整** Bot 库拆分为 Email / Sms / Telegram / DingTalk / Lark / WeCom 六个 Provider 子包，配置 store 化
- **调整** 返回码语义化并对齐 HTTP 标准，新增 10000+ 业务码区段
- **调整** `IBotClient` 发送方法返回 `BotDispatchResult`，支持显式通道与取消令牌
- **修复** `BotResult` 工厂自递归导致的堆栈溢出
- **修复** Bot 投递结果被门面丢弃，Email 取消被吞成发送失败
- **修复** `IsPasswordReusedAsync` 改为加盐哈希比对，修复历史密码永不命中
- **升级** 升级依赖，发布 v3.1.0

## v3.0.1 (2026-06-27)

- **新增** 新增国际化机制，含请求文化中间件、异常可本地化与响应本地化兜底
- **新增** 支持时区切换
- **新增** 排序字段由 C# 属性名解析并映射为物理列名，标准化表名与列名
- **调整** 撤销分页方法统一走 POST 的约定，改由各方法显式标注 `HttpPost`
- **修复** JSON 本地化资源无 backing 程序集时崩溃
- **修复** `ApplyFilter` 健壮值强转，修复 In 类型不匹配与可空字段处理

## v3.0.0 (2026-06-20)

- **新增** 新增分布式锁、队列与延迟队列
- **新增** 新增一次性验证码服务 `IOneTimeCodeService`
- **新增** 实时通信新增后台任务进度事件常量 `TaskProgress`
- **新增** 本地存储 Provider 支持预签名 URL
- **优化** 日志体系规范化，支持敏感脱敏、查询不落操作日志与软删恢复识别
- **优化** `RandomCoder` 全面加密安全化，异常统一化输出
- **修复** `DateTimeOffset` 按 ISO 8601 带时区偏移序列化，返回码按数值序列化
- **修复** 本地存储静态文件注册在鉴权前导致上传目录 401
- **修复** 动态 API 特性布尔默认值覆盖全局配置
- **修复** Cron 时区统一、六段秒位、调度死亡显性化与一次性触发语义
- **移除** 移除消息后台发送，改由业务层接管

## v2.5.0 (2026-05-30)

- **新增** 新增混合授权与 ABAC 策略，落地权限体系与登录日志
- **新增** 新增分析器包 `XiHan.Framework.Analyzers`
- **新增** 新增请求链路追踪 ID 与 `ApiLog` 写入管道
- **新增** 新增密码策略服务、消息发件箱与实体变更拦截器
- **新增** 枚举元数据增强，支持动态 JSON 加载与变更重载
- **调整** 架构重构，内核分层与启动层初始化
- **调整** 仓储参数注入移入 `DataExecuting` AOP
- **优化** 收紧 OpenApi 算法基线并强化防重放
- **优化** `AuditLog` 更名为 `DiffLog`，减少歧义
- **修复** `EventBus` 后台服务停止异常
- **修复** 动态 API 取消令牌绑定与命名空间路由、去重、绑定一致性问题
- **移除** 移除分表仓储与基类索引

## v2.4.0 (2026-04-08)

- **新增** 新增第三方登录支持
- **新增** 新增 Castle 动态代理集成库与缓存 AOP 拦截
- **新增** 新增动态 API 权限保护
- **新增** 数据库初始化下沉到框架
- **调整** 各模块内联注册提取为 `AddXiHanXxx` 扩展方法，可脱离模块系统直接注入
- **调整** Contracts 接口去除实体泛型参数，消除应用层 HTTP 形状泄漏
- **修复** long 序列化为字符串，避免 JavaScript 精度溢出
- **修复** HTTP 请求 string 类型重复序列化

## v2.3.3 (2026-03-14)

- **新增** 实现机器人功能，支持邮件、钉钉、飞书、企业微信与 Telegram
- **新增** 实现消息模块功能，重写 AI 功能
- **新增** 新增异步日志管道，中间件与过滤器改为可插拔管道
- **新增** 新增统一请求上下文 `RequestContext`
- **新增** 新增 OpenAPI 签名加密与分布式安全升级引擎
- **新增** 新增动态 API 分组、标签能力与数据库元数据能力
- **调整** 接口返回默认改为 camelCase，统一返回结果封装
- **调整** 强化工作单元失败回滚语义
- **修复** 仓储条件更新与删除的租户过滤缺口
- **修复** CRUD 服务基类的 DTO 到实体映射覆盖失效

## v2.2.0 (2026-02-17)

- **新增** 新增租户数据隔离、租户解析透传与多租户实体基类
- **新增** 新增通用响应封装与错误消息混淆
- **调整** 租户 ID 由 GUID 统一改为 long
- **优化** 优化分页查询与自动分页仓储，可指定分页方法
- **优化** 优化 OpenAPI 文档与标签功能
- **优化** 优化 SqlSugar ID 生成与分布式 ID 配置注入
- **修复** 分页不生效，单审计实体无法使用仓储
- **修复** HTTP 库 Fluent API 设置不生效，动态 API 参数误加入路径

## v2.0.0 (2026-01-26)

- **新增** 新增数据库初始化与种子数据初始化服务
- **新增** 实现认证授权体系，含鉴权、授权默认实现与密码配置项
- **新增** 文件系统支持云存储（本地 / 阿里云 OSS / 腾讯云 COS / MinIO）及图片、视频处理
- **新增** 分布式 ID 提供默认注入
- **调整** 部分包抽离为抽象层
- **优化** 重构仓储结构与参数类型，优化聚合根与数据访问基类
- **修复** 动态 API 多版本丢失、版本号不正确与服务未暴露
- **升级** 全框架升级至 .NET 10，同步升级所有依赖

## v1.4.6 (2026-01-26)

- **修复** HTTP 库 Fluent API 设置不生效

## v1.4.5 (2025-11-12)

- **调整** 仓储实现分页，统一审计字段，重构 CRUD 服务与表达式扩展
- **优化** Base 编码转换器性能优化，内存分配减少约 70-80%
- **优化** 优化分布式 ID 生成库与命名空间
- **修复** ID 工厂泛型类型与 HTTP 请求序列化问题
- **修复** VS2026 将 `Reverse` 扩展误映射为 `Span.Reverse`
- **移除** 移除默认 ID 注入与 snk 签名
- **升级** 升级依赖，发布 v1.4.5

## v1.4.3 (2025-10-27)

- **新增** 新增动态 WebApi 功能
- **新增** 补充更多基础类型转换能力
- **调整** 分页重构，统一仓储方法名
- **修复** 字符串强转可空数值类型时转换报错
- **升级** 升级依赖，发布 v1.4.3

## v1.4.2 (2025-10-23)

- **新增** HTTP 包实现代理功能
- **优化** 优化扩展方法，避免二义性污染
- **升级** 升级依赖，发布 v1.4.2

## v1.4.1 (2025-10-19)

- **新增** 完善租户接口，优化租户解析与租户设置
- **修复** 修复依赖注入的默认实现
- **升级** 发布 v1.4.1

## v1.4.0 (2025-10-19)

- **新增** 新增高性能线程安全内存缓存，支持惰性清理、事件通知与多种过期淘汰策略
- **新增** 新增设置中心默认实现，完善本地事件总线
- **新增** 新增顺序 GUID 生成器与锁扩展方法
- **优化** 优化日志清理逻辑
- **升级** 日志组件升级为 `Task` + `Channel` 异步架构，新增可插拔格式化器与背压策略
- **升级** 升级依赖，发布 v1.4.0

## v1.3.6 (2025-10-17)

- **调整** 调整开发工具与命名空间组织
- **修复** 修复命令赋值错误
- **升级** 升级第三方依赖

## v1.3.5 (2025-10-16)

- **修复** 修复 HTTP 模块扩展相关问题
- **修复** 修复主键构造函数缺陷

## v1.3.2 (2025-10-10)

- **新增** 新增计划任务功能，完善调度能力
- **调整** 重写任务调度，去除第三方库依赖改为自研
- **调整** 调整命名空间
- **优化** 优化并扩充仓储功能与实体建模
- **升级** 升级第三方依赖

## v1.3.1 (2025-09-24)

- **新增** 引入默认模板引擎
- **优化** 优化 HTTP 请求包

## v1.3.0 (2025-09-20)

- **新增** 新增领域模块 `XiHan.Framework.Domain`，完善实体与 ID 建模
- **新增** 新增后台服务基类与配套示例
- **新增** 新增命令行工具集，含进度条、加载指示器、彩色输出与交互式菜单
- **新增** 新增表格打印，支持自适应列宽与多种边框样式
- **新增** 新增 gRPC 库 `XiHan.Framework.Web.Grpc`
- **调整** 计划任务相关项目重命名，包名统一，`Data` 模块结构优化
- **优化** 重写文件日志，大幅提升写入性能
- **升级** 多轮升级第三方依赖

## v1.2.1 (2025-09-08)

- **优化** 精简与优化模块依赖引用
- **修复** 修复包命名错误

## v1.1.0 (2025-09-07)

- **新增** 新增 `Security`、`Script`、`DevTools` 等模块
- **新增** 重写序列化器，新增 JSON 转换器、序列化 Try 方法与动态 JSON
- **调整** 大规模包重构整合，`AspNetCore.Mvc` 并入 `Web.Api`、Serilog 并入 `Logging`、`SqlSugarCore` 并入 `Data`、SignalR 并入 `Web.RealTime`
- **调整** 合并 Swagger 与 Scalar 为 `Web.Docs`，对象映射与后台任务相关包合并
- **优化** 扩充表达式功能，优化重试、系统信息获取与对象映射
- **移除** 移除 `BlobStoring`、`DataFiltering`、`AspNetCore.Refit` 等模块
- **升级** 升级依赖，发布 v1.1.0

## v0.11.7 (2025-08-02)

- **新增** 新增渐变控制台打印与项目信息展示
- **新增** 扩充动态 JSON 与数字扩展方法
- **新增** 集合新增随机取项能力
- **调整** `AspNetCore.Serilog` 更名为 `XiHan.Framework.Logging`，重构元数据包
- **优化** 优化日志模块、应用启动流程与默认序列化选项
- **移除** 移除 `Serializable` 标记，统一使用 `System.Text.Json`
- **升级** 升级依赖，发布 v0.11.7

## v0.11.3 (2025-07-03)

- **优化** Windows 硬件信息采集由 wmic 替换为 PowerShell
- **移除** 移除运行时已自带的重复功能
- **升级** 升级依赖，发布 v0.11.3

## v0.11.2 (2025-06-29)

- **修复** 修复 JSON 字符串格式化问题
- **移除** 移除 EFCore 库，ORM 统一使用 `SqlSugarCore`
- **升级** 升级依赖，发布 v0.11.2

## v0.11.1 (2025-06-25)

- **新增** 定义事件总线抽象接口，新增事件处理器工厂与事件追踪 ID 接口
- **新增** 新增对象扩展包、类型辅助类与验证异常
- **调整** 重构本地化包，`AssemblyHelper` 更名为 `ReflectionHelper`
- **优化** 优化 HTTP 模块返回结果与序列化
- **优化** 优化 RSA 加密默认参数、脱敏逻辑与 `RandomCoder`
- **修复** 动态 JSON 访问不存在属性时报错，改为返回 `null`
- **升级** 升级依赖，发布 v0.11.1

## v0.9.4 (2025-06-05)

- **新增** 扩充工具包，新增农历、异步屏障、数据预测与摩尔斯编码
- **新增** 新增双向链表、堆栈、队列扩展方法与身份主体抽象基类
- **新增** 新增 OpenAPI 支持
- **升级** 升级第三方依赖

## v0.9.3 (2025-06-03)

- **新增** 新增事件总线，支持本地与分布式事件处理器
- **新增** 新增 `XiHan.Framework.Data` 包
- **调整** 对象访问器由工具包迁移至核心包
- **优化** 优化启动日志
- **修复** 修复服务提供器为空与 AI 模块注入问题

## v0.9.1 (2025-06-02)

- **新增** 新增领域服务与仓储接口
- **新增** 工作单元管理器支持子工作单元

## v0.9.0 (2025-06-02)

- **新增** 新增 `XiHan.Framework.Script` 包，支持脚本安全执行与调试
- **调整** 数据检测器 `CheckHelper` 更名为 `Guard`
- **优化** 优化 HTTP 包引用与整体包结构

## v0.8.35 (2025-05-31)

- **新增** 新增 `DistributedIds`、`Validation`、`Authorization` 包，ID 算法补充 NanoId
- **新增** 扩充编码方案，新增 Base32 / Base36 / Base58 / Base62 / Base95 与自定义进制编码
- **调整** `Data` 包更名为 `DataFiltering`，`ValidateCoder` 更名为 `RandomCoder`
- **修复** 修复包引用错误
- **移除** 移除 UUID 生成器
- **升级** 升级依赖

## v0.8.31 (2025-05-17)

- **修复** 修复 AI 依赖升级后的接口兼容问题
- **移除** 移除非常用功能
- **升级** 升级 `XiHan.Framework.AI` 依赖

## v0.8.30 (2025-05-16)

- **新增** 新增 Sqids 短 ID 编解码，支持代码动态加载
- **优化** 优化 Python 解释器、日志输出与字符串方法
- **修复** 修复 Sqids 算法缺陷
- **升级** 升级第三方依赖

## v0.8.28 (2025-05-06)

- **新增** 新增数据库访问库与 SqlSugar 包
- **新增** 新增雪花 ID、UUID 生成器与文本数字水印工具
- **新增** 新增 MCP 包、SSE 通讯、模板引擎、SFTP / SSH 连接与 YAML 解释器
- **新增** 新增简单的国际化处理器
- **优化** 优化 RSA 加密与程序集依赖相关方法
- **升级** 多轮升级第三方依赖

## v0.8.20 (2025-04-02)

- **新增** 新增 `XiHan.Framework.Http.Client` 包
- **优化** 优化工作单元处理逻辑
- **修复** 判空统一改为 `is null` 写法，规避运算符重载风险
- **升级** 升级依赖

## v0.8.18 (2025-03-19)

- **优化** 细节优化

## v0.8.17 (2025-03-18)

- **新增** 新增本地化包 `XiHan.Framework.Localization`
- **新增** 新增混合缓存、深度合并配置与主题枚举
- **优化** 优化设置与多租户模块引用
- **修复** 修复模块间循环依赖
- **移除** 移除多余包引用
- **升级** 升级至 .NET 10 及相关依赖

## v0.8.15 (2025-02-25)

- **新增** 新增安全库与设置库
- **新增** 新增 DDD 实体、聚合根与领域事件基础能力
- **新增** 新增对象映射包，集成 Ollama 与 OpenAI 调用
- **新增** 新增数据脱敏工具类，强化虚拟文件系统的文件监听
- **调整** 重命名日志与实时通信等包
- **优化** 优化 HTTP 请求处理、接口文档注入与虚拟文件系统包
- **移除** 移除虚拟文件系统的文件缓存功能

## v0.8.10 (2025-01-26)

- **调整** 调整命名空间组织
- **优化** 优化分页处理逻辑
- **升级** 升级依赖

## v0.8.9 (2025-01-21)

- **修复** 补丁修复若干问题
- **升级** 升级依赖并推进版本号

## v0.8.7 (2025-01-05)

- **升级** 升级第三方依赖

## v0.8.5 (2024-12-16)

- **新增** 新增工作单元、线程与多租户模块
- **新增** 新增文本模板与虚拟文件系统模块
- **新增** 新增 Redis 缓存包
- **新增** 新增接口文档包

## v0.8.4 (2024-12-10)

- **修复** 修复 AspNetCore 包对象访问器注入问题

## v0.8.3 (2024-12-10)

- **新增** 新增 OTP 与 HMAC 生成能力
- **移除** 移除 `AspNetCore.MVC` 目录

## v0.8.0 (2024-12-06)

- **新增** 新增 `AspNetCore.MVC` 库
- **新增** 新增 XML 解析与表达式扩展方法
- **优化** 优化树节点处理功能

## v0.7.5 (2024-12-04)

- **新增** 通用分页支持多条件筛选与多字段排序
- **新增** 新增数据过滤与树形数据处理能力
- **新增** 新增国密算法支持
- **新增** 引入机器学习与人工智能相关依赖
- **升级** 多目标支持 .NET 8.0 与 9.0

## v0.7.0 (2024-11-13)

- **修复** 修复时间转换为简易字符串时结果错误
- **升级** 框架整体升级到 .NET 9

## v0.5.9 (2024-10-28)

- **优化** 细节优化

## v0.5.8 (2024-10-28)

- **优化** 优化模块化服务配置基类

## v0.5.7 (2024-10-28)

- **优化** 优化应用扩展实现

## v0.5.6 (2024-10-28)

- **新增** 新增核心包应用扩展
- **优化** 核心包功能收尾完善

## v0.5.5 (2024-10-27)

- **调整** 更新工具包命名空间与项目引用关系

## v0.5.4 (2024-10-27)

- **新增** 新增核心包模块功能
- **移除** 移除插件包

## v0.5.3 (2024-10-26)

- **优化** 细节优化
- **升级** 发布 v0.5.3

## v0.5.2 (2024-10-20)

- **优化** 优化工具类与加密相关工具

## v0.5.1 (2024-10-11)

- **新增** 奠基解决方案与基础库，搭建框架整体骨架
- **新增** 新增鉴权授权、工作单元、后台任务、领域驱动、缓存等核心基础设施项目
- **新增** 新增 ORM 数据库访问、客户端库与序列化项目
- **新增** 新增代码生成、接口文档、网关、插件、工具等配套项目
- **升级** 统一 .NET 版本管理，补齐 NuGet 打包文件
