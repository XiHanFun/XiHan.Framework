# ADR-0093 S3 条件写入探测被拒与缺桶启动失败、用户名遮蔽范围已定与 Zenko 补跑

## 状态

已采纳（`spec/rfcs/0012-objectstorage.md`、`spec/conventions/error-codes.md`、`spec/rfcs/0016-packaging.md`、`spec/rfcs/0014-configuration.md`、`spec/rfcs/0023-observability.md` 随之修订）。修订 ADR-0090 第一节「被拒绝与探测桶不存在记为尚未确认、照常启动」；ADR-0090 第四节「范围之外、留待再定」的两处与 ADR-0084 的连接信息脱敏范围在此定为维持。

## 日期

2026-11-02

本 ADR 记录第七批回灌里资源分区的三项决定（`k-batch-backlog.md` 第 1、2、3 条，来由是 ADR-0090 留下的待定项），每项一节。

## 一、S3 条件写入探测被拒绝、探测桶不存在恢复为启动失败（第 1 条，修订 ADR-0090 第一节）

### 背景

ADR-0089 让 S3 Provider 在缺省实例启动时、命名实例建出时探测服务是否认 `If-None-Match: *`，不认、被拒绝、探测桶不存在、连不上四种失败都让启动失败。ADR-0090 按用户"恢复照常启动、健康降级"的意见，把后三种都改成"尚未确认"：照常启动、后台重试，修好后自动恢复。被拒绝与缺桶归入尚未确认是 ADR-0090 的推论（理由是改 IAM、建桶不需要动应用），第六批汇报里列为待定。用户拍板：凭据被拒、桶不存在是配置错误，要早暴露，恢复为启动失败；只有"连不上"照常启动、后台重试、修好自动恢复。

### 备选

- **启动时的被拒绝与缺桶**：照 ADR-0090 记为尚未确认（配置错误要等有人看健康项才发现，部署流水线以为发布成功）；**启动失败（选中）**——密钥写错、只读凭据、桶名拼错、桶还没建都是部署时就该发现的错误，启动失败让编排器的发布失败、回滚，而不是带着"不覆盖写入一律 503"的应用进入服务。
- **连不上**：同样启动失败（S3 比应用晚就绪的编排里应用起不来，ADR-0090 已否决）；**照常启动、后台重试（选中，不变）**。
- **启动之后的探测才被拒绝或缺桶**（只会发生在启动时连不上的实例上：连上时凭据已被吊销、桶已被删，或配置本来就错而启动时探不到）：
  - 让应用停止：运行中的服务被一个子系统拖垮，ADR-0090 对"之后才探明不认"已否决同样的做法。
  - 当作终态、不再重试：改好权限或建好桶之后还要重启，而启动时同样的错误修好后重启即可，运行中没有理由更差。
  - 与连不上一样 Degraded：看不出是要人处理的配置错误。
  - **仍是尚未确认、照常按退避重试，健康项 Failed，记 Error 日志，不覆盖写入仍是 `OBJECTSTORAGE_UNAVAILABLE_004`（选中）**。
- **启动之后被拒时不覆盖写入返回什么**：直接回探测的错误（403 `OBJECTSTORAGE_DENIED_002` / 404 `OBJECTSTORAGE_NOTFOUND_001`）——这次写入的调用方没有过错，403 / 404 会让它以为自己没有权限或写错了桶；**仍是 `OBJECTSTORAGE_UNAVAILABLE_004`（503），`reason` 带出 `DENIED_002` / `NOTFOUND_001`（选中）**——写入目标没有被访问，原因由健康项与日志报告给运维。

### 决策

- 一次探测的结论分四类：已确认；探明不认（`OBJECTSTORAGE_UNAVAILABLE_003`）；配置错误——被拒绝（`OBJECTSTORAGE_DENIED_002`）、探测桶不存在（`OBJECTSTORAGE_NOTFOUND_001`）；连不上（`OBJECTSTORAGE_UNAVAILABLE_001`：连不上、超时或其余服务端错误）。后两类都是"尚未确认"（服务认不认条件写入还不知道）。探测本身（探测键、两次带条件写入、分片的条件完成、单次超时、`EnsureBuckets` 时先建桶）不变。
- 启动或建实例时：探明不认、被拒绝、探测桶不存在 → 缺省实例以 `KERNEL_HOSTING_002`（内层为对应码，消息键 `objectstorage.conditional_write.unsupported` / `probe_denied` / `probe_bucket_missing`，参数不变）启动失败，已起的健康循环先停；命名实例不建、不登记，`S3BlobStoreProvider.CreateAsync` 抛出携带该码的 `FrameworkException`，注册表转成同码的失败结果，下次取用重新探测。只有连不上 → 照常启动、照常建出并登记，后台按退避重试（1 秒起倍增，上限 `HealthProbeIntervalSeconds`，注入的 `TimeProvider`），S3 就绪后自动确认。实现只改内部类型 `S3ConditionalWriteGate.StartAsync` 的判定（新增私有判定 `IsConfigurationError`），这次失败不起后台重试，也不另记日志（启动失败本身就是报告）。
- 启动之后（后台重试或写入时的就地探测）才被拒绝或缺桶：应用不停止；状态仍是尚未确认，后台照常按退避重试，权限改好或桶建好后自动确认、不必重启；不覆盖写入（`Overwrite = false` 的写入、分片上传的发起与完成）返回 `OBJECTSTORAGE_UNAVAILABLE_004`（HTTP 503，`reason` 为 `OBJECTSTORAGE_DENIED_002` 或 `OBJECTSTORAGE_NOTFOUND_001`），不访问写入目标；缺省实例的健康项 `objectstorage.s3.connection` 为 **Failed**（错误为该码，数据 `conditionalWrites` 仍是 `unconfirmed`；连不上仍是 Degraded），应用的聚合状态 Degraded、仍就绪；命名实例的健康项照注册表一律记 Degraded（不改抽象），错误同上；原因变化时记一条 Error 日志（新增 `LoggerMessage`，连不上仍是 Warning），稳定的失败不随每次重试刷屏。
- 已确认之后凭据被吊销或桶被删除不在此列：已确认是终态、不再探测（服务的能力不会在运行中改变），此后的读写照常访问存储，按既有错误映射失败（被拒等服务端错误 → `OBJECTSTORAGE_UNAVAILABLE_001`），连通性的 HEAD 失败让健康项 Degraded。
- 记日志的时机微调：状态变化的日志先记、终态信号（内部的 `Settled`）后发，等终态的一方看到的日志已经完整。
- 公开 API、错误码、消息键、配置键都不变；`OBJECTSTORAGE_NOTFOUND_001`、`OBJECTSTORAGE_DENIED_002`、`OBJECTSTORAGE_UNAVAILABLE_004` 的说明改写（`error-codes.md`、`ObjectStorageErrors` 的 XML 文档）。

### 理由

- 用户定的原则是"配置错误要早暴露"：密钥与桶名在部署时就确定，启动这一刻就能判定，启动失败比上线后不覆盖写入一律 503 更早、更明确；ADR-0090 担心的"编排器反复重启"只对暂时性的失败成立，而它们留在"连不上"一类里照常启动。
- 启动之后的配置错误不能再让启动失败，所以用健康项 Failed（内核里表示"要人处理"）与 Error 日志报告；继续重试的代价有界（退避上限即健康探测间隔，与健康项同一节奏），换来修好后不必重启。
- 写入仍返回 `UNAVAILABLE_004`：调用方无法也不该处理服务端的凭据问题，503 让它稍后重试即可；错误码的含义"暂不可用、探测在重试"仍然准确。

### 影响

- **启动期新增失败条件**（装配 `S3ObjectStorageFeature` 且它提供缺省实例、`ConditionalWrites` 开启时）：凭据被拒（密钥错、只读、无权写探测键 `<HealthBucket>/.xihan/probe/*` 或放弃分片上传、`EnsureBuckets` 时无权建桶）→ `KERNEL_HOSTING_002` ← `OBJECTSTORAGE_DENIED_002`；探测桶不存在且没开 `EnsureBuckets` → `KERNEL_HOSTING_002` ← `OBJECTSTORAGE_NOTFOUND_001`。这是撤回 ADR-0090 的放宽、回到 ADR-0089 的这两条；连不上仍照常启动。命名实例（`s3`）同理：这两种情况 `GetAsync` / `GetOrAddAsync` 返回对应码、不登记（ADR-0090 之下是照常建出）。迁移：部署前建好探测桶（或开 `EnsureBuckets`），给凭据开探测键的写入与放弃分片上传权限；Compose 里连 RustFS 时同样要先建桶或开 `EnsureBuckets`（RustFS 不自动建桶；它晚于应用就绪时照常启动）。
- 行为：启动时连不上、之后才被拒或缺桶时健康项由 Degraded 改为 Failed，日志由 Warning 改为 Error；不覆盖写入的返回不变（`UNAVAILABLE_004`）。
- 测试（`ObjectStorage.S3.Tests`，替身 `FakeS3Client` 加 `FakeTimeProvider`，不靠真实等待；新增测试辅助 `LogCapture`）：启动时被拒、缺桶即启动失败、不起后台重试、不记日志，开 `EnsureBuckets` 时先建桶通过；命名实例被拒、缺桶不建，建好桶后下次取用建出；启动时连不上、之后才被拒 → 健康项 Failed、写入 `UNAVAILABLE_004`（`reason` 为 `DENIED_002`）、不访问写入目标、继续按退避重试、权限改好后确认，日志依次 Warning / Error / Information；之后才缺桶 → Failed、建桶后确认；命名实例连上后才被拒 → 健康探测带出 `DENIED_002`、实例仍登记、之后恢复。对真实服务的用例改为：密钥错启动失败（`DENIED_002`），命名实例密钥错、缺桶不建；健康桶不存在且条件写入开启即启动失败（`NOTFOUND_001`），关闭时照常启动、条目降级。另在 Docker 上对 RustFS 1.0.4 手工核对：密钥错 `KERNEL_HOSTING_002 ← OBJECTSTORAGE_DENIED_002 (reason=SignatureDoesNotMatch)`；缺桶 `← OBJECTSTORAGE_NOTFOUND_001`；容器停着时照常启动（`Degraded`，`OBJECTSTORAGE_UNAVAILABLE_001`），`docker start` 后约 1 秒确认、不覆盖写入成功、再写 `OBJECTSTORAGE_CONFLICT_001`；容器停着且密钥错时照常启动，`docker start` 后约 1 秒健康项 `Failed`（`OBJECTSTORAGE_DENIED_002`），应用 `Degraded`、仍就绪，不覆盖写入 `OBJECTSTORAGE_UNAVAILABLE_004`。

## 二、用户名打码不扩到 `RuntimeInfo.UserName` 与驱动消息里的 `Username=`（第 2 条，维持 ADR-0084）

### 背景

ADR-0090 第四节让与凭据同在一个记录里的登录用户名（Redis `User`、RabbitMQ `UserName`、Nacos `Username`）在选项打印时遮蔽，并把两处列为"范围之外、留待再定"：观测包的 `RuntimeInfo.UserName`（运行进程的系统账户），与持久化驱动异常消息里连接串的 `Username=` / `User Id=` 等键（ADR-0084 定为保留）。用户拍板：都不扩，维持 ADR-0084。

### 决策

- 用户名的遮蔽范围到"与口令、密钥等凭据同在一个记录里的 `User` / `UserName` / `Username`"为止，已定：
  - `RuntimeInfo.UserName` 照常打印。它是主机诊断数据（`IHostMonitor.ReadRuntime()` 交给管理界面展示的运行进程账户），不是任何服务的登录凭据，所在记录不带凭据，架构测试 `CredentialRedactionTests` 的规则本来就不覆盖它。
  - 持久化驱动异常消息经 `ConnectionStrings.Redact` 的脱敏维持 ADR-0084：只遮整串连接串与口令类键（`password`、`pwd`、`secret`、`token`、`key`）的值，主机、端口、库名、用户名保留，排查连接问题要用。选项打印的遮蔽规则（RFC-0014）与驱动消息的脱敏（RFC-0005「机密」）是两件事，前者不扩到后者。
- 不改代码。RFC-0014「选项里的凭据」规则 2 与 RFC-0023「契约」写明已定；配置、持久化与可观测性指南同步写明。

### 理由

- 遮蔽 `RuntimeInfo.UserName` 没有安全收益（它不是凭据的一半），反而让诊断页看不到进程以哪个账户运行；为一个二十多个属性的诊断记录手写 `PrintMembers` 只增加维护面。
- 驱动消息里的用户名是排查"连的是哪个账户、为什么被拒"的关键信息，口令已经遮蔽；ADR-0084 的理由不变。

### 影响

- 代码、公开 API、错误码、配置键都不变。以后新加的带凭据的公开记录仍按 RFC-0014 规则遮蔽同一记录里的用户名，不带凭据的记录里的用户名照常打印。

## 三、Zenko CloudServer 补跑"不认条件写入的真实服务"用例（第 3 条）

### 背景

第六批（ADR-0090）没有拉取 Zenko 镜像，门控用例 `UnconditionalService_FailsTheStartup_AndTheNamedStoreCreation`（`XIHAN_TEST_S3_UNCONDITIONAL`）跳过。用户同意拉取官方仓库的固定 tag 在本机补跑一次。

### 决策与结果

- 镜像 `zenko/cloudserver:8.2.7`（Docker Hub 官方仓库），拉取前 `docker buildx imagetools inspect` 核对 tag 仍在，摘要 `sha256:44ef9109901a223aca3151d6f980bf1995b55e80301965219c1fa512ba94d4e0`，与第五批（ADR-0089）实测的摘要相同。容器以 `SCALITY_ACCESS_KEY_ID` / `SCALITY_SECRET_ACCESS_KEY`（与 RustFS 相同的测试凭据）、`REMOTE_MANAGEMENT_DISABLE=1` 启动。
- curl 8.22（`--aws-sigv4`）对探测键 `.xihan/probe/conditional-write` 的原始响应码：建桶 200；第一次带 `If-None-Match: *` 的 `PutObject` 200，**第二次 200**（RustFS 1.0.4 为 412）；带条件完成分片上传 **200**（RustFS 为 412）；放弃上传 404（上传已完成）；错密钥 403；缺桶 404。与第五批一致：Zenko 忽略条件头。
- 门控用例在本分支的实现上通过：缺省实例启动失败 `KERNEL_HOSTING_002 ← OBJECTSTORAGE_UNAVAILABLE_003`（参数 `bucket=xihan-probe`、`operation=PutObject`），命名实例返回 `OBJECTSTORAGE_UNAVAILABLE_003`、不登记。`ObjectStorage.S3.Tests` 同时指向 RustFS 1.0.4（`XIHAN_TEST_S3`）与 Zenko（`XIHAN_TEST_S3_UNCONDITIONAL`）时 69 / 69 通过、0 跳过。
- 同一构建上的全量测试（`XIHAN_TEST_S3` 指向 RustFS、`XIHAN_TEST_S3_UNCONDITIONAL` 指向 Zenko）中 `ObjectStorage.S3.Tests` 0 跳过。跑完停掉并删除了容器，拉取的 `zenko/cloudserver:8.2.7` 镜像随后删除（本机原来没有它）；`rustfs/rustfs:1.0.4` 是本机原有的，保留。

### 影响

- 只是验证，不改代码与规范；RFC-0012 的"不认的服务启动失败"与 ADR-0089 的实测结论在第七批的实现上再次确认。

> 修订（2026-11-02）：第一节「启动时连不上、之后的探测才被拒绝或缺桶」的处理（照常重试、缺省实例健康项 Failed、不覆盖写入仍是 `OBJECTSTORAGE_UNAVAILABLE_004`）由 ADR-0097 第四节定为已定，不改为停止重试或只记 Degraded。
