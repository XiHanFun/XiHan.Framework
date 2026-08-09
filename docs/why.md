# 为什么选择曦寒

功能清单在 [模块总览](./packages/)，这一页不重复。它只回答三个问题：

1. 它究竟替我解决什么问题？
2. 我这个项目适合用它吗？
3. 它和别的做法有什么不同？

选型是一次要背很多年的决定，所以这一页会把**代价**和**亮点**写在一起。

## 30 秒结论

::: tip 适合这样的项目
- **.NET 10 起新项目**，前后端分离，预期要维护三年以上
- **中后台 / 业务系统 / SaaS**：接口多、权限细、要多租户、要审计
- **团队 1–20 人**：希望有统一约定，但不想被框架绑死到改不动
- **面向国内交付**：需要中文文档，以及企业微信 / 钉钉 / 飞书、Gitee / QQ 登录、国内对象存储与短信这类本土集成
:::

::: warning 这些情况建议先别选
- **被锁在 .NET 6 / 8 LTS**：框架只发 `net10.0`，没有向下兼容目标
- **团队已深度绑定 EF Core**：数据访问基于 SqlSugar，换 ORM 等于换掉数据层约定
- **需要商业 SLA、7×24 支持、或按简历直接招到熟手**：曦寒是社区项目，没有商业支持团队
- **面向 C 端的超高并发交易系统**：框架为企业应用的复杂度设计，不是为极限吞吐设计
:::

## 一、它替你解决的四件事

### 1. 装配不再靠人肉维护

传统项目里，`Program.cs` 会长成这样——注册顺序、依赖关系、中间件先后，全靠人记：

```csharp
builder.Services.AddAuthentication(/* ... */);
builder.Services.AddAuthorization(/* ... */);
builder.Services.AddSqlSugar(/* ... */);
builder.Services.AddStackExchangeRedisCache(/* ... */);
// ……几十行，新人不敢挪动任何一行

app.UseAuthentication();
app.UseAuthorization();
// ……中间件顺序再来一遍，接错一个位置就是线上事故
```

曦寒把每块能力封进一个**模块类**，你只声明「我要用谁」：

```csharp
[DependsOn(
    typeof(XiHanWebApiModule),      // 动态 API + 完整中间件管道
    typeof(XiHanDataModule),        // SqlSugar 数据访问
    typeof(XiHanCachingModule)      // 分布式缓存与分布式锁
)]
public class MyAppModule : XiHanModule;
```

```csharp
var builder = WebApplication.CreateBuilder(args);
await builder.AddApplicationAsync<MyAppModule>();   // 自动拓扑排序装配整棵依赖树
var app = builder.Build();
await app.InitializeApplicationAsync();             // 触发各模块的初始化钩子
await app.RunAsync();
```

**增删一块能力 = 加减一行 `[DependsOn]`。** 服务注册、中间件接入、后台任务启动由模块自己在生命周期钩子里负责，不再堆在 `Program.cs` 里。

> 机制细节见 [模块系统](./guide/modularity) 与 [模块生命周期](./guide/lifecycle)。

### 2. 写完服务就有接口

不写 Controller，不写路由特性。应用服务打上 `[DynamicApi]`，框架按约定生成 REST 路由：

```csharp
[DynamicApi]
public class HelloAppService : ApplicationServiceBase
{
    public string GetGreeting(string name) => $"你好，{name}！";
}
```

`ApplicationServiceBase` 同时标记了 `IApplicationService` 与 `ITransientDependency`，所以它**自动进 DI 容器**，也不需要 `services.AddTransient`。这意味着新增一个业务接口的成本是一个方法，而不是「实体 → DTO → 服务 → 接口 → Controller → 路由 → 注册」七个文件。

> 路由推导规则与自定义方式见 [动态 API](./guide/dynamic-api)。

### 3. 引一个能力，只拖它自己的依赖

框架被拆成 **66 个可独立引用的 NuGet 包**，第三方依赖挂在各自的包上，而不是集中在一个大包里：

- 只用工具库？`XiHan.Framework.Utils` **零第三方依赖**
- 不用 Elasticsearch，`Elastic.Clients.Elasticsearch` 就不会出现在你的输出目录
- 不用 Kafka / RabbitMQ / Telegram / 阿里云 OSS，同理

整个框架合计引用 60 个外部 NuGet 包，其中 26 个是 Microsoft 官方发布的 .NET 扩展包，真正的第三方库是 34 个——而且**没有任何一个应用会同时拿到它们**。「优先 .NET 原生能力，仅在必要时引入第三方库」不是口号，是可以用 `dotnet list package` 验证的结果。

> 逐包的依赖与配置项见 [模块总览](./packages/)。

### 4. 底座之上还有组件与应用

大多数后端框架到「框架」为止，之后前端选型、后台系统怎么搭，你自己想办法。曦寒是三层同源的一套东西：

| 层 | 项目 | 你能拿它做什么 |
| --- | --- | --- |
| 后端基座 | **XiHan.Framework** | 就是本文档，模块化后端能力 |
| 组件层 | [XiHan.UI](https://ui.docs.xihanfun.com/) | 框架无关的设计系统运行时，Vue 3 与 Web Components 适配器 |
| 基础应用 | [XiHan.BasicApp](https://basicapp.docs.xihanfun.com/) | 用前两者搭出来的企业级中后台，可直接投产、也可当范例读 |

三者**可以单独用**：只引 Framework 的某几个包完全可行，BasicApp 也不是必须的。但当你需要「后端约定怎么落到前端页面」的完整答案时，它是现成的。

> 想看框架能力在真实业务里怎么落地，最快的路径是读 [XiHan.BasicApp](https://basicapp.docs.xihanfun.com/) 的源码。

## 二、后端框架的四条路线

同类方案不是一个平面上的竞品，而是四条不同的路线，各自解决不同阶段的问题。下面只描述路线特征，不做优劣评判——**很多时候「不选曦寒」才是对的**。

| 路线 | 代表 | 你得到什么 | 你付出什么 | 什么时候选它 |
| --- | --- | --- | --- | --- |
| **A. 大型模块化企业框架** | ABP Framework、MASA Framework | 完整的 DDD / 模块化 / 多租户体系，生态与文档积累深厚 | 概念栈较厚，团队需要专门的学习投入；能力往往整体引入 | 大团队、长周期、愿意把框架当作长期技术资产 |
| **B. 轻量高效应用框架** | Furion | 上手极快，约定强，写业务基本没有仪式感 | 抽象层较薄，复杂权限 / 多租户 / 大型分层需要自己补 | 中小项目、追求交付速度、系统边界清晰 |
| **C. 原生 + 自选组件库拼装** | ASP.NET Core 原生 + EF Core / FreeSql / MediatR 等独立库 | 每一块都能挑到当下最合适的库，没有任何多余抽象 | 装配、约定、跨切面能力（事务、审计、租户）全靠团队自己收口 | 系统形态特殊，或团队有很强的架构掌控欲 |
| **D. 团队自研内部框架** | 各公司内部沉淀的基础库 | 完全贴合自家业务与规范 | 框架本身要有人长期维护，人员流动即风险 | 组织规模足够大，能养得起一个基础架构团队 |

**曦寒站在哪里：** 它的形态属于路线 A——模块化、分层、依赖倒置、跨切面能力由框架统一收口。区别在两处：

- **粒度更细。** 66 个包可以单独引用，用哪块装哪块，不必整体引入一个大而全的体系。
- **概念负担更接近路线 B。** 核心只有「模块类 + `[DependsOn]` + 生命周期钩子」三个概念，剩下的靠约定，不要求团队先建立一整套方法论才能开工。

代价也很直接：它没有路线 A 那样厚的生态与社区积累，遇到冷门问题时可检索的第三方答案更少。

## 三、逐维度看取舍

下表不是打分表，而是「曦寒做了什么选择，以及这个选择的代价」。选型时真正有用的是右边那一列。

| 维度 | 曦寒的选择 | 这么选的代价 |
| --- | --- | --- |
| **运行时** | 只发 `net10.0`，直接用新版本的语言与运行时能力 | 老项目升不上来就用不了，没有多目标框架兼容包袱 |
| **模块装配** | `[DependsOn]` 声明 + 自动拓扑排序 | 需要理解模块生命周期的钩子顺序，比裸 `AddXxx` 多一层概念 |
| **接口暴露** | 应用服务 + 动态 API，不写 Controller | 路由由约定推导，需要熟悉约定；极端定制场景仍要落回 Controller |
| **ORM** | SqlSugar（PostgreSQL / MySQL / SQLite 等） | 不是 EF Core，团队既有的 EF 经验与生态迁移不过来 |
| **AOP** | Castle DynamicProxy，事务、缓存、审计以拦截器织入 | 拦截器要求方法可代理（虚方法 / 接口），有约定要遵守 |
| **权限模型** | RBAC + ABAC + 数据范围 + 字段级脱敏四个正交维度 | 概念比「角色—菜单」两层模型多，简单系统会觉得重 |
| **多租户** | 字段级隔离内建于数据层，不是可选插件 | 实体要遵守多租户约定，绕开约定写原生 SQL 需要自己保证隔离 |
| **依赖策略** | 优先 .NET 原生，第三方按模块下沉 | 部分能力的功能面不如成熟第三方库丰富 |
| **文档语言** | 中文为母语，[开发指南 38 章 + 包参考 66 页](./guide/modularity) | 英文文档尚不完整，海外协作有语言成本 |
| **许可证** | MIT，全部代码开源，无企业版 / 无功能墙 | 也意味着没有付费支持通道，问题主要靠社区与作者 |
| **质量保障** | 20 余个测试项目覆盖核心模块，含集成测试 | 覆盖率并非全包均衡，边缘模块以实际使用验证为主 |

## 四、用最小成本验证

别信任何一页文档的自述，跑一遍最快：

| 花多久 | 做什么 | 你会验证到什么 |
| --- | --- | --- |
| **30 分钟** | 跟着 [快速上手](./quickstart) 建一个 Web API | 装配方式、动态 API 是不是你喜欢的写法 |
| **半天** | 读 [模块系统](./guide/modularity) → [依赖注入](./guide/dependency-injection) → [工作单元与事务](./guide/uow) 三章 | 核心机制是否经得起你团队的追问 |
| **一天** | 挑一个你真实用得上的模块（[多租户](./guide/multi-tenancy) / [授权](./guide/authorization) / [缓存](./guide/caching)），读包页并接进 demo | 能力深度与配置面是否够用 |

## 下一步

- [快速上手](./quickstart)：5 分钟跑起第一个接口
- [框架概述](./overview)：设计原则、分层架构、技术栈
- [模块总览](./packages/)：66 个包逐一查阅
- [为什么选择曦寒基础应用](https://basicapp.docs.xihanfun.com/why)：如果你要的是一套现成的中后台
