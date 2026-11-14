# ADR-0229 框架缓存走 ICache 与跨实例失效

## 状态

已采纳（`spec/rfcs/0006-caching.md`、`spec/rfcs/0007-multitenancy.md`、`spec/rfcs/0008-security.md`、`spec/rfcs/0010-localization.md` 随之修订）。修订 ADR-0027「授予缓存放存储里、多实例靠 TTL 收敛」与 ADR-0193「不做跨实例广播、分片缓存按存活时间过期」；修订 ADR-0194 第三节「存活时间为 0 仍然允许」。

## 日期

2026-11-14

## 背景

BasicApp 多实例部署时装 Redis 与两级缓存（`RedisCachingFeature` + `HybridCachingFeature`），自己的业务缓存都走 `ISharedCache`。框架里有几处缓存没有走缓存抽象，不管配不配 Redis，都只缓存在本实例内存里：

- `DatabasePermissionGrantStore`：按（主体，租户）的有界 TTL 表。`PermissionGrantRecordStore` 写入后的失效只到当前实例，其余实例要等 `CacheTimeToLive`（缺省 30 秒）才看到撤销。
- `DatabaseTenantStore`：按租户的 TTL 表。停用、改连接串、删除之后，其余实例在存活时间内仍按旧租户解析。
- `TenantStoreShardResolver`：在租户存储之上又缓存一层分片（ADR-0193 给它加了存活时间）。迁库之后，其余实例在两层缓存都过期前仍往旧库写。
- `Localization.Database` 的翻译快照：只按周期（缺省 5 分钟）刷新。`MessageTranslationStore` 写入后，连写入的实例自己都不会立即重载；`RefreshInterval` 为 0 时，其余实例永远看不到新翻译。

用户要求：前三处改用 `ICache`，多实例要一致；框架里其他默认放内存、会被写入改变的缓存也换成 `ICache`。

约束：

- 授权判定与租户解析每个请求都要走，读取必须能命中本机一级缓存，不能每次远程往返。
- 失败语义不能变：授予读库失败仍是 `SECURITY_UNAVAILABLE_001`（503），失败不进缓存，缓存或存储故障不能当成「没有授予」。
- ADR-0194 的「失效之前开始的读取不能把旧值写回」要在多实例下继续成立。另一个实例的读取可能在本实例失效之前开始、失效之后才写回，本实例的进程内代数挡不住它。
- 跨进程缓存要序列化值，序列化器可能是应用用自己的源生成上下文登记的，那个上下文不认识框架的类型。

核对 `HybridObjectCache` 的读路径：L1 命中直接返回，不碰 L2，满足热路径要求。但它有一个竞争：L1 未命中时去读 L2，读的途中另一个实例写了这个键，变更通知先到（L1 里本来没有，删了个空），读回的旧值后到并回填 L1，旧值就留到 `LocalTimeToLive`（缺省 30 秒）。这正是跨实例失效要防的情况。

## 备选

缓存放哪里：

1. **各处继续手写进程内缓存，另加跨实例广播（消息总线）**：否决。框架的默认实现不依赖消息基础设施（ADR-0193 的理由仍然成立），每处还要各自处理订阅、丢失与重放。
2. **直接用 `ISharedCache`**：否决。装了 Redis 就是每次判定一次远程往返，不满足热路径要求。
3. **用 `ICache`**：采纳。单实例下就是内存实现；装了 Redis 与两级缓存时，L1 命中不出进程，写入经 Redis 频道失效各实例的 L1（ADR-0055）。多实例一致的前提从「每个包自己想办法」收拢成一条：装两级缓存。

防旧值写回：

1. **只靠两次失效**（写入时一次、提交之后一次）：否决。另一个实例在提交之前读库、在第二次失效之后才写回，旧值留到存活时间结束。
2. **按范围删键**：否决。`ICache` 没有按前缀删除；授予缓存的条目按（主体，租户）分，失效的实例不知道别的实例缓存了哪些租户。
3. **代数也放进缓存**：采纳。每个范围（主体、租户）与整个区各有一个代数（随机令牌），存在 `ICache` 里；条目写回时记下读取开始时的代数，读取时代数都还是当前值才算命中。失效就是换一个新代数，不需要知道这个范围下有哪些条目；别的实例写回的旧条目带着旧代数，没有人会当作命中。

分片解析器：

1. **分片也走 `ICache`，自己一套代数**：否决。两层缓存、两套失效，ADR-0193 的问题正是数据库租户存储的缓存被分片缓存挡住。
2. **分片解析器不再缓存**：采纳。它每次读租户存储（数据库租户存储已经走 `ICache`，内存租户存储本来就是内存），从属性推出分片的开销是一次字典遍历。缓存与失效只在租户存储一处。

翻译快照：

1. **每次查找读 `ICache`**：否决。`IMessageLocalizer` 的查找是同步的，也是日志、异常消息的热路径。
2. **整份快照放进 `ICache`**：否决。上限十万条，序列化进 Redis 太重，查找仍要同步拿到对象。
3. **快照留在进程内，`ICache` 里放版本戳**：采纳。写入提交后换版本戳，各实例按短间隔（缺省 1 秒）核对，变了就重新加载；两级缓存下核对是 L1 命中。

缓存后端故障：

1. **读取失败返回 503**：否决。数据库是事实来源，缓存只是加速；Redis 故障不该让授权整体不可用。
2. **读取绕过缓存直接查源、不写回**：采纳。结果仍然正确，代价是故障期间多查库。
3. **写入时的失效失败**：拒绝写入（异常抛出、事务回滚）。撤销传不到别的实例时不能报成功；提交之后的第二次失效失败只记错误日志，此时写入已经提交，靠存活时间收敛。

## 决策

1. **`Caching.Abstractions`**：
   - `GenerationCache<T>`（Preview）：架在 `ICache` 上的读穿缓存区，构造参数为缓存、区名、条目存活时间、日志。
     - `GetOrLoadManyAsync(keys, load)`：批量读，结果与入参顺序一一对应，重复位置只查一次，空集合不触缓存。未命中的一次交给 `load`；`load` 失败原样返回、不写缓存。
     - `InvalidateAsync(scopes)`：给每个范围换新代数，一次批量写入；空集合什么也不做。
     - `InvalidateAllAsync()`：换区代数。
     - 读取在查源之前取代数，缺代数时先写下新代数再查源。代数的存活时间是条目的 10 倍；代数过期或被淘汰后，新代数让旧条目作废，只多一次查源。
     - 键是 `<区>:gen`、`<区>:gen:<范围>`、`<区>:entry:<范围>:<条目>`。范围与条目按 `<长度>.<文本>` 拼接，含空白、控制字符或超过 128 个字符的换成 `#<SHA-256>`。
     - 缓存后端故障（`CACHING_UNAVAILABLE_001`）时，读取直接查源、不写回，每个区每 10 秒最多记一次警告；失效把异常抛给调用方。
   - `GenerationCacheKey`（范围、条目）与 `GenerationCacheEntry<T>`（区代数、范围代数、值）。后者是跨进程缓存里的存储形状。
   - `CacheSerializerRegistration.AddTypeInfo(services, resolver)` 与 `CacheTypeInfoSource`（Preview）：组件登记自己放进缓存的类型的源生成上下文。应用登记了 `JsonSerializerOptions` 时，`TryAddJsonSerializer` 把这些上下文并在应用解析器之后；反射版本来就认识全部类型；应用自己登记 `ICacheSerializer` 时要自行处理。
2. **`Caching.Hybrid`**：L1 按键散列分 64 个条带记失效轮次，删除一个键让它所在条带的轮次加一，按前缀删除让全部条带加一。读 L2 之前记下轮次，回填前核对，读的途中有过失效就不回填（`GetAsync`、`GetManyAsync`、`GetOrCreateAsync`）。写入后照旧写进 L1。
3. **`Security.Database`**：
   - `DatabasePermissionGrantStore` 用 `GenerationCache`，区 `security:grants`，范围是主体（`<种类>:<ID>`），条目是租户（平台为空字符串）。
   - `Invalidate` / `InvalidateAll` 改为 `InvalidateAsync` / `InvalidateAllAsync`，作用于所有共用缓存的实例；去掉 `CachedEntryCount`。
   - 构造参数的 `TimeProvider` 换成 `ICache`。
   - 返回给调用方的仍是冻结集合，同一个缓存对象只建一次。
   - `PermissionGrantRecordStore` 实现 `IAsyncDisposable`，构造多了 `ILoggerFactory`。写入时的失效失败就抛出；释放时的失效失败记错误日志。同步释放阻塞等待异步释放完成。
   - 清单依赖 `caching.local@1`。
   - 选项去掉 `CacheSize`（容量由提供 `caching.local` 的 Provider 管）。`CacheTimeToLive` 改为必须为正的 `TimeSpan`，配置 0 时装配失败。
4. **`MultiTenancy.Database`**：
   - `DatabaseTenantStore` 同样用 `GenerationCache`，区 `multitenancy:tenants`，范围是租户 ID；不存在也缓存；查库改用 `FindManyAsync`。
   - `Invalidate` / `InvalidateAll` 改为异步，去掉 `CachedCount`，构造参数换成 `ICache`。
   - `TenantRecordStore` 与授予写入方同样处理两次失效。
   - 清单依赖 `caching.local@1`。选项的修改与授予缓存相同。
5. **`MultiTenancy.Abstractions`**：
   - `TenantStoreShardResolver` 不再缓存，每次读租户存储。构造改为 `(ITenantStore, ILoggerFactory)`；`Invalidate` / `InvalidateAll` 什么也不做，仍校验入参。
   - `MultiTenancyOptions` 去掉 `ShardCacheSize` 与 `ShardCacheTimeToLive`。
   - `ITenantShardResolver` 契约不变，应用自己的解析器仍可以缓存。
6. **`Localization.Database`**：
   - `MessageTranslationLoader` 在 `ICache` 的 `localization:translations:version` 放版本戳。
   - 新增 `PublishChangeAsync`：换版本戳，并唤醒本实例立即重新加载。
   - 后台循环按 `ChangeCheckInterval` 核对版本戳（缺省 1 秒，配置键 `ChangeCheckIntervalSeconds`，0 关闭），变了就重新加载；`RefreshInterval` 的周期刷新保留，兜住直接改表与版本戳丢失。
   - 重新加载彼此串行。
   - `MessageTranslationStore` 实现 `IAsyncDisposable`，写过翻译就在释放时（提交之后）调用 `PublishChangeAsync`，失败记错误日志。
   - 清单依赖 `caching.local@1`。
7. **盘点之后不改的**：
   - 调度引擎的本地触发器表（`JobSchedulerEngine._local`）：混着本节点推算的触发进度，不是数据缓存。别的节点约滞后 10 次轮询，改法是订阅已有的作业变更事件，另立一项。
   - 发件箱的分片清单（`OutboxDispatcher`）：只按存活时间（1 分钟）刷新，没有「只失效本实例」的不对称，代价只是新分片库的投递晚一会儿。分片解析器不再缓存之后，滞后只剩它自己这一层。
   - 文件系统对象存储的配额计量：需要原子计数器，不是缓存，文件系统 Provider 本是单节点。
   - 活对象注册表（`AIProviderCatalog`、`DataSourceRegistry`、`BlobStores`）：存的是客户端实例，不能序列化。定义来自启动期配置或应用传入。
   - 外部来源自带刷新（OIDC 元数据与 JWKS、远程配置与密钥、Nacos 令牌、SqlSugar 从库健康）：每个实例各自向来源拉取才对。
   - 纯计算或反射的备忘（表达式编译、JSON Schema、实体映射等）与启动期注册表：不随数据变化。
   - OpenAPI 文档缓存：生成之后不随翻译更新，另立一项。

## 理由

- 一致性前提只剩一条：多实例部署装两级缓存。框架自己的缓存与应用的缓存走同一套失效，运维只需要看一处的健康项与配置。
- 代数放进缓存，失效不需要知道条目分布，也不需要原子自增（`ICacheStore` 没有）。随机令牌永不重复，代数丢了只会多查源，不会把旧条目当作命中。
- 读取先取代数再查源，失效先换代数：查源读到的数据不早于记下的代数被写下的那一刻。再加上提交之后的第二次失效，ADR-0194 的保证在多实例下仍然成立。
- 缓存故障时绕过缓存查源，正确性只依赖数据库。写入时失效失败就拒绝，「撤销成功」与「各实例看到撤销」不会脱节。
- 回填核对失效轮次堵住两级缓存自己的读写竞争，所有 `ICache` 使用者都受益；条带数固定，内存有界。

## 影响

- 破坏性变更（5.0 尚未正式发布，均在 Unshipped，不登记豁免）：
  - 直接调用 `DatabasePermissionGrantStore.Invalidate` / `InvalidateAll`、`DatabaseTenantStore.Invalidate` / `InvalidateAll` 的代码改为 `await …InvalidateAsync(…, ct)` / `InvalidateAllAsync(ct)`。
  - 直接构造这些存储、`TenantStoreShardResolver`、`PermissionGrantRecordStore`、`TenantRecordStore`、`MessageTranslationStore`、`MessageTranslationLoader` 的代码按新签名传参；经容器构造的不受影响。
  - 删掉配置里的以下键，不删的照常被忽略：
    - `XiHan:Security:Database:CacheSize`
    - `XiHan:MultiTenancy:Database:CacheSize`
    - `XiHan:MultiTenancy:ShardCacheSize`
    - `XiHan:MultiTenancy:ShardCacheTimeToLiveSeconds`
  - 两处 `CacheTimeToLiveSeconds` 不再接受 0。
  - 只装 `Security.Database`、`MultiTenancy.Database`、`Localization.Database` 而不装缓存 Provider 的宿主，要加 `InMemoryCachingFeature`（Standard / Full 组合包已带）。
- 多实例部署要装 `Caching.Redis` 与 `Caching.Hybrid`。只装 Redis 时 `ICache` 仍是进程内实现，框架的这些缓存跨实例只靠存活时间收敛，与改动之前相同。
- 存活时间退为兜底，覆盖失效通知丢失、释放时失效失败、绕过写入方直接改表三种情况。默认值不变（30 秒），现在可以按命中率调大。
- 自己登记 `ICacheSerializer` 的应用，要能序列化 `GenerationCacheEntry<GrantSetEntry>`、`GenerationCacheEntry<TenantCacheValue>` 与 `string`。框架的上下文经 `CacheTypeInfoSource` 取得，可以并进自己的解析器。
- 两级缓存在读的途中有失效时少回填一次 L1，下一次读取再去 L2。
- 缓存值的序列化形状以后要变时换区名（如 `security:grants` 加后缀）：滚动升级期间新旧实例共用缓存后端，同一个区里读到对方写的条目会反序列化失败。
- 测试：
  - 单实例：失效立即生效，代数挡住失效之前开始的读取，缓存后端故障时读取直查、写入拒绝、释放时只记日志。
  - 两级缓存：回填竞争。
  - 有 `XIHAN_TEST_REDIS` 时：两个实例共用 Redis 与同一个库，一个实例写入，另一个实例立即读到。
