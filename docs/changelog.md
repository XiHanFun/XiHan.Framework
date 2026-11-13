# 更新日志 · XiHan Framework

本页记录 XiHan Framework 各版本中使用者可感知的变更，每条标注 **新增 / 修复 / 优化 / 调整 / 移除** 类别；仓库自身的 CI、测试工程与构建脚本不列入。

## 未发布

### 版本与兼容

- **新增** 每个包带 SPDX 2.2 软件物料清单（包内 `_manifest/spdx_2.2/manifest.spdx.json`），列出包的组成与依赖

### 安全

- **修复** 端点或路由组上的授权约定原先顶替了操作契约的权限：在 `MapXiHanOperations()` 上加 `RequireXiHanAuthentication()` 后，没有授予的已登录用户能调用全部操作；现在两者叠加判定，全部满足才放行。`FindAuthorizationRequirement` 改为 `GetAuthorizationRequirements`，返回全部已写入的要求
- **修复** 端点上的授权要求不再被静默跳过：挂在绕过管道的端点（OpenAPI、`.proto`、健康探针、自己标注绕过的）上时宿主启动失败（`SECURITY_VALIDATION_008`），原先从不判定；`UseXiHan()` 没调或排在 `UseRouting()` 之前时，带要求的端点拒绝执行；`MapXiHanGrpc()` 上的授权约定现在每次调用都判定
- **修复** HTTP 操作的请求体改在授权之后才读，原先进管道前就整读解析，匿名调用方也能让宿主为每个请求缓冲几十 MB；请求体上限 `XiHan:Protocols:Http:MaxRequestBodyBytes`（缺省 4 MiB，超过为 `PROTOCOLS_VALIDATION_011`），有请求体时 `Content-Type` 必须是 JSON（否则 `PROTOCOLS_VALIDATION_012`）：以 `text/plain` 或表单发 JSON 的客户端要改成 `application/json`
- **修复** 进程外与 Wasm 插件读宿主配置节与绑定要运营方授予：原先插件在清单里点名哪个 `XiHan:*` 节、哪个绑定就拿到哪个，零权限的插件也能拿到 JWT 密钥、连接串与签名密钥；现在要在 `XiHan:Plugins:<OutOfProcess|Wasm>:Grants:<清单 ID>` 里授予 `config:<配置节>` 与 `binding:<绑定名>`，没有授予即拒绝加载（`PLUGINS_DENIED_001`）
- **修复** S3 命名实例必须给出 `AccessKey` 与 `SecretKey`：原先没给密钥就用宿主的 AWS 凭据链，能登记存储实例的租户管理员可以以宿主的身份读写任何桶；要用环境凭据由运营方打开 `XiHan:ObjectStorage:S3:NamedInstancesUseAmbientCredentials`
- **修复** WebAuthn 认证核对凭据属于谁：原先只在存储与响应都带用户句柄时才比对，知道口令的攻击者能用自己账号下的通行密钥完成别人的登录。`WebAuthnStoredCredential.UserHandle` 改为必填，`VerifyAuthentication` 增加 `expectedUserHandle`（先认出用户时传他，免用户名传 `null`，此时响应必须带句柄），结果新增 `UserHandle`
- **修复** OpenID Connect 缺省校验受众：原先没配 `Audience` 就不校验，共用身份提供方时发给别的应用的令牌也被接受。现在 `XiHan:Security:OpenIdConnect:Audience` 必填，没有即启动失败；签发者只为本 API 签发令牌时可以显式设 `ValidateAudience: false`
- **修复** 工作流 `Http` 活动的正文读到 4 MiB 即停：原先先整体缓冲（上限约 2 GiB）再截断，能发布流程定义的租户用户把地址指向自己的服务器就能耗尽宿主内存。`TimeoutSeconds` 不超过 `XiHan:Workflow:MaxHttpTimeoutSeconds`（缺省 300），不是正的秒数时发布即校验失败，原先静默取缺省值
- **修复** 工作流的扇出与变量大小有了上限：原先并行 `ForEach` 的子实例数不受限，`s + s` 几十步就能拼出上 GB 的字符串，级联取消只处理最新的 1000 个子实例。现在 `ForEach` 最多 `XiHan:Workflow:MaxForEachItems` 项（缺省 1000），实例变量序列化后最多 `MaxVariablesBytes`（缺省 8 MiB），`+` 拼出的字符串最长 4194304 个字符，级联覆盖全部子实例
- **修复** 文件系统对象存储的配额改为边写边预留：原先只检查不预留，并发写入能合计超出 `MaxTotalBytes`，并发覆盖或删除同一个键会少记用量、之后一直能写到重启。覆盖写入现在需要新内容全长的余量
- **修复** 文件系统对象存储的分片暂存有了上限：原先暂存的分片不计入配额、上传数不限，过期的上传只在启动时清掉且之前照样能传分片。现在暂存计入 `MaxTotalBytes`，未完成的上传最多 `XiHan:ObjectStorage:FileSystem:MaxMultipartUploads`（缺省 1000），过期的上传拒绝再传分片与完成，运行期间也会清掉
- **修复** 进程外插件的 stdout / stderr 一行最多 16384 个字符：原先宿主按行整读，插件写一行不带换行的几十 MB 输出就让宿主整行装进内存。超过的部分丢弃并记一条告警，握手行超长即握手失败
- **修复** Wasm 插件沙箱补齐资源上限：睡在 WASI `poll_oneoff` 里的调用原先中断不了，宿主要等它睡完，现在最多睡到调用的截止时间即以 `KERNEL_TIMEOUT_001` 结束；`table.grow` 受 `XiHan:Plugins:Wasm:MaxTableElements`（缺省 100000）约束；`xihan.log` 一条最多 16384 字节；stdout / stderr 不再写进工作目录的文件，日志走 `xihan.log`
- **修复** CloudEvents 入站端点的请求体与 batch 条数有了上限：原先整读请求体、batch 不限条数，派发模式下每个事件都走一次完整管道。现在请求体最多 `XiHan:Protocols:CloudEvents:MaxRequestBodyBytes`（缺省 1 MiB，超过为 `PROTOCOLS_VALIDATION_013`），batch 最多 `MaxBatchSize` 个事件（缺省 100，超过为 `PROTOCOLS_VALIDATION_014`）；`CloudEventsHttpBinding.ReadAsync` 多了 `options` 参数
- **修复** CloudEvents Webhook 发布方不再读接收方的响应体：原先先把整个响应缓冲进内存（上限约 2 GiB）再看状态码，接收方返回超大正文就能耗尽宿主内存；现在读到响应头即判定
- **修复** gRPC 解码嵌套消息最多 64 层：原先递归的消息描述能嵌套到第 1000 层，写 JSON 时以非预期异常失败，调用方得到 `Internal`；现在超过即 `InvalidArgument` + `PROTOCOLS_VALIDATION_005`
- **修复** 知识库检索的条数与文档过滤有了上限：`Top` 最多 1000（`KnowledgeSearch.MaxTop`），`RetrievalFilter.DocumentIds` 最多 1000 个（`MaxDocumentIds`），超过在构造时抛出；原先条数不限、带元数据过滤时乘以超取倍数会溢出，向量存储把文档 ID 逐个左接成过滤表达式，现在拼成平衡的或树
- **修复** 审计不再把被 Tenant 阶段拒绝的租户记成事件的租户：原先安全上下文没有租户就回退到调用方自报的 Baggage，失败的调用落进不存在或已禁用的租户的审计；现在装配了多租户时只取安全上下文，自报的租户在结果的错误参数里。内核新增 `MarkTenantResolved()` / `IsTenantResolved()`
- **修复** 配了 `XiHan:MultiTenancy:DefaultTenant` 或 `RequireTenant` 的宿主里，工作流轮询平台分区不再失败：原先平台分区的调用被补上缺省租户（存储发现分区不符即抛出）或因不带租户被拒绝。内核新增 `MarkPlatformCall()` / `IsPlatformCall()`，Tenant 阶段对声明的平台调用不补缺省租户、不要求带租户
- **修复** 租户分片的解析缓存过了 `XiHan:MultiTenancy:ShardCacheTimeToLiveSeconds`（缺省 30 秒）即重新读租户存储：原先从不过期，多实例部署里别的实例改了租户的连接串，本实例一直往旧库写。`TenantStoreShardResolver` 的构造多了 `TimeProvider`
- **修复** 数据库授予的撤销在提交之后立即生效：原先只在写入时清缓存，撤销提交之前的判定会把已撤销的授予写回缓存，保留到存活时间结束（`CacheTimeToLiveSeconds` 为 0 时永久保留）；现在作用域结束时再失效一次，缓存丢弃失效之前开始的读取
- **修复** 数据库租户目录的停用与改连接串在提交之后立即生效：原先只在写入时清目录与分片缓存，提交之前的解析会把旧租户写回缓存，停用的租户在存活时间内仍被当作启用、仍指向旧库
- **修复** 插件回调与发布只能带宿主交给插件的租户：原先回调元数据里的 `xihan.tenant` 原样采用、Wasm 中间件也能改写它，插件能在任何启用的租户里调用宿主操作、发布消息。越界的回调与信封为 `PLUGINS_DENIED_002`，中间件改写租户被丢弃；原样转发收到的元数据的插件不受影响
- **修复** 租户 ID 只用小写（`^[a-z0-9][a-z0-9._-]{0,63}$`，整串匹配）：原先区分大小写，数据库缺省排序规则下 `ACME` 的调用能读写 `acme` 的行；`acme\n` 这类结尾带换行的 ID 也曾被放过。含大写的 ID 为 `MULTITENANCY_VALIDATION_001`。升级前把租户目录、`TenantId` 列、授予与资源名里的租户 ID 改成小写，只有大小写不同的两个租户先拆开数据
- **修复** 没有租户的授予只对平台调用生效：原先它对每个租户生效，平台授给 `role:admin` 的权限落到每个租户里带同名角色、同一用户 ID 的人身上。要对每个租户生效的授予把范围写成 `*`（`PermissionGrant.AnyTenant`，数据库租户列存 `*`，内存配置写在 `XiHan:Security:InMemory:AnyTenantGrants`）；`GrantTenantFilter.Global` 改名 `Platform`，`PermissionGrantRecord.GlobalTenantId` 改名 `PlatformTenantId`
- **修复** 多租户实体的唯一索引必须含 `TenantId`：原先不要求，撞上不含租户的唯一索引会告诉调用方别的租户已有这个键。不含又没写 `AcrossTenants = true` 的声明在启动时失败；主键仍全局唯一，多租户实体的标识应由服务端生成
- **修复** MCP 的 HTTP 端点校验 `Origin`、`Content-Type` 与请求体大小：原先任意网页都能用 `text/plain` 的简单请求调用本机 MCP 服务的匿名 tool，请求体也只受 Kestrel 的 30 MB 限制。带 `Origin` 的请求要在 `XiHan:Protocols:Mcp:AllowedOrigins` 里（否则 403），只收 `application/json`（否则 415），请求体最多 `MaxRequestBodyBytes`（缺省 4 MiB，否则 413）；浏览器里的 MCP 客户端要把它的源配进去
- **修复** CloudEvents 入站缺省只收 `XiHan:Protocols:CloudEvents:Topics` 映射过的事件类型：原先没有映射的类型以它本身为主题，只被允许投递某一种事件的发送方能投进任何有订阅的主题，包括插件订阅的主题。依赖类型直接当主题的应用把类型映射进 `Topics`，或在发送方可信时打开 `AcceptUnmappedTypes`
- **修复** Kubernetes 租约客户端校验 API Server 证书的主机名与服务端认证用途：原先只核对证书链通到集群 CA，持有集群 CA 签发的任意证书就能冒充 API Server、拿到 ServiceAccount 令牌并伪造租约
- **修复** 宿主调用进程外插件时每次都带会话令牌，回调套接字放进私有目录：原先只有回调带令牌，本机其他用户或同一 Pod 的边车直接连插件端点就能冒充宿主，宿主的回调套接字也直接放在共享的临时目录下。插件必须校验 `xihan-plugin-session`，不符即 `Unauthenticated`；自己实现协议的插件要补上这道校验（.NET 写法见 `samples/EchoPlugin/SessionGuard.cs`）
- **修复** 极远的截止时间不再让调用以 500 结束：原先 `grpc-timeout` 的大数值溢出时间范围，超过约 49.7 天的截止时间让定时器构造抛出，匿名调用方能随意制造错误日志；现在前者截到最远的时刻，后者不起定时器
- **修复** Baggage 的值在建调用上下文时核对：原先原样进上下文，调用方能把换行写进关联 ID 伪造日志行，超长的值进日志、审计与信封。为空、超过 256 个字符或含控制字符的值丢弃（幂等键除外，照旧由 Idempotency 阶段拒绝）；HTTP 入站不合文法的 `xihan.locale` 当作没给，`Accept-Language` 照常协商
- **修复** 幂等的作用域键里主体带长度前缀：原先主体 ID 直接拼接，主体 `a:b` 配键 `c` 与主体 `a` 配键 `b:c` 共用一条记录，能回放别人的结果；升级前写下的幂等记录不再命中（保留期内的重试会再执行一次）。MCP 的 HTTP 传输把幂等键交给 `tools/call`：原先外层请求先写下「处理中」，同一个键的工具调用一次也执行不了
- **修复** 出站地址策略在开了私网时同样拒绝 GCP（`fd20:ce::254`）与 Oracle（`fd00:c1::a9fe:a9fe`）的 IPv6 元数据地址：原先只拒绝 AWS 的，另两个能连到并取得实例凭据
- **修复** 插件工作目录缺省按用户分开（`<临时目录>/xihan-plugins-<用户名>`、`xihan-plugins-wasm-<用户名>`），Unix 上必须是本用户私有的真实目录：原先是临时目录下名字固定的目录，别的用户能抢先建成指向别处的链接。插件留在旧缺省目录里的数据不会自动搬过去，要保留就把 `WorkDirectory` 配回旧位置
- **修复** 进程外插件不再按 `GO` 前缀继承宿主的环境变量：原先 `GOOGLE_APPLICATION_CREDENTIALS` 等 Google 云凭据随之交给了插件；现在 Go 只继承 `GOMAXPROCS`、`GOGC`、`GOMEMLIMIT`、`GODEBUG`、`GOTRACEBACK`
- **调整** 健康探针只给 `status` 与 `state`：原先匿名访问者能读到全部特性、健康条目与它们的描述和数据。详情改由新的 `MapXiHanHealthDetails()`（`/health/details`）给出，它经内核管道、能加访问要求。MCP 的 `serverInfo.version` 缺省改为应用的版本，不再是框架版本
- **修复** 知识库的默认集合按租户分开：原先请求不指明集合时所有租户共用 `knowledge`，一个租户摄入的内容别的租户检索得到。带租户的调用现在用 `knowledge.<租户 ID>`，要共享就显式指明集合；`IKnowledgeIngestor` 与 `IKnowledgeRetriever` 改为作用域服务，在根容器里直接取用的代码要先建作用域；已经摄入在共享集合里的内容要重新按租户摄入
- **修复** 发件箱投递循环覆盖租户分片库：原先只轮询默认库，EF Core 在带分片的调用里写进租户库的消息发不出去。每轮先投递默认库，再逐个投递分片库；新增 `XiHan:Messaging:Outbox:IncludeShards`（默认 `true`）与 `ShardRefreshIntervalSeconds`（默认 60）。EF Core 下带分片的调用要发消息，分片库要建发件箱表
- **修复** 文件系统对象存储不再把键里含 `.xihan-tmp-` 的对象当作临时文件：原先这样的对象列举不到、重启时被删除，根路径含这段文字时整个存储重启后清空。临时文件片段改为 `~xihan-tmp-`，只按文件名判定
- **修复** 文件系统对象存储按首个点之前判定 Windows 设备名并补上 `com0`、`lpt0`，`RootPath` 去掉结尾的分隔符：原先 `con.tar.gz` 这样的键段在 Windows 上写不进去，`RootPath` 以分隔符结尾时每个操作都抛异常。Linux 与 macOS 上已有的这类键（如 `nul.tar.gz`、`com0`）换了文件名，升级后要重新写入，或把文件与元数据文件的首字符改成 `%XX`
- **调整** 对象的内容类型按媒体类型文法（最长 255 个字符），自定义元数据值不含控制字符、名与值合计不超过 2048 个 UTF-8 字节：原先换行能进 S3 的请求头，元数据不限大小。不合法时 `BlobPutOptions` 抛出 `ArgumentException`；把客户端给的内容类型交给它之前先用 `BlobPutOptions.IsValidContentType` 判断。宿主直传端点对不合法的请求内容类型返回 `OBJECTSTORAGE_VALIDATION_006`（HTTP 400）
- **修复** 雪花机器位租约改为原子续约：原先续约是释放再获取，释放的那一刻别的实例能拿到同一机器位，两边会发出相同的 ID。`ICacheStore` 新增 `TryRefreshAsync`（比较后重设到期），`LockHandle` 新增 `TryExtendAsync`；自己实现 `ICacheStore` 或派生 `LockHandle` 的要补上这两个成员。远程缓存插件的操作集新增 `cache-entries.try-refresh`，插件要实现它
- **修复** WebAuthn 认证在验签之后才判定凭据属于谁，RSA 公钥的模长不超过 4096 位：原先没有私钥的人能从失败原因分辨凭据属于哪个用户，几万位的模数让每次验签都很慢。文档不再建议拿 `Aaguid` 做型号白名单（`none` 证明下它是认证器自报的）
- **调整** 没有装配授权能力时，契约声明了权限或策略的操作、挂了 `RequireXiHan*` 的端点让启动失败（`KERNEL_VALIDATION_006`）：原先要求静默失效，操作与端点等于敞开。之后登记的这类插件操作登记失败，启动之后才写入的 `RequireAuthorization` 抛出 `InvalidOperationException`。迁移：装配 `Security.InMemory` 或 `Security.Database`，或去掉这些要求
- **调整** 已认证主体带多个值不同的租户声明时拒绝调用（`MULTITENANCY_VALIDATION_004`）：原先取第一个，签发方给出矛盾的身份时调用落进其中任意一个租户
- **调整** 预签名限定的内容类型须合内容类型文法，保存的文件名不含控制字符、最长 255 个字符：原先只要求不空白，带换行的值能在宿主签名的规范串里把一项挪成另一项
- **调整** Nacos 的令牌改走 `Authorization: Bearer` 请求头、不进查询串，失败原因不再带响应正文；配置了用户名密码时非本机的 `http` 地址默认拒绝，链路另有保护时设 `XiHan:Configuration:Nacos:AllowInsecureHttp` 为 `true`
- **修复** `xihan migrate` 与 `xihan setup` 遍历时不再跟随符号链接与目录联接：原先链接指向 `--path` 之外时迁移会改写别处的文件，链接成环时遍历到路径过长才失败
- **调整** `xihan pack` 发现进 ConfigMap 的配置文件里有带值的凭据键时失败（`CLI_VALIDATION_007`），只列键的路径、不带值：原先配置文件原样进不加密的 ConfigMap。凭据改放绑定的 Secret 或环境变量，或在这些文件里留空
- **修复** 客户端建在租户分片上时，工作单元的租户必须是这个分片的租户，不一致即抛出 `PERSISTENCE_VALIDATION_010`：原先不核对，管道外以别的租户开工作单元会把那个租户的行写进这个分片库
- **修复** EF Core 的多租户实体加全局查询过滤器 `xihan:tenant`：原先只有仓储按租户过滤，直接用工作单元的 `Context` 或注入的 DbContext 查询能读到别的租户的行。有意跨租户时用 `IgnoreQueryFilters([ModelBuilderExtensions.TenantQueryFilter])`；多租户实体上已有匿名过滤器的要给它起名
- **调整** 删除租户留墓碑：删除的 ID 读不到，也不能再新建（`MULTITENANCY_CONFLICT_001`）。原先删了再建同一个 ID，新租户会接手旧租户留下的数据。`TenantRecord` 改为软删除，已有的表要加 `IsDeleted`、`DeletedAt` 两列；内存实现的墓碑计入 `MaxTenants`

### 宿主、协议与插件

- **新增** 插件协议的 `HandleEventRequest` 带这次投递的订阅方式 `mode`，宿主总是填写，插件只交给以这种方式订阅的处理器。原先同一主题的不同类型各用一种方式时，插件分不出投递来自哪种订阅。自己从 `plugin.proto` 生成桩的插件要重新生成，并在 `HandleEvent` 里按 `mode` 分派

## v5.0.0-preview.3（2026-11-12）

::: warning 预览须知
- 这是预览版，安装要带 `--prerelease`；公开面多数标为 `Preview` 或 `Experimental`，正式版前仍可能调整
- 这一版集中调整了正式版前的公共 API，多数条目要改调用代码，升级前逐条看下面的迁移写法
:::

### 版本与兼容

- **调整** 稳定性级别的承诺：`Stable` 大版本内不破坏；`Preview` 与 `Experimental` 可能在小版本变更，补丁版本不变，每次变更在本页写明迁移方式；要求升级不改代码就只用 `Stable` 的 API（见 [稳定性分级](/getting-started/concepts#稳定性分级)）
- **调整** 5.0 的级别定准：配置、对象存储、审计的抽象，七个默认实现与 Redis 缓存、RabbitMQ、SqlSugar、OpenTelemetry，调度的应用面（`IJob`、`IJobScheduler`、作业定义、计划与日历）、工作流的定义模型与 `WebApplicationBuilder.AddXiHan` 升为 `Stable`；三个 Profile、发件箱与全部一致性套件降为 `Preview`；`HttpActivity`、`WorkflowHttpClient` 与 SqlSugar 的读写分离、差异日志降为 `Experimental`。各包页的「稳定性」一行写明例外

### 原语与内核

- **新增** 宿主启动时向标准输出打印启动 Banner（版式同 4.x），`XiHan:Kernel:Banner:Enabled=false` 关闭，以 JSON 行收集 stdout 的部署应关掉；命令行宿主缺省不打印（见[装配入口](/startup/bootstrap#启动-banner)）
- **移除** `CapabilityAttribute`：没有使用者，运行期也不读，随清单生成器一起提供；`CapabilityResolver` 与 `NullAuditSink` 不再公开，能力求解经 `AddXiHan` 装配，结果从 `ResolvedGraph` 读取
- **调整** `OperationContract` 构造只收 ID，其余字段改为初始化器：手写的 `new OperationContract(id, stability, expose, policy, permission, readOnly)` 改成 `new OperationContract(id) { Stability = …, Expose = …, Policy = …, Permission = …, IsReadOnly = … }`；用生成器的项目重新编译即可，生成的 `GeneratedOperations` 变为 internal
- **调整** `Expose.All` 冻结为 5.0 的五个协议：以后新增的协议不并入 `All`，要在新协议上发布须显式开启；现有行为不变
- **调整** 清单记录的可选字段改为初始化器：`new Binding(name, type) { Required = false }`、`new CapabilityRequirement(id, version) { Optional = true }`、`new ConfigContribution(section) { Schema = … }`、`new PipelineContribution(middleware, stage) { Before = …, After = … }`（`PipelineContribution` 不再是位置记录）
- **调整** `Error.Unexpected` 的参数顺序改为 `(code, messageKey, details, inner)`，与构造函数一致；只传内部错误的写成 `Error.Unexpected(code, messageKey, null, inner)`
- **调整** `Stable` 的公开方法与构造不再带可选参数，改为重载（`Error` 的构造与工厂、`PipelineBuilder.UseAt` / `UseInstance` / `UseNamedInstance`、`FeatureContext.AddMiddleware`、`ProblemResponse.FromError`、`BlobUrlRequest.ForDownload` / `ForUpload` 等）：用命名实参跳过前面参数的调用补上被跳过的参数，如 `UseAt<T>(stage, Placement.End, owner)`、`ForDownload(null, fileName)`
- **调整** `SchemaViolation` 的消息键与参数改为初始化器：`new SchemaViolation(path, reason, key, args)` 改成 `new SchemaViolation(path, reason) { MessageKey = key, Arguments = args }`
- **新增** 分析器规则 XH0006～XH0008：`Stable` 的签名不得引用 `Preview` / `Experimental` 类型，成员级别不得高于所在类型，`Stable` 不得声明可选参数
- **修复** `CapabilityDeclaration.TryParse` 与 `CapabilityRequirement.TryParse` 的输出标注为可空（`[NotNullWhen(true)]`），失败时为 `null`
- **修复** `InvocationContext.Baggage` 构造时复制成只读副本，调用方之后改原字典不再影响上下文；`Items` 在派生上下文之间共享，并行写入时不再损坏
- **调整** 匿名主体的 `Principal.Id` 由空字符串改为 `null`，`new Principal(id, scheme)` 只创建已认证主体（两者都不得为空），匿名用 `Principal.Anonymous`；判断 `IsAuthenticated` 之后可直接用 `Id`。`ResourceName.Tenant` 的全局资源由空字符串改为 `null`，构造时校验每一段
- **调整** 内核的数据类型不再是位置记录：`AuditEvent`、`HealthSnapshot`、`OperationResponse`、`FeatureStatus` 按属性名写初始化器（`new AuditEvent { Timestamp = …, Action = …, Outcome = …, TraceId = … }`）；`AuthorizationRequest`、`ContributorStatus`、`PluginSource` 只保留两参构造，第三个实参改成初始化器（`new ContributorStatus(name, state) { Error = … }`）；这些类型都不再能解构
- **调整** 返回 `Maybe` 的查找统一叫 `Find…`：`GetOutcome` / `GetOrigin` / `GetOperation…` / `GetCredential` / `GetAuthorizationRequirement` / `GetTenantShard` / `GetAuditAction` / `GetAuditResource` / `GetDelivery` / `GetRun` / `GetXiHanContext` 与按键读条目的 `Get<T>` 改成对应的 `Find…`，`TryGetBinding` 改成 `FindBinding`，插件宿主的 `Describe` 改成返回 `Maybe` 的 `FindStatus`；`GetAuditDetails` 改成 `FindAuditDetails` 并返回只读副本；`WorkflowVariables` 删掉 `Get` / `Get<T>`，改用索引器与 `TryGet<T>`
- **调整** `App.CreateContext` 的可选参数收进新的 `InvocationContextOptions`：`App.CreateContext(sp, ct, deadline: d, baggage: b)` 改成 `App.CreateContext(sp, new InvocationContextOptions { Deadline = d, Baggage = b }, ct)`，只传服务与令牌的不用改；`DomainEventBuffer` 改为无参与 `(int capacity)` 两个构造，`Items` 交出只读视图

### 子系统

- **新增** `Security.WebAuthn`：通行密钥的依赖方实现，提供 `security.webauthn@1`。`WebAuthnCeremonies` 生成与 WebAuthn Level 3 JSON 同形的注册与认证选项，按 RFC-0008 的顺序核对响应（ES256 / PS256 / RS256，证明一律按 `none`）；无状态，挑战与凭据由应用保存。配置节 `XiHan:Security:WebAuthn`，`RelyingPartyId` 与 `Origins` 必填。新错误码 `SECURITY_VALIDATION_006` / `007`、`SECURITY_DENIED_005` / `006`（签名计数没有增加）（ADR-0170）
- **新增** `Testing` 的 `WebAuthnTestAuthenticator` 与 `WebAuthnTestCeremony`：测试里生成合法或篡改过的通行密钥响应；`Testing` 随之依赖 `Security.WebAuthn` 与 `System.Formats.Cbor`
- **新增** `Messaging.Redis`：用 Redis Streams 提供 `messaging.queue@1`，只有 Redis 的部署也能跨进程收发消息；需显式 `UseFeature<RedisMessagingFeature>()`，配置节 `XiHan:Messaging:Redis`，可选绑定 `queue`（类型 `cache:distributed`），需要 Redis 6.2+，不提供 `messaging.stream@1`（见 [Messaging.Redis](/packages/messaging-redis)）
- **修复** 工作流启动请求不再公开 `InstanceId`、`ParentInstanceId`、`ParentNodeInstanceId` 与 `Depth`：调用方原先能借它们伪造子流程的父链接、绕过嵌套深度上限；这几个字段只由引擎为子流程设置，给它们赋值的代码删掉即可，要幂等启动用 `CorrelationId` 与 `ReusePolicy`
- **修复** 活动、结果处理器与指派规则拿到的 `Instance`、`NodeInstance` 改为只读视图 `WorkflowInstanceView` / `WorkflowNodeInstanceView`：原先交出的是引擎随后写回的对象，扩展代码改了状态或租户会被持久化；只读的代码不用改，要读变量与私有状态改用上下文的 `Variables` 与 `State`
- **修复** 用户任务与外部任务的书签不能再经 `IWorkflowEngine.ResumeAsync` 直接消费：原先绕过指派人与持锁工作者的校验、也不发完成事件，现在返回 `WORKFLOW_CONFLICT_003`；办理任务改用 `IWorkflowTaskService.CompleteAsync`，外部任务改用 `IWorkflowExternalTaskService.CompleteAsync` / `FailAsync`
- **调整** 工作流的引擎、定义目录、任务与外部任务服务的实现类型，`ActivityRegistry.Add` 与 `GetActivityRegistry`，实例的 `JoinStates` 与 `WorkflowJoinState`，以及 `Workflow.Database` 的实例锁与轮询类型不再公开：经 `IWorkflowEngine` 等接口与 `AddActivity<TActivity>` 使用；`WorkflowEngine.LastErrorVariable` 等常量移到 `WorkflowReservedNames`，成员名不变，只改类名
- **调整** 调度与配置的实现细节不再公开：`JobPlanner`、`InMemorySchedulingHealthContributor`、`NacosDocumentParser`，以及 `KubernetesSchedulingOptions` 的 `ResolveNamespace` / `ResolveHolder` / `ResolveApiServer` / `LeaseNameFor`；它们只在各自的包里用
- **调整** `JobRegistry`、`JobTypeRegistry`、`JobCalendars` 的 `Add` 不再公开：装配之后只读，登记作业、作业类型与日历用 `AddJob`、`AddJobType`、`AddJobCalendar`
- **调整** 持久化、本地化、缓存与消息的实现类型不再公开：三个 Provider 的仓储实现、`InMemoryUnitOfWork`、`InMemoryTable` 与 `InMemoryStore.Table`、`LeasedWorkerIdSource`、`RepositoryRegistrations.Items`、`MessageCatalog.ParseLocaleFile`、`CacheStoreDistributedCache.Translate`、领域事件的登记表、映射与派发器、三个消息代理的连接与探测成员、`OpenIdConnectMetadataCache.FailureRetryDelay`；照常经 `IRepository`、`IUnitOfWork`、`AddDomainEvent<TEvent>` 等入口使用
- **调整** `IPermissionGrantStore` 改为批量读取 `GetManyAsync(subjects, tenantId)`，权限判定把用户与全部角色一次读出；实现授予存储的 Provider 把 `GetGrantsAsync` 换成 `GetManyAsync`，单主体的 `GetGrantsAsync` 仍可调用（扩展方法）
- **新增** 授予管理的可选接口 `IPermissionGrantAdministration`，内存与数据库实现都提供：`GrantAsync` / `RevokeAsync` 与入参同序的逐项结果（撤销不存在是 `SECURITY_NOTFOUND_002`），`ListAsync(PermissionGrantQuery)` 按主体与租户（`GrantTenantFilter.Any` / `Global` / `Of(tenantId)`）过滤、分页并计数，越界为新错误码 `SECURITY_VALIDATION_005`；`InMemoryPermissionGrantStore.Grant` / `Revoke` 不再公开，`PermissionGrantRecordStore.ListAsync(subject, tenantId, skip, take)` 删除，改用接口
- **调整** 审计查询新增 `AuditQuery.Resource`（`xrn:` 文本精确匹配），数据库实现加 `(Resource, Sequence)` 索引；审计细节统一为 JSON 值：`AddAuditDetail(key, JsonNode?)`（简单值写法不变），`AuditEvent.Details` 改为 `IReadOnlyDictionary<string, JsonElement>?`，内存与数据库读回同一类型，读细节的代码改用 `GetDecimal()` / `GetString()` 等
- **调整** `AuditQuery` 的 `PrincipalId` / `TenantId` 改为显式过滤 `Principal`（`AuditPrincipalFilter`）与 `Tenant`（`AuditTenantFilter`），能只查匿名调用或平台级事件：`new AuditQuery { TenantId = id }` 改成 `new AuditQuery { Tenant = AuditTenantFilter.Of(id) }`，主体同理
- **调整** `ITenantStore.FindAsync` 返回 `Result<Maybe<TenantInfo>>`，存储不可达是 `MULTITENANCY_UNAVAILABLE_003`（Tenant 阶段以 503 结束，不再当作 500 抛出）；`ListAsync(TenantQuery)` 改为分页，逐页读到 `ContinuationToken` 为空；自己实现租户存储的 Provider 改这两个方法
- **新增** 租户管理的可选接口 `ITenantAdministration`（`UpsertAsync` / `RemoveAsync` 与入参同序的逐项结果，写后失效目录与分片缓存），内存与数据库实现都提供；`InMemoryTenantStore.Set` / `Remove` 不再公开（内存实现写入后也会失效分片缓存了），`TenantRecordStore.ListAsync(skip, take)` 删除，改用 `ITenantStore.ListAsync`
- **调整** 缓存失效统一为 `Invalidate(ids)` 加 `InvalidateAll()`，不再以 `null` 表示全部：`ITenantShardResolver`、`IAIProviderCatalog`、`DatabaseTenantStore`、`DatabasePermissionGrantStore`；`Invalidate("acme")` 改为 `Invalidate(["acme"])`，无参调用改为 `InvalidateAll()`
- **调整** `ITenantResolver.ResolveAsync` 返回 `Result<Maybe<string>>`，自定义解析器可以返回失败拒绝调用（以该错误结束，后面的解析器与默认租户不再顶替）；实现解析器的应用把返回值包进 `Result.Success(…)`
- **调整** `IMessageLocalizer` 只留 `Options`、`Find`、`Resolve(key, locale, arguments)`，其余 `Localize` / `Resolve(error, …)` 改为 `MessageLocalizerExtensions` 的扩展方法：调用方不用改，自己实现本地化器的 Provider 删掉这 5 个成员
- **调整** `IAuditSink.WriteAsync` 的契约写明不抛出：接收不下或落地失败时由实现自己丢弃、计数并告警，返回只表示已接收；自己实现接收器的 Provider 需通过一致性套件新增的用例
- **移除** `XiHan:MultiTenancy:Database:MaxListedTenants`：租户列表分页之后不再截断，删掉这个配置键
- **调整** `MessageDelivery` 改为初始化器（`new MessageDelivery { Topic = …, Envelope = …, Attempt = …, Mode = … }`），`MessageSubscription` 只保留 `(topic, mode)` 构造；`IdempotencyRecord` 不再公开，记录格式与回放不变
- **调整** 调度器的状态与历史改为只读视图：`RunsAsync` 返回 `JobRunView`（失败结果在 `Outcome`），`JobStatus.Job` 是 `JobView`；`StatusesAsync()` 改为分页的 `ListStatusesAsync(new JobStatusQuery())`，按名批量用 `StatusesAsync(names)`，`StatusAsync(name)` 不变；`new JobTriggerRequest(name, data)` 改为 `new JobTriggerRequest(name) { Data = data }`，`JobRun`、`JobOutcome`、`JobTriggerStatus`、`JobStatus` 按属性名初始化
- **调整** 调度器的批量方法中途取消不再抛出：已开始的项照常给出结果，未开始的项以 `KERNEL_CANCELLED_001` 结束；开始前已取消时仍抛出
- **移除** `IWorkflowTaskService.PendingAsync` 与 `PendingByInstanceAsync`：待办一律分页读取，改为 `QueryAsync(new UserTaskQuery { AssigneeId = … })` 或 `{ InstanceId = … }`
- **调整** 工作流的批量方法传空集合时直接返回空结果，不再开一次调用
- **调整** `IBlobStoreProvider.CreateAsync` 与 `IDataSourceProvider.CreateAsync` 以结果报告失败（`Result<IBlobStore>` / `Result<IDataSource>`），不再抛出：自己写存储或数据源 Provider 的改返回类型，拒绝定义用 `BlobStoreFailures` / `DataSourceFailures`；经注册表使用的应用不受影响
- **调整** 工作流的任务与控制请求改为初始化器：`new UserTaskCompletion(taskId, actorId) { Outcome = …, Comment = … }`，外部任务请求保留 `(taskId, workerId)`，`new WorkflowJump(instanceId, targetNodeId) { Reason = … }`、`new WorkflowVariableUpdate(instanceId) { Variables = …, Remove = … }`；`UserTaskPage`、`BlobMetadata`、`BlobPage`、`EntityColumnMapping`、`SqlSugarDiffLogData` 按属性名初始化，这些类型都不再能解构
- **调整** 待办查询的 `UserTaskQuery.Descending` 改为排序项列表 `Sorts`（`UserTaskSort` / `UserTaskSortField`，与定义、实例查询同一写法）：`Descending = true` 改成 `Sorts = [new(UserTaskSortField.CreatedAt, Descending: true)]`；状态序号 `WorkflowStatusOrder.Of` 移到 `Workflow.Abstractions` 公开一份，存储实现共用
- **调整** 字符串承载的标识（`CacheKey`、`BlobKey`、`ContainerName`、`JobName`、`WorkflowName`、`ActivityType`、`SecretName`）不再从 `string` 隐式转换：传字符串的地方写 `new JobName("cleanup")` 或 `(JobName)"cleanup"`；这些类型与 `GrantSubject`、`TriggerKey` 的 `default` 值读文本抛 `InvalidOperationException`，`PermissionGrant` 与 `TriggerKey` 拒收 `default` 成员
- **调整** 分页统一成两种形状：偏移分页 `Skip` / `Take`（`MaxTake`，页为 `Items` 与 `long Total`），续页分页 `PageSize` / `ContinuationToken`（页为 `Items` 与 `ContinuationToken`）；调度运行历史与工作流的定义、实例、待办查询的 `Limit` / `MaxLimit` 改为 `Take` / `MaxTake`，`CacheKeyQuery` 的 `Limit` / `Continuation` 改为 `PageSize` / `ContinuationToken`，`CacheKeyPage.Keys` 与 `AuditPage.Events` 改为 `Items`，分页错误的参数 `limit` 改为 `take` / `pageSize`
- **调整** 批量的逐项结果统一为与入参同序的 `Result` 列表：`BlobPutResult`、`BlobDeleteResult`、`JobTriggerResult`、`TenantRecordResult`、`MessageTranslationResult`、`PermissionGrantResult` 删除，读 `result.Outcome` 的地方直接用列表元素，要键时按下标取入参；实现 `IBlobStore` 的 Provider 改 `PutAsync` / `DeleteAsync` 的返回类型
- **修复** 分片上传句柄 `BlobMultipartUpload` 的覆盖标志改为必填的初始化成员：原先重建句柄时漏传参数会把「不覆盖」静默变成「覆盖」（S3 完成时按句柄决定条件写入），写成 `new BlobMultipartUpload(container, key, uploadId) { Overwrite = … }`；`FileSystemBlobStore` 只经 `CreateAsync` 创建（扫描已用量后才交出），`BlobStoreConformance.CreateStore()` 改为 `CreateStoreAsync()`
- **修复** 文件系统对象存储在 Linux 与 macOS 上并发的不覆盖写入（含分片上传的完成）偶发两个都成功、后写的覆盖先写的：改为先独占创建占位文件再替换内容；Windows 上仍用不替换的改名
- **调整** `SnowflakeId` 不再能逐项构造，只经 `SnowflakeId.Parse(value, epoch)` 得到（值与分量总是一致）；`SqlSugarPersistenceOptions.CommandTimeoutSeconds` 改为 `TimeSpan CommandTimeout`（缺省 `StandardCommandTimeout`），配置键 `CommandTimeoutSeconds` 不变
- **调整** `Caching.Hybrid.HybridCache` 改名 `HybridObjectCache`（与 BCL 的 `HybridCache` 不再同名），`Localization.InMemory.MessageLocalizer` 改名 `InMemoryMessageLocalizer`；Redis 缓存、Redis 消息与 Nacos 选项的用户名统一为 `UserName`，Kafka 的 `GroupId` 与 Redis 消息的 `GroupName` 统一为 `ConsumerGroup`，配置键改为 `XiHan:Messaging:Kafka:ConsumerGroup`、`XiHan:Messaging:Redis:ConsumerGroup`、`XiHan:Configuration:Nacos:UserName`（值不变，位点照常接续）
- **调整** `MessageSubscriptions` 在消息运行时启动后只读：再 `Add` 抛 `InvalidOperationException`，`All` 与 `HandlersFor` 交出只读视图；订阅照旧在 `Configure` 里用 `AddMessageHandler` 登记
- **调整** 缓存键管理的 `RemoveByPrefixAsync` 返回 `Result<long>`，空前缀或不合法的前缀返回新错误码 `CACHING_VALIDATION_004`（HTTP 400）而不是抛 `ArgumentException`；`CacheKeyQuery.Prefix` 不合法不再在构造时抛出，由 `ListKeysAsync` 以同一错误码返回；实现键管理的 Provider 在开头调 `CacheKeyQuery.ValidateRemovalPrefix`
- **调整** `LockHandle` 改为抽象基类：包外实现 `IDistributedLock` 时派生它并实现 `ReleaseCoreAsync`，基类保证并发释放也只执行一次；只经 `TryAcquireAsync` 取句柄的调用方不用改
- **调整** AI 护栏的 `InspectAsync` 改收 `GuardrailInspection`（`Direction` 与 `Messages`），`CompleteAsync` 拿到响应后再以输出方向检查一次；自写护栏改签名，只看输入的在 `Direction` 不是 `Input` 时放行。`IKnowledgeStore.SearchAsync` 改收 `KnowledgeSearch`（最低得分由存储施加），`TrimAsync` 改收 `KnowledgeTrim` 列表；自写存储按新签名实现
- **调整** `IJob.RunAsync(run, context)` 去掉与 `context.CancellationToken` 重复的令牌参数，实现里的 `cancellationToken` 改为 `context.CancellationToken`；`LeaderLease.ReleaseAsync(cancellationToken)` 收令牌并返回 `Result`（幂等，后端没删掉时返回那个失败），实现租约的 Provider 改签名
- **调整** `JobSchedule.Interval` 与 `At` 改为可空，不适用的计划为 `null`；`JobRunQuery.Trigger` 改名 `TriggerName`（与运行记录同名），新增按触发来源过滤的 `Triggers`；`IHostMonitor` 的七个方法改为 `Read…Async(cancellationToken)`，可能阻塞的读取不占调用方线程、可取消等待；`ConfigurationDocument` 与 `SecretsConfigurationOptions` 构造时复制调用方的字典，只差大小写的重复键抛 `ArgumentException`
- **调整** `UnitOfWorkOptions` 改为初始化器：`new UnitOfWorkOptions { TenantId = …, IsReadOnly = true, Connection = … }`；`IUnitOfWork.TenantId` / `IsReadOnly` 改为 `unitOfWork.Options.TenantId` / `Options.IsReadOnly`，自己实现工作单元的 Provider 实现 `Options`

### 宿主、协议与插件

- **调整** AI、协议与宿主的实现类型不再公开：AI 的对话、知识库摄入与检索、提示增强、Provider / 工具 / 提示目录的默认实现，`McpServer` 的构造与 `HandleAsync`、`McpReply`、`McpStdioCommand`，命令行内置的 `graph` / `pipeline` / `operations` / `invoke` 命令，`HttpRouteConvention.Derive` 的布尔重载、`OpenApiDocumentBuilder.Build` 的五参重载，`ProtoMessage` 的构造与 `S3ObjectStorageFeature.CreateClient`；经接口与特性使用的写法不变
- **调整** `McpTool`、`McpToolAnnotations`（提示缺省为假，只写为真的项）、`OutOfProcessPluginStatus`、`WasmPluginStatus`、`ParsedCommandLine` 改为初始化器；`new OpenApiServer(url) { Description = … }`、`new OpenApiDocumentSource(registry, options) { Permissions = … }`
- **调整** `MapXiHanOperations()` 返回 `IEndpointConventionBuilder` 而不是端点数，可以在返回值上给全部操作端点统一加 CORS、限流等约定
- **调整** `ProblemResponse`、`HealthResponse`、`FeatureHealth` 改为初始化器，按属性名赋值；响应的 JSON 不变
- **修复** `Plugins.OutOfProcess`、`Plugins.Wasm`、`Plugins.Conformance` 对 `Plugins.Protocol`（以及 Conformance 对两个宿主）改为精确版本依赖：原先单独升级其中一个包，还原能通过，运行时才缺成员；这几个包要用同一版本

### 包

- **优化** NuGet 包换用新图标

## v5.0.0-preview.2（2026-11-06）

::: warning 预览须知
- 这是预览版，安装要带 `--prerelease`；公开面多数标为 `Preview` 或 `Experimental`，正式版前仍可能调整
:::

### 宿主、协议与插件

- **修复** `invoke` 的字段选项按操作的请求 Schema 写成布尔、数字与数组，`--times 2` 进载荷是 `"times":2`；操作没有 Schema 时值仍是字符串（见[命令行](/protocols/command-line)）

### 包

- **优化** 全部包的描述与 README 重写，NuGet 包恢复图标并附更新日志链接

## v5.0.0-preview.1（2026-11-05）

::: warning 预览须知
- 这是预览版，安装要带 `--prerelease`；公开面多数标为 `Preview` 或 `Experimental`，正式版前仍可能调整
:::

### 原语与内核

- **新增** 原语层 `Primitives`：`Result` / `Result<T>`、`Error` 与错误码文法、`Maybe<T>`、`TypedId`、`InvocationContext`、清单模型、`PipelineStage`、`LifecycleState`、`Stability` 与 `[ApiLevel]`，只增不改
- **新增** 内核 `Kernel`：`AddXiHan` + `UseFeature<T>()` 装配特性，按能力 ID 求解能力图谱，`XiHan:Providers:<能力 ID>` 或 `UseProvider` 选择 Provider，启动日志打印图谱与管道
- **新增** 十一个阶段的执行管道与内置中间件：截止时间、按操作 ID 的熔断 / 限流 / 并发上限（`XiHan:Kernel:Resilience`）、按 JSON Schema 与 `IValidatableObject` 的契约校验
- **新增** 操作契约与调用器：`[Operation]`、`IOperation<TRequest, TResponse>`、`OperationRegistry`、`IOperationInvoker`；`Kernel.Generators` 生成登记代码与请求 / 响应 JSON Schema（`XHG001`–`XHG006`），程序集级 `OperationDefaults` 与只读声明
- **新增** 九态生命周期、`/health/startup|ready|live` 探针、`IHealthContributor` 与周期探测的 `PeriodicHealthContributor`，健康条目带说明、检查时刻、耗时与标量数据
- **新增** 资源绑定（`XiHan:Bindings`、`SERVICE_BINDING_ROOT`）与配置目录 `XIHAN_CONFIG_PATH` 热更新
- **新增** `IInvocationAccessor` 与调用来源 `InvocationOrigin`（协议、请求目标、远端地址、User-Agent、端点路由与名字）
- **新增** 分析器 `Kernel.Analyzers`（XH0001–XH0005）与测试基座 `Testing`
- **新增** `OutboundAddressPolicy.ConnectAsync(host, port, ct)`：不是 HTTP 但地址来自用户的连接（SMTP）用同样的规则，返回连好的 `Socket`
- **修复** `MapXiHanBlobs` 的响应带 `X-Content-Type-Options: nosniff`，写出内容时除 PDF 外带沙箱的 `Content-Security-Policy`，用户上传的 HTML、SVG 不能在应用的源上运行脚本
- **修复** 载荷解码错误的 `reason` 只写 JSON 路径，不再把请求类型的全名回给调用方
- **修复** SqlSugar 的数据库健康项与运行时数据源探测走一次数据库往返并按配置超时：连接池里有空闲连接时不再在库宕了之后报健康，服务器不回话时不再卡住启动；`persistence.sqlsugar.database` 的错误码由 `PERSISTENCE_UNAVAILABLE_003` 变成 `KERNEL_UNAVAILABLE_005`；`TestAsync` 单次上限是运行时数据源的 `HealthProbeIntervalSeconds`
- **修复** 配了从库时授予与租户目录读主库，撤销授予、停用租户的生效时间不再多出复制延迟；新增 `UnitOfWorkOptions.ReadsPrimary`
- **调整** HTTP、gRPC、MCP 入站丢弃 Baggage 里的 `xihan.tenant` 与 `xihan.principal`，这些调用方的租户只来自认证（CloudEvents 入站不变，端点权限由宿主自己加）；要按主机名或请求头选租户时登记自己的 `ITenantResolver`
- **新增** 出站地址策略 `OutboundAddressPolicy`：工作流 `Http` 活动、CloudEvents Webhook 与 S3 命名实例缺省只能连公网地址，连接时按实际 IP 判定；连本机或内网要在 `XiHan:Security:Outbound` 放行，本地开发写 `AllowLoopback: true`（见[出站地址策略](/security/outbound)）

### 子系统

- **新增** 本地化：消息键与 `key.L()`、每种语言一个 `Localization/<区域>.json`、内置 10 种区域（默认 `en-US`）、宿主按 `Accept-Language` 翻译 Problem Details；`Localization.InMemory` 与 `Localization.Database`
- **新增** 领域模型 `Domain`：实体协议接口、值对象、领域事件缓冲与规约
- **新增** 持久化：仓储、查询、工作单元与 Transaction 阶段、批量方法、雪花标识生成器；`Persistence.InMemory`、`Persistence.EntityFrameworkCore`、`Persistence.SqlSugar`（多库、分表、差异日志）与 `Persistence.Generators`
- **新增** SqlSugar 读写分离：只读工作单元读从库，其余读写走主库；从库权重归一化与周期探测，不可读的自动摘除、全部不可读时读回到主库，健康项 `persistence.sqlsugar.replicas`（见[持久化](/persistence/sqlsugar)）
- **新增** 缓存：字节存储、类型化缓存、分布式锁、Idempotency 阶段、`IDistributedCache` 适配、键管理；`Caching.InMemory`、`Caching.Redis`、`Caching.Hybrid`、`Caching.Remote`（Experimental）
- **新增** 多租户：租户解析与 Tenant 阶段、租户分片与管道外的 `TenantShardScope`；`MultiTenancy.InMemory`、`MultiTenancy.Database`
- **新增** 安全：认证处理器与 Authenticate 阶段、权限与策略、RBAC 授予、权限收窄过滤器、授予来源、条件引擎、IP 归属地；`Security.InMemory`、`Security.Database`、`Security.Jwt`、`Security.OpenIdConnect`、`Security.Ip2Region`
- **新增** 消息：CloudEvents 信封、发布与订阅（竞争消费与每个实例都收到的广播订阅）、经管道的派发、重试与死信、事务性发件箱、领域事件到消息的登记；`Messaging.InMemory`、`Messaging.RabbitMQ`、`Messaging.Kafka`（流与按时刻重放）
- **新增** 对象存储：批量写入与删除、按前缀列举、范围读取、分片上传、预签名、命名存储实例；`ObjectStorage.FileSystem`、`ObjectStorage.S3`
- **新增** 调度：作业与触发器、间隔 / cron / 一次 / 手动计划、日历、misfire、集群分发与失联恢复、领导选举、重试与执行时限；`Scheduling.InMemory`、`Scheduling.Database`、`Scheduling.Kubernetes`
- **新增** 远程配置与密钥：`Configuration.Abstractions` 的配置源（轮询重载、失败保留最后一份）与 `Configuration.Nacos`
- **新增** 审计：Observe 阶段审计中间件与查询契约；`Auditing.InMemory`、`Auditing.Database`
- **新增** 可观测性：`Observability.OpenTelemetry` 导出追踪、指标与日志，`Observability.Host` 读取进程与主机运行信息
- **新增** 工作流：JSON 定义模型、定义目录、引擎、内置活动、用户任务、补偿与事件；`Workflow.InMemory`、`Workflow.Database`
- **新增** AI：基于 `Microsoft.Extensions.AI` 的 Provider 目录、`IAIChat`、护栏、操作桥接成模型工具、知识库流水线；`AI.OpenAI`、`AI.VectorData`
- **新增** 每个领域的一致性套件 `<领域>.Conformance`，默认实现与 Provider 通过同一套用例

### 宿主、协议与插件

- **新增** 宿主 `Hosting.AspNetCore`（请求桥接、探针、本地化的 Problem Details、绕过管道端点的 `XiHan` 认证方案）与 `Hosting.CommandLine`（`help` / `graph` / `pipeline` / `operations` / `invoke`，按错误 KIND 的退出码）
- **新增** 协议适配器 `Protocols.Http`（路由推导、OpenAPI 3.1 与权限扩展）、`Protocols.Grpc`（由 Schema 推出 protobuf、`/operations.proto`）、`Protocols.Mcp`（Streamable HTTP 与 stdio）、`Protocols.CloudEvents`（入站 `/events` 与出站 Webhook）
- **新增** 插件协议 `xihan.plugin.v1` 与三种宿主：`Plugins.InProcess`、`Plugins.OutOfProcess`、`Plugins.Wasm`（Experimental），黑盒套件 `Plugins.Conformance`
- **新增** 组合包 `Profiles.Minimal`、`Profiles.Standard`、`Profiles.Full`
- **新增** 命令行工具 `xihan`：`pack` 从 `xihan.app.json` 生成 Dockerfile、Kustomize、Compose 与 Aspire 产物，`graph` / `pipeline` / `operations` / `invoke` 转发给应用
