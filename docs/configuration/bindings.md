# 资源绑定

这一页讲特性怎样拿到数据库、Redis、消息代理这类外部资源的连接信息：在清单里声明一个绑定，由宿主在装配时从 Service Binding 目录或配置节解析出来，特性用名字取用。读完你能给自己的特性声明一个必需绑定，并知道缺了它应用为什么在启动期就失败。前置知识是 [配置模型与选项](/configuration/options)与[特性与清单](/startup/features)。

## 为什么需要

同一个能力包要在开发机、Compose、Kubernetes 里运行，连接信息却各不相同：本地是 `localhost:6379`，集群里是某个 Service 的地址加一份 Secret 里的口令。如果特性直接读某个固定的配置键，每换一个环境就要改配置的形状；如果连接信息缺失，往往要等到第一次真正连接时才发现。

框架把「我需要一个什么类型的资源」与「这个资源在哪」分开：特性在清单的 `Bindings` 里按名字声明需要的资源与类型，宿主在 `AddXiHan` 时从环境里解析出 `BindingValue`，缺失的必需绑定立刻让启动失败。部署工具 `xihan pack` 按同样的名字生成 Secret 与环境变量，代码一行不改。

## 概念

| 概念 | 类型 | 说明 |
| --- | --- | --- |
| 绑定声明 | `Binding(name, type, required = true)` | 写在清单的 `Bindings` 里；`type` 是资源类型，如 `connection:relational`、`cache:distributed` |
| 绑定值 | `BindingValue(Name, Type, Value, Properties)` | 宿主解析出的结果：名字、类型、主值（连接字符串或地址）与其余属性；`Property(key)` 取单个属性 |
| 解析器 | `BindingResolver.Resolve(configuration, serviceBindingRoot)` | 内核按固定顺序收集绑定 |
| 取用 | `FeatureContext.GetBinding(name)` / `TryGetBinding(name)` | 在 `Configure` 里取；前者缺失时抛出，后者返回 `Maybe<BindingValue>` |
| 来源一 | `SERVICE_BINDING_ROOT` 目录 | Service Binding 规范的目录布局，Kubernetes 里由 Secret 挂载 |
| 来源二 | 配置节 `XiHan:Bindings:<name>` | 本地开发、Compose 与 Aspire 用环境变量注入 |

### 绑定的来源与优先级

`BindingResolver.Resolve` 按下面的顺序收集：

1. 环境变量 `SERVICE_BINDING_ROOT` 指向的目录。每个子目录是一个绑定，目录名是绑定名；目录里每个文件是一个属性，文件名是属性名、内容修剪首尾空白后是值；`type` 文件是类型，`uri` 文件既是主值也保留为属性；以 `.` 开头的文件跳过。`AppBuilder.UseServiceBindingRoot(directory)` 可以在代码里改用别的目录。
2. 配置节 `XiHan:Bindings:<name>`。节本身的标量值或子键 `value` 是主值，子键 `type` 是类型，其余子键是属性。

同名时配置整体覆盖目录来源，不逐属性合并。目录布局长这样：

```text
/bindings/
└─ ledger/
   ├─ type        # connection:relational
   ├─ uri         # Host=db;Database=ledger;Username=app;Password=...
   └─ password    # 可选的单独属性
```

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

环境变量写法是 `XiHan__Bindings__ledger__value`、`XiHan__Bindings__ledger__type`，Compose 与 Aspire 的生成产物就是这样注入的。

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
        Id = "shop.billing.ledger",
        Package = "Billing",
        Version = "1.0.0",
        Primitives = 1,
        Stability = Stability.Experimental,
        Kind = ManifestKind.Capability,
        Requires = [new CapabilityRequirement(KernelCapabilities.Hosting, KernelCapabilities.Version)],
        // 必需绑定：缺失时 AddXiHan 抛出 KERNEL_HOSTING_001，启动前就暴露
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

```csharp
// Program.cs
using Billing;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.AddXiHan(xihan => xihan.UseStandardProfile().UseFeature<BillingFeature>());

var app = builder.Build();
app.UseXiHan();
app.MapGet("/ledger", (LedgerConnection ledger) => ledger.ConnectionString.Length);
app.Run();
```

先不给任何绑定直接 `dotnet run`：`AddXiHan` 抛出错误码为 `KERNEL_HOSTING_001` 的 `FrameworkException`，消息指出缺少绑定 `ledger`，进程退出。再把上一节的 `XiHan:Bindings:ledger` 写进 `appsettings.json`，或设置环境变量 `XiHan__Bindings__ledger__value=...` 与 `XiHan__Bindings__ledger__type=connection:relational`，应用正常启动，`curl localhost:5000/ledger` 返回连接字符串的长度。换成 Kubernetes 时把同样的内容做成 Secret 挂到 `SERVICE_BINDING_ROOT`，代码不变。

## 必需绑定与启动期失败

清单里 `required: true`（缺省）的绑定在 `AddXiHan` 构建时校验：

| 情形 | 结果 |
| --- | --- |
| 必需绑定缺失 | 抛出 `KERNEL_HOSTING_001`，应用不启动 |
| 来源声明了类型且与清单不一致 | 同上 |
| 来源没有声明类型（空字符串） | 不比对类型，按名字匹配 |
| 可选绑定（`required: false`）缺失 | 不报错，特性用 `TryGetBinding` 自行处理 |
| `GetBinding` 取一个不存在的绑定 | 抛出 `KERNEL_HOSTING_001` |

这正是「配置与编程错误抛异常、可恢复失败返回 `Result`」的分界：缺绑定是部署错误，不应该带着它运行，见[结果与错误处理](/web/errors)。

## 内置包声明的绑定

框架内置包声明的绑定都是可选的。绑定来自部署环境，它给出的连接信息（主值）与凭据（属性）逐项覆盖 Provider 配置节里的同类键：绑定里有这一项就用绑定的，没有就读配置节的；前缀、超时、分组等不是连接与凭据的键照读配置节。主值或属性为空白时不算给出；凭据属性与主值互不依赖，绑定只给凭据、连接来自配置节也照样覆盖凭据；两者都没有连接时装配失败。属性名区分大小写，按表里的写；凭据不进日志与错误消息。

| 绑定名 | 类型 | 声明它的包 | 用法 |
| --- | --- | --- | --- |
| `db` | `connection:relational` | `Persistence.EntityFrameworkCore`、`Persistence.SqlSugar` | 交给应用传入特性的配置委托，主值是连接字符串 |
| `cache` | `cache:distributed` | `Caching.Redis` | 主值覆盖 `XiHan:Caching:Redis:Configuration`；属性 `username`、`password` 覆盖连接字符串里的 `user`、`password` |
| `queue` | `queue` | `Messaging.RabbitMQ` | 主值覆盖 `XiHan:Messaging:RabbitMQ:Configuration`；属性 `username`、`password` 覆盖 AMQP URI 里的用户名与密码 |
| `stream` | `stream` | `Messaging.Kafka` | 主值覆盖 `XiHan:Messaging:Kafka:BootstrapServers`；属性 `security.protocol`、`sasl.mechanism`、`sasl.username`、`sasl.password` 覆盖 `Properties` 里的同名键（只认这四项） |
| `nacos` | `configuration:remote` | `Configuration.Nacos` | 主值覆盖 `XiHan:Configuration:Nacos:ServerAddresses`；属性 `username`、`password` 覆盖 `Username`、`Password` |
| `objectstorage` | `blob` | `ObjectStorage.S3` | 主值（服务端点）与属性 `region`、`accessKey`、`secretKey`、`forcePathStyle` 覆盖 `XiHan:ObjectStorage:S3` 里的同名设置 |

## 与部署产物的关系

表里六种绑定类型 `xihan pack` 都认识：应用清单 `xihan.app.json` 按上表的绑定名声明后，Compose 与 Aspire 产物会起对应的依赖服务并把主值注入 `XiHan__Bindings__<绑定名>__value`（`blob` 的依赖服务是 S3 兼容的 RustFS，另注入 `accessKey`、`secretKey` 两个属性，其余服务的凭据在主值里或没开认证）；Kustomize 产物不在集群里起依赖服务，只按 Service Binding 规范挂 Secret，示例 Secret 以注释列出各类型可单独给出的凭据键。生成规则见[应用清单与 xihan pack](/deployment/pack)。

## 常见问题

### 绑定与配置节有什么区别，什么时候用哪个

配置节放行为参数（超时、前缀、容量），绑定放「连到哪、用什么凭据」。绑定由部署环境注入，Provider 用它覆盖配置节里的连接与凭据键，其余键照读配置节。本地开发没有 Service Binding 目录时，用 `XiHan:Bindings` 配置节写同样的内容。

### 必需绑定在生产里一定要用 Service Binding 目录吗

不一定。目录与配置节两种来源等价，Kustomize 产物用目录，Compose 与 Aspire 用环境变量注入配置节；只要 `BindingResolver` 能解析到，特性拿到的 `BindingValue` 相同。

### 同一个绑定在目录与配置节里都有，取哪个

配置节整体覆盖目录来源，连属性也一起换掉，不逐项合并。

### 绑定的主值会出现在日志里吗

不会。`BindingValue.ToString()` 把 `Value` 打印成 `***`，Provider 的选项记录也遮蔽连接字符串与口令，见 [远程配置与密钥](/configuration/remote)的「选项打印时遮蔽凭据」。

### 绑定的值能热更新吗

不能。绑定在 `AddXiHan` 时解析一次并交给特性装配，改了要重启。轮换凭据时用滚动重启。

## 相关包与下一步

- [Kernel](/packages/kernel)：`BindingResolver`、`BindingValue`、`FeatureContext.GetBinding`
- [Primitives](/packages/primitives)：清单模型 `Manifest` 与 `Binding`
- [应用清单与 xihan pack](/deployment/pack)：按绑定名生成 Secret 与环境变量
- 下一页：[远程配置与密钥](/configuration/remote)
