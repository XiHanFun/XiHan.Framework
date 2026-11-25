# 资源绑定

这一页讲特性怎样拿到数据库、Redis、消息代理这类外部资源的连接信息：在清单里声明一个绑定，由宿主在装配时从 Service Binding 目录或配置节解析出来，特性用名字取用。读完你能给自己的特性声明一个必需绑定，并知道缺了它应用为什么在启动期就失败。前置知识是 [配置模型与选项](/configuration/options)与[特性与清单](/startup/features)。

## 为什么需要

同一个能力包要在开发机、Compose、Kubernetes 里运行，连接信息却各不相同：本地是 `localhost:6379`，集群里是某个 Service 的地址加一份 Secret 里的口令。如果特性直接读某个固定的配置键，每换一个环境就要改配置的形状；如果连接信息缺失，往往要等到第一次真正连接时才发现。

框架把「我需要一个什么类型的资源」与「这个资源在哪」分开：特性在清单的 `Bindings` 里按名字声明需要的资源与类型，宿主在 `AddXiHan` 时从环境里解析出 `BindingValue`，缺失的必需绑定立刻让启动失败。部署工具 `xihan pack` 按同样的名字生成 Secret 与环境变量，代码一行不改。

## 概念

| 概念 | 类型 | 说明 |
| --- | --- | --- |
| 绑定声明 | `Binding(name, type)` | 写在清单的 `Bindings` 里；`type` 是资源类型，如 `connection:relational`、`cache:distributed`；缺省必需，可选的用初始化器写 `Required = false` |
| 绑定值 | `BindingValue { Name, Type, Value, Properties }` | 宿主解析出的结果：名字、类型、主值（连接字符串或地址）与其余属性，四项都是 `required` 的初始化成员；`Property(key)` 取单个属性 |
| 解析器 | `BindingResolver.Resolve(configuration, serviceBindingRoot)` | 内核按固定顺序收集绑定 |
| 取用 | `FeatureContext.GetBinding(name)` / `FindBinding(name)` | 在 `Configure` 里取；前者缺失时抛出，后者返回 `Maybe<BindingValue>` |
| 来源一 | `SERVICE_BINDING_ROOT` 目录 | Service Binding 规范的目录布局，Kubernetes 里由 Secret 挂载 |
| 来源二 | 配置节 `XiHan:Bindings:<name>` | 本地开发、Compose 与 Aspire 用环境变量注入 |

### 绑定的来源与优先级

`BindingResolver.Resolve` 按下面的顺序收集：

1. 环境变量 `SERVICE_BINDING_ROOT` 指向的目录。不以 `.` 开头的每个子目录是一个绑定，目录名是绑定名；以 `.` 开头的子目录不是绑定，不读、不计，点了名也不读。目录里每个文件是一个属性，文件名是属性名、内容修剪首尾空白后是值；`type` 文件是类型，`uri` 文件既是主值也保留为属性；以 `.` 开头的文件跳过，子目录不是属性、也不读。目录项可以是符号链接：名字取目录项自己的，内容、大小与条目数按链接最终指向的计。`AppBuilder.UseServiceBindingRoot(directory)` 可以在代码里改用别的目录。
2. 配置节 `XiHan:Bindings:<name>`。节本身的标量值或子键 `value` 是主值，子键 `type` 是类型，其余子键是属性。绑定名与 `type`、`value` 两个子键按字面比，区分大小写：写成 `XiHan:Bindings:Db:Value` 读不到绑定 `db` 的主值，得到的是另一个没有主值的绑定 `Db`，`Value` 只是它的一个属性。

同名时配置整体覆盖目录来源，不逐属性合并。

目录来源的文件有大小上限：一个文件最多 524288 字节（512 KiB，`BindingResolver.MaxFileBytes`），一个绑定目录里读的文件（`type`、`uri` 与其余属性文件，以 `.` 开头的不读也不计）合计最多 1048576 字节（1 MiB，`BindingResolver.MaxBindingBytes`，与 Kubernetes 一个 Secret 的上限相同），都按文件的原始字节计。读的时候到上限就停，不先把整个文件读进内存；超过即装配时启动失败（`KERNEL_HOSTING_005`，消息只带绑定名、文件名与上限，不带内容），把不属于绑定的文件（日志、备份、证书归档）挪出绑定目录。

条目数同样有上限：一个绑定目录里读的文件（以 `.` 开头的不计）最多 128 个（`BindingResolver.MaxBindingFiles`），根目录下的绑定（以 `.` 开头的子目录不计）最多 128 个（`BindingResolver.MaxBindings`）。先数、后读：多出一项时这一层一个文件也不读，装配时启动失败（`KERNEL_HOSTING_005`，消息为 `Binding '<绑定名>' has more than 128 files.` 或 `The service binding root contains more than 128 bindings.`，不带路径与文件名）。`SERVICE_BINDING_ROOT` 指错到日志目录、家目录这类大目录树时就是这样失败的：改正根目录，或把不属于绑定的文件与目录挪走。配置来源不受这两道上限。目录布局长这样：

```text
/bindings/
└─ ledger/
   ├─ type        # connection:relational
   ├─ uri         # Host=db;Database=ledger;Username=app;Password=...
   └─ password    # 可选的单独属性
```

Kubernetes 的两种挂法都读得出卷里当前的内容。把 projected volume 直接挂到根目录时，根目录下是 `..data`（指向带时间戳的目录的符号链接）、带时间戳的 `..<时间>` 目录，以及每个绑定名一个、指向 `..data/<绑定名>` 的符号链接：前两个以 `.` 开头，不是绑定，也不占 128 个绑定的名额。把 Secret 卷挂到一个绑定目录时，目录里的 `..data` 与带时间戳的目录是子目录、不是属性，每个属性文件是指向 `..data/<键>` 的符号链接。卷更新时 Kubernetes 换掉 `..data` 的指向，下一次解析读到的就是新内容。

同一份信息用配置节表达：

```json
{
  "XiHan": {
    "Bindings": {
      "cache": "localhost:6379",
      "ledger": {
        "type": "connection:relational",
        "value": "Host=localhost;Database=ledger;Username=dev;Password=dev-password"
      }
    }
  }
}
```

环境变量写法是 `XiHan__Bindings__ledger__value`、`XiHan__Bindings__ledger__type`，Compose 与 Aspire 的生成产物就是这样注入的。绑定名与子键照清单里的大小写书写，不要全部大写。

### 下发给插件的绑定

进程外与 Wasm 插件不在宿主进程的容器里，它们的绑定由插件宿主随 `Configure` 请求下发，来源与上面相同：先读 Service Binding 目录，再读 `XiHan:Bindings`，同名时配置整体覆盖目录。应用用 `UseServiceBindingRoot` 指定了根目录时，插件宿主用同一个（特性经 `FeatureContext.ServiceBindingRoot` 取到）。与特性的绑定有三处不同：

- 只下发插件清单 `bindings` 里声明、且运营方授予了 `binding:<绑定名>` 的绑定，缺授予的插件在发现时以 `PLUGINS_DENIED_001` 拒绝；别的绑定不读、不下发。
- 每次配置插件时解析一遍，不用宿主启动时的那一份：插件重启、运行期加载时拿到的是那一刻的值。
- 下发的是名字、类型、主值与全部属性：`type` 总是清单声明的类型（来源声明了别的类型即配置失败，见下文）；属性与宿主里的特性读到的 `BindingValue.Properties` 是同一份，属性名区分大小写、原样下发，值为空的属性照发。只给了属性、没有主值的绑定（只挂了 `host`、`password` 这类文件、没有 `uri`）照常下发、主值为空。可选的绑定两种来源都没给时不下发；只给了类型、没有主值也没有属性时不下发，记一条 Warning。必需的绑定这样即配置失败。

插件清单的 `bindings[].required` 缺省为 `true`，与特性的清单是同一个字段：部署里可以没有的绑定在清单里写 `"required": false`，插件自己处理没有它的情形。

属性随绑定一起受 `binding:<绑定名>` 约束，不单独授权；属性值与主值一样不进宿主的日志与错误。下发之前宿主按清单的顺序逐个绑定核对，一个绑定里依次是有没有、类型、属性的个数、属性名与大小，报第一处不合的；全部合格之后再核对整个请求：

| 核对 | 不合的情形 | `reason` |
| --- | --- | --- |
| 有没有 | 必需的绑定两种来源都没给，或只给了类型、没有主值也没有属性 | `binding '<绑定名>' is required but not provided` |
| 类型 | 来源声明了类型（`type` 文件或 `XiHan:Bindings:<名字>:type`）而与清单不同，按序数比较，只差大小写也算；必需与可选的都核对 | `binding '<绑定名>' is of type '<来源的类型>' but the manifest declares '<清单的类型>'` |
| 属性个数 | 一个绑定多于 64 个 | `binding '<绑定名>' has more than 64 properties` |
| 属性名 | 不是 1 到 253 个 ASCII 字母、数字、`.`、`_`、`-`，或以 `.` 开头 | `binding '<绑定名>' has a property name that is not valid` |
| 大小 | 一个绑定的属性名与属性值按 UTF-8 编码后合计超过 262144 字节（256 KiB），主值不计入 | `the properties of binding '<绑定名>' exceed 262144 bytes` |
| 整个请求 | `Configure` 请求（配置节、授予的权限、全部绑定与工作目录）按 protobuf 二进制编码超过 2097152 字节（2 MiB）；没有声明绑定的插件同样核对，进程外与 Wasm 都按这个大小算 | `the configure request exceeds 2097152 bytes` |

不合时宿主不少发、不截断，也不发 `Configure`：这个插件配置失败（`PLUGINS_UNEXPECTED_001` + `plugins.process.failed`，`phase` 为 `configure`，`reason` 是表里的固定文本，只带绑定名、类型名与上限，不带主值、属性名与属性值），宿主记一条 Error（`The configure request of plugin <清单 ID> is not sent: <reason>; the plugin is not configured.`），插件进入 Failed 并按重启策略重试。补上或改正绑定、把不该给插件的文件挪出绑定目录，或把属性名改到上面的字符集里，插件下一次重试时自己起来。属性与请求的上限只管下发给插件的那一份，宿主进程里的特性照常读到全部属性。

清单声明的绑定在目录里读不出来（文件在、权限不对）或超过上一节的大小与文件数上限时，插件这一次配置失败，同样进入 Failed 并按重启策略重试：读不出时 `reason` 只写异常类型，单个文件超限为 `a file of binding '<绑定名>' exceeds 524288 bytes`，合计超限为 `the files of binding '<绑定名>' exceed 1048576 bytes`，文件多于 128 个为 `binding '<绑定名>' has more than 128 files`（都不带文件名）；根目录不存在不算失败。插件宿主只读清单声明的那几个绑定目录，根目录下有多少个绑定与它无关，不数。清单声明的绑定按清单的顺序读，报第一个读不出或超限的。见[进程外插件](/plugins/out-of-process)与 [Wasm 插件](/plugins/wasm)。

进程内插件拿的是整个宿主配置，宿主不下发绑定，插件自己经 `BindingResolver.Resolve(context.Configuration, context.ServiceBindingRoot)` 读。根目录在构造宿主时给：`new InProcessPluginHost(configuration, loggerFactory, serviceBindingRoot)`，应用用 `UseServiceBindingRoot` 指定了根目录时传同一个值，插件经 `PluginContext.ServiceBindingRoot` 拿到它；不传时宿主与插件都取环境变量 `SERVICE_BINDING_ROOT`。宿主在加载插件代码之前按这个根目录与宿主配置解析，按上表的有没有与类型两条核对清单声明的绑定，不合即加载失败（`PLUGINS_UNEXPECTED_001` + `plugins.plugin.load_failed`，`reason` 相同），插件程序集不加载；目录超过上一节的大小或条目数上限时同样加载失败，`reason` 是内核的消息；属性与请求的上限不核对。见 [Plugins.InProcess](/packages/plugins-inprocess)。

插件一侧由各语言 SDK 把绑定交给作者：属性是一张只读的表，没有属性时是空表，只给了属性的绑定主值为空串；绑定类型的调试输出只带绑定名、类型与属性名。较早的 SDK 构建的插件不认这个字段，照旧读到名字、类型与主值，只是会多收到主值为空的绑定。各语言的写法见 [Go](/sdks/go)、[TypeScript](/sdks/typescript)、[Rust](/sdks/rust)、[Java](/sdks/java)、[Python](/sdks/python) 与 [PHP](/sdks/php)。

## 最小示例

我们写一个需要关系数据库连接的记账特性，声明一个必需绑定 `ledger`，并在 `Configure` 里取出主值：

```csharp
// BillingFeature.cs
using Microsoft.Extensions.DependencyInjection;
using XiHan.Framework.Kernel;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace Billing;

public sealed class BillingFeature : IFeature
{
    public Manifest Manifest { get; } = new()
    {
        Id = "xihan.shop.billing",
        Package = "Billing",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        // 必需绑定：没给或只给了类型时 AddXiHan 抛出 KERNEL_HOSTING_001，启动前就暴露
        Bindings = [new Binding("ledger", "connection:relational")],
        Entry = typeof(BillingFeature).FullName,
    };

    public void Configure(FeatureContext context)
    {
        var ledger = context.GetBinding("ledger");
        // 必需绑定一定存在，但主值可能为空：空值是部署错误，抛出让启动失败
        var connectionString = ledger.Value ?? throw new InvalidOperationException("Binding 'ledger' has no value.");
        context.Services.AddSingleton(new LedgerConnection(connectionString));
    }
}

public sealed record LedgerConnection(string ConnectionString);
```

`Program.cs` 沿用[第一个应用](/getting-started/first-app)的骨架，装配这个特性并加一个端点：

```csharp
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<BillingFeature>());

// app.UseXiHan() 之后
app.MapGet("/ledger", (LedgerConnection ledger) => ledger.ConnectionString.Length);
```

先不给任何绑定直接 `dotnet run`：`AddXiHan` 抛出错误码为 `KERNEL_HOSTING_001` 的 `FrameworkException`，消息是 `Feature 'xihan.shop.billing' is not configured: binding 'ledger' is required but not provided.`，进程退出。再把上一节的 `XiHan:Bindings:ledger` 写进 `appsettings.json`，或设置环境变量 `XiHan__Bindings__ledger__value=...` 与 `XiHan__Bindings__ledger__type=connection:relational`，应用正常启动，`curl localhost:5000/ledger` 返回连接字符串的长度。换成 Kubernetes 时把同样的内容做成 Secret 挂到 `SERVICE_BINDING_ROOT`，代码不变。

## 必需绑定与启动期失败

清单声明的每个绑定都在 `AddXiHan` 构建时按清单的顺序核对，报第一处不合的；规则与插件宿主是同一处（内核 `Kernel.Hosting` 的 `BindingRules`）：

| 情形 | 结果 |
| --- | --- |
| 必需绑定（`required` 缺省 `true`）两种来源都没给，或只给了类型、没有主值也没有属性 | 抛出 `KERNEL_HOSTING_001`，应用不启动 |
| 来源声明了类型且与清单不一致（按序数比较，必需与可选的都核对） | 同上 |
| 来源没有声明类型（空字符串） | 不比对类型，按名字匹配 |
| 可选绑定（`{ Required = false }`）没给或只给了类型 | 照常装配，特性用 `FindBinding` 自行处理 |
| `GetBinding` 取一个不存在的绑定 | 抛出 `KERNEL_HOSTING_001` |
| 绑定目录超过大小上限（一个文件 512 KiB、一个绑定合计 1 MiB）或条目数上限（一个绑定 128 个文件、根目录下 128 个绑定） | 抛出 `KERNEL_HOSTING_005`，应用不启动 |

核对不合时的消息是 `Feature '<清单 ID>' is not configured: <reason>.`，`<reason>` 与插件宿主写的相同（见上文[下发给插件的绑定](#下发给插件的绑定)表里有没有与类型两行），只带绑定名与类型，不带主值与属性；被顶替而不装配的特性不核对。框架内置包声明的绑定都是可选的，类型照样核对：把 `db` 挂成一个 `queue` 类型的绑定，应用在启动时失败，而不是拿着 AMQP 的地址去连关系库。插件宿主在下发或加载插件之前执行同一套规则，对同一份清单得出同一个结论。

这正是「配置与编程错误抛异常、可恢复失败返回 `Result`」的分界：缺绑定是部署错误，不应该带着它运行，见[结果与错误处理](/web/errors)。

## 内置包声明的绑定

框架内置包声明的绑定都是可选的。绑定来自部署环境，它给出的连接信息（主值）与凭据（属性）逐项覆盖 Provider 配置节里的同类键：绑定里有这一项就用绑定的，没有就读配置节的；前缀、超时、分组等不是连接与凭据的键照读配置节。主值或属性为空白时不算给出；凭据属性与主值互不依赖，绑定只给凭据、连接来自配置节也照样覆盖凭据；两者都没有连接时装配失败。属性名区分大小写，按表里的写；凭据不进日志与错误消息。

| 绑定名 | 类型 | 声明它的包 | 用法 |
| --- | --- | --- | --- |
| `db` | `connection:relational` | `Persistence.EntityFrameworkCore`、`Persistence.SqlSugar` | 交给应用传入特性的配置委托，主值是连接字符串 |
| `cache` | `cache:distributed` | `Caching.Redis` | 主值覆盖 `XiHan:Caching:Redis:Configuration`；属性 `username`、`password` 覆盖连接字符串里的 `user`、`password` |
| `queue` | `queue` | `Messaging.RabbitMQ` | 主值覆盖 `XiHan:Messaging:RabbitMQ:Configuration`；属性 `username`、`password` 覆盖 AMQP URI 里的用户名与密码 |
| `queue` | `cache:distributed` | `Messaging.Redis` | 主值覆盖 `XiHan:Messaging:Redis:Configuration`；属性 `username`、`password` 覆盖连接字符串里的 `user`、`password`。绑定名与 RabbitMQ 相同（消息队列这个角色），类型是 Redis 服务；不读缓存的 `cache`，要共用一台 Redis 时让两个绑定指向它 |
| `stream` | `stream` | `Messaging.Kafka` | 主值覆盖 `XiHan:Messaging:Kafka:BootstrapServers`；属性 `security.protocol`、`sasl.mechanism`、`sasl.username`、`sasl.password` 覆盖 `Properties` 里的同名键（只认这四项） |
| `nacos` | `configuration:remote` | `Configuration.Nacos` | 主值覆盖 `XiHan:Configuration:Nacos:ServerAddresses`；属性 `username`、`password` 覆盖 `Username`、`Password` |
| `objectstorage` | `blob` | `ObjectStorage.S3` | 主值（服务端点）与属性 `region`、`accessKey`、`secretKey`、`sessionToken`、`forcePathStyle` 覆盖 `XiHan:ObjectStorage:S3` 里的同名设置；主值不能带用户信息（`user:pass@`），带了即启动失败，凭据写属性 `accessKey`、`secretKey`；`sessionToken` 是临时凭据的会话令牌，覆盖之后仍要与两项密钥同时给出 |

## 与部署产物的关系

表里六种绑定类型 `xihan pack` 都认识：应用清单 `xihan.app.json` 按上表的绑定名声明后，Compose 与 Aspire 产物会起对应的依赖服务并把主值注入 `XiHan__Bindings__<绑定名>__value`（`blob` 的依赖服务是 S3 兼容的 RustFS，另注入 `accessKey`、`secretKey` 两个属性，其余服务的凭据在主值里或没开认证）；Kustomize 产物不在集群里起依赖服务，只按 Service Binding 规范挂 Secret，示例 Secret 以注释列出各类型可单独给出的凭据键。生成规则见[应用清单与 xihan pack](/deployment/pack)。

## 常见问题

### 绑定的主值会出现在日志里吗

不会。`BindingValue.ToString()` 把 `Value` 打印成 `***`，属性只列名字（`Properties = [password, username]`）、不打印值，Provider 的选项记录也遮蔽连接字符串与口令，见 [远程配置与密钥](/configuration/remote)的「选项打印时遮蔽凭据」。

### 绑定的值能热更新吗

特性的绑定不能：它在 `AddXiHan` 时解析一次并交给特性装配，`FeatureContext.Bindings` 此后不变，改了要重启，轮换凭据时用滚动重启。下发给插件的绑定在每次配置插件时重新解析，所以改了挂载的 Secret 或配置之后，重启那个插件（或卸载再加载）它就拿到新值；这时应用自己的那一份仍是启动时的，两者可以不同。

## 相关包与下一步

- [Kernel](/packages/kernel)：`BindingResolver`、`BindingValue`、`FeatureContext.GetBinding` 与 `ServiceBindingRoot`
- [Primitives](/packages/primitives)：清单模型 `Manifest` 与 `Binding`
- [应用清单与 xihan pack](/deployment/pack)：按绑定名生成 Secret 与环境变量
- 下一页：[远程配置与密钥](/configuration/remote)
