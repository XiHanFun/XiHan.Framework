# ADR-0068 配置源的失败语义与 Nacos 客户端

## 状态

已采纳（`spec/rfcs/0014-configuration.md` 远程配置、密钥与 Nacos Provider 三节随之修订）。

## 日期

2026-11-01

## 背景

写 5.0 文档站的配置一章时，对照代码发现 `Configuration.Abstractions` 的两个配置源与 `Configuration.Nacos` 有几处行为与 RFC-0014 不一致，或者在失败时丢掉报告、停掉后台工作。本 ADR 记录修正时做出的设计决定，按条目分节。

### 1. 加载与重读的失败处理前后不一

- 远程配置的首次加载只捕获超时。客户端抛出别的异常时直接穿出 `Load`，绕过 `OnError` 与 `Optional`：`Optional = true` 的应用照样启动失败，回调也收不到。轮询路径却捕获全部异常、只回调。
- 密钥配置首次读取超时且 `Optional = true` 时直接返回，不启动重读循环，与 RFC-0014「`RefreshInterval` 非空时周期性重读」矛盾：密钥库在启动那一刻不可用，这个源就一直为空。密钥库抛出别的异常同样绕过 `OnError` 与 `Optional`。
- 密钥库返回的条数与请求不一致：首次读取直接抛出，不回调、不看 `Optional`；重读时静默跳过，什么都不报告。
- `OnError` 自己抛出的异常在轮询与重读循环里没有捕获：后台任务以异常结束，此后不再轮询，也没有任何报告。

### 2. Nacos 的版本认不出

RFC-0014 规定 Nacos 的版本是内容 MD5，与监听接口的约定一致。实现从 `response.Headers` 读 `Content-MD5`，没有时退到内容的 SHA-256。

- `Content-MD5` 是内容头。`HttpClient` 收到真实响应时把它归入 `response.Content.Headers`，`response.Headers` 里永远没有：用真实套接字回一个带 `Content-MD5` 的响应即可复现。所以对真实的 Nacos 总是走到退路。
- Nacos 2.5.1 的监听接口拿客户端报来的值与服务端存的内容 MD5（`MD5Utils.md5Hex(content, PERSIST_ENCODE)`，`PERSIST_ENCODE` 缺省 UTF-8）逐字比较。SHA-256 永远对不上，每个监听窗口都立刻报告变化，客户端再取一次整份文档、比出版本没变，长轮询退化成每个轮询周期一次「监听 + 取文档」的普通轮询。结果仍然正确，一致性套件发现不了。
- 包 README 写的是「取响应头 `Content-MD5`，没有就自己算」，读起来是自己算 MD5，与代码不符。

### 3. 自建的客户端没人释放

`AddXiHanNacos` 用 `NacosConfigurationClient.Create` 自建客户端（自带 `HttpClient`），交给 `AddXiHanRemoteConfiguration(client, options)`。这个重载不持有客户端，配置根释放时只停轮询，客户端与它的连接池从来不释放，也没有别人拿得到它的引用。

### 4. 地址里的路径被丢掉

客户端用 `new UriBuilder(address) { Path = "/nacos/v1/cs/configs" }` 拼接口地址，整体替换了地址的路径。Nacos 改了上下文路径（`server.servlet.contextPath`），或经网关挂在某个前缀下时，地址里写的路径被悄悄丢掉，请求打到 `/nacos/...`，得到 404，被当作 `dataId` 不存在（`CONFIGURATION_NOTFOUND_001`）。包 README 没有写这条限制，docs-next 写了「不适用」。

## 备选

### 1. 失败语义

1. 首次加载时客户端抛出的异常仍然穿出（视为违反契约的编程错误），只修「可选的首次失败不重读」。
2. 首次加载把客户端或密钥库抛出的任何异常都当作失败，与返回失败、超时一样走 `OnError` 与 `Optional`。

`OnError` 抛出时：

1. 原样抛出：首次加载让构建失败，循环里终止循环。
2. 首次加载原样抛出，循环里丢弃。
3. 一律丢弃。

### 2. Nacos 的版本

1. 保留 SHA-256 退路，只把读头的位置改对：常态正确，去掉头的代理后面仍退化。
2. 读头的位置改对，退路改为按服务端的算法算内容 MD5。
3. 去掉退路，没有头就不长轮询。

### 3. 自建客户端的归属

1. `RemoteConfigurationOptions` 加一个「释放时一并释放客户端」的开关，客户端仍在添加源时建好、由配置源共用。
2. 新增工厂重载 `AddXiHanRemoteConfiguration(clientFactory, options)`：每次构建 Provider 按工厂新建一个客户端，归这个 Provider 所有。

### 4. 地址路径的含义

1. 地址的路径是前缀，固定的 `/nacos/v1/...` 接在它后面。`http://host:8848/nacos`（Nacos 控制台的地址，常被直接抄来）从能用变成 `/nacos/nacos/v1/...` 的 404；Nacos 改了上下文路径时仍然表达不了。
2. 地址的路径就是 Nacos 的上下文路径，接口路径（`/v1/...`）接在它后面；没有路径时用 Nacos 的缺省值 `/nacos`。
3. 新增 `ContextPath` 选项（缺省 `nacos`），与 Nacos SDK 的同名参数对应，地址的路径作为网关前缀。

## 决策

### 1. 失败语义统一

取第一组的 2、第二组的 3。

- 首次加载的失败有三种：返回失败（错误码照用）、超时（不可达，原因 `load timeout`）、抛出任何异常（不可达，原因取异常消息）。三种一样处理：先回调 `OnError`；`Optional` 为假则抛出带该错误码的 `FrameworkException`，抛出的异常作为它的内部异常；为真则该源为空。不可达的码远程配置是 `CONFIGURATION_UNAVAILABLE_001`，密钥是 `CONFIGURATION_UNAVAILABLE_002`。
- 密钥库返回的条数与请求不一致是整体失败，与超时、抛出同类：首次读取与重读都回调一次 `CONFIGURATION_UNAVAILABLE_002`，首次按 `Optional` 处理，重读保留全部旧值。
- 不论首次加载成败，`PollInterval` / `RefreshInterval` 非空时都启动循环。远程配置首次失败时已知版本为空，第一次成功的轮询装入整份文档。
- `OnError` 抛出的异常被丢弃：不改变首次加载按 `Optional` 得出的结果，也不终止轮询或重读。
- 配置根释放、循环停止之后，循环里出现的任何异常（取消、客户端被释放）都不再报告。
- 不新增错误码与消息键，原因写进现有不可达消息的 `reason` 参数。

### 2. Nacos 的版本取内容 MD5

取 2。

- 版本取 `response.Content.Headers` 里 `Content-MD5` 的原文（`NonValidated`，不按 RFC 1864 的 Base64 解析）。
- 没有这个头时，按服务端的算法自己算：内容按 UTF-8 编码的 MD5，小写十六进制。
- MD5 触发安全分析器 CA5351。按仓库现有做法，在这一个私有方法上用 `[SuppressMessage]` 并写明理由：这是协议规定的版本标识，只用于与服务端比对是否变化，不用于任何安全目的。不在项目级压掉这条规则。

### 3. 自建客户端归 Provider 所有

取 2。

- `Configuration.Abstractions` 新增 `AddXiHanRemoteConfiguration(Func<IRemoteConfigurationClient> clientFactory, RemoteConfigurationOptions options)`，配置源是内部类型。每次构建 Provider 调用一次工厂，Provider 释放时先停轮询，再释放客户端（实现 `IDisposable` 时）。
- 原重载 `AddXiHanRemoteConfiguration(client, options)` 语义不变，写明客户端由调用方持有、配置根释放时不释放。
- `AddXiHanNacos` 改走工厂重载。运行期由 `NacosConfigurationFeature` 登记进容器的客户端仍归容器。

### 4. 地址的路径是上下文路径

取 2。登录、取配置、监听三个接口一样处理：去掉地址路径末尾的 `/`，为空时用 `/nacos`，再接上 `/v1/auth/login`、`/v1/cs/configs`、`/v1/cs/configs/listener`。上下文路径设成根（`/`）的 Nacos 因此无法表达，写明不受支持。

## 理由

### 1. 失败语义统一

- 客户端契约要求以失败结果表达不可达，但抛出的异常在调用方看来同样是「这次没取到」。`Optional` 回答的是「配置中心暂时不可用时能不能启动」，不应由失败的形式（返回还是抛出）决定。轮询路径本来就这样处理，首次加载与它对齐。
- `Optional` 的意思是「先空着，之后补上」。不重读就成了「一直空着」，而且没有任何信号提示这一点。
- 条数不一致时逐项结果对不上配置键，只能整体处理；静默跳过让运维看不到密钥库出了问题。
- `OnError` 是配置构建期唯一的报告渠道，再往上没有可报告的地方。原样抛出，要么让 `Optional = true` 的应用启动失败，要么让后台循环悄无声息地停掉，两者都比丢弃回调自己的异常更糟。回调失败只影响报告，不应影响配置的取值。

### 2. Nacos 的版本取内容 MD5

- 监听协议只认内容 MD5，版本取别的摘要等于放弃长轮询。退路照服务端的算法算，结果与服务端给的头一致，去掉头的代理后面也能长轮询。
- 读原文而不是解析后的值：Nacos 给的是十六进制，不是 RFC 1864 规定的 Base64。32 位十六进制恰好也是合法的 Base64，解析再格式化会还原成同一个串，但这是巧合，不应依赖。

### 3. 自建客户端归 Provider 所有

- 配置源不可释放，可释放的只有 Provider；而同一个源会构建出多个 Provider：同一构建器构建多次，或 `ConfigurationManager` 在属性、源列表变化时重建全部 Provider 并释放旧的。共用一个客户端时，第一个 Provider 释放就把它关掉，其余 Provider 拿着已释放的客户端每个周期报一次错。按 Provider 各建一个，归属与生命周期才一致。
- 先停轮询再释放客户端，配合第 1 节「停止之后不再报告」，释放不会经 `OnError` 报出一条假失败。

### 4. 地址的路径是上下文路径

- 向后兼容：此前能用的两种写法 `http://host:8848` 与 `http://host:8848/nacos` 结果不变；此前被丢掉的路径现在都生效。备选 1 会让第二种写法失效，而且失效的表现是「`dataId` 不存在」，不容易想到是地址的问题。
- 一个地址就能表达改过的上下文路径与网关前缀，不新增选项；多个地址各自带路径，按序尝试时互不影响。备选 3 要同时理解两个概念，地址的路径与 `ContextPath` 还可能重复。
- 代价是根上下文无法表达。Nacos 的部署文档与镜像都用 `/nacos`，根上下文罕见；真有需要时再加选项，不为它让常见写法多一个概念。

## 影响

### 1. 失败语义统一

- 行为：首次加载时客户端或密钥库抛出的异常，由原样穿出改为 `FrameworkException`（不可达，原异常是内部异常）；`Optional = true` 时不再让启动失败。密钥配置可选的首次失败之后照常重读。密钥库返回条数不一致时首次读取会回调、看 `Optional`，重读时回调并保留旧值。回调抛出不再终止循环。
- 公开 API 不变。
- 测试（`Configuration.Abstractions.Tests`，计时一律用 `FakeTimeProvider` 推进）：
  - 远程配置首次加载时客户端抛出：必需时回调一次再抛出不可达，原异常是内部异常；可选时为空、回调一次，客户端恢复后第一次轮询装入整份文档并触发重载。
  - 回调抛出：首次加载的结果不变（可选为空、必需照常抛出），轮询中连续两个周期照常取值、照常回调，远端恢复后照常装入新版本。
  - 密钥配置首次读取超时且可选：为空、回调一次 `load timeout`，之后照常重读，密钥库恢复后第一次重读装入并触发重载。
  - 条数不一致：首次读取必需时回调并抛出不可达，可选时为空；重读时回调一次并保留全部旧值，回调抛出之后下一个周期照常写回新值。
  - 密钥库整体抛出：原有用例断言抛出的是原始 `InvalidOperationException`，改为断言 `FrameworkException`（不可达，原异常是内部异常）并回调一次，这是本决定改变的契约。

### 2. Nacos 的版本取内容 MD5

- 行为：对真实的 Nacos，监听恢复为长轮询，内容没变时窗口内不再取整份文档；版本串由 64 个十六进制字符的 SHA-256 变为 32 个字符的 MD5，进程内的已知版本随首次加载重建，不涉及持久化。
- 公开 API 不变。
- 测试（`Configuration.Nacos.Tests`，HTTP 桩 `NacosStubHandler` 按 Nacos 的约定逐字比较监听报来的 MD5，`Content-MD5` 与真实响应一样放在内容头）：有头与没有头两种情况下，版本都等于独立算出的内容 MD5（内容含非 ASCII 字符），以它监听时服务端认出未变化、不再取整份文档；头里的值原样作版本。修正前三例都失败，有头的一例同样失败，印证了头从来没被读到。

### 3. 自建客户端归 Provider 所有

- 行为：`AddXiHanNacos` 自建的客户端随配置根释放；同一构建器构建多次时各有各的客户端。
- 公开 API：`Configuration.Abstractions` 新增 `AddXiHanRemoteConfiguration(clientFactory, options)` 一个重载（PublicAPI +1）。`Configuration.Nacos` 的公开 API 不变，`Create` 与 `AddXiHanNacos` 各多一个供测试替换 HTTP 处理器的内部重载。
- 测试：
  - `Configuration.Abstractions.Tests`：同一构建器构建两次各建一个客户端、各随自己的配置根释放；`ConfigurationManager` 改动属性重建全部源时，被替换的 Provider 连同客户端释放、新 Provider 用新客户端；传入现成客户端时配置根释放不释放它。
  - `Configuration.Nacos.Tests`：经 `AddXiHanNacos` 的内部重载换入 HTTP 桩，构建两次各自装入文档，配置根释放时各自的 HTTP 处理器随客户端释放。
  - 反向验证：Provider 不释放客户端时 Abstractions 用例失败；`AddXiHanNacos` 改回传入现成客户端时 Nacos 用例失败（只建了一个客户端）。

### 4. 地址的路径是上下文路径

- 行为：地址带路径时请求打到这个路径下；此前这样的地址请求的是 `/nacos/...`。不带路径或路径就是 `/nacos` 的地址不变。
- 公开 API 不变，`NacosOptions.ServerAddresses` 的注释写明路径的含义。
- 测试（`Configuration.Nacos.Tests`，HTTP 桩只在给定的上下文路径下作答，带用户名密码以覆盖登录）：`http://nacos.test:8848`、末尾带 `/`、`/nacos`、改过的 `/config`、网关前缀 `/config-center/nacos/` 五种地址，登录、取配置、监听依次打到上下文路径下。换回修正前的客户端，前三种照常通过（向后兼容），后两种失败。
