# 常见问题

框架层面的高频故障速查。这些坑的共同点是**没有异常、没有日志、静默失效**——知道去哪儿看就能秒解，不知道就能耗一天。每条都对照框架源码核实。

[[toc]]

---

## 模块与装配

### 我的模块写好了，但服务一个都没注册

`ConfigureServices` 只会被框架在**依赖图内**调用。检查两点：

1. 你的模块有没有出现在启动模块（或其某个上游模块）的 `[DependsOn]` 里。不在依赖图里的模块类，框架根本不认识它。
2. 有没有在宿主里额外手写 `services.AddMyModule(...)`。模块自己的 `ConfigureServices` 已经会跑一次，宿主再调一次就是**重复注册**。宿主只用 `[DependsOn]` 挂模块，不重复接线。

### `[DependsOn]` 要把间接依赖也列上吗

不用。**只写直接依赖**——间接依赖由被依赖模块自己声明，框架按依赖图拓扑排序装配，重复列举没有收益，只会让依赖关系失真。

### 启动时抛出动态 API 装配异常

这是**故意的**。`DynamicApiOptions.ThrowOnGenerationFailure` 默认 `true`：任一应用服务生成控制器失败就中断装配并抛聚合异常，异常信息里有具体是哪个服务、哪个方法。

设成 `false` 会改为只记日志并跳过——该服务的全部端点就从路由表里静默消失，前端拿到 404 却查不出所以然。**不建议**关掉。

### 代码该写在哪个生命周期钩子里

| 你要做的事 | 钩子 |
| --- | --- |
| 注册服务、绑定选项 | `ConfigureServices` |
| 挂拦截器注册器、预设其他模块要读的选项 | `PreConfigureServices` |
| 所有模块注册完后做最终覆盖 | `PostConfigureServices` |
| 接中间件、映射端点 | `OnApplicationInitialization` |
| 要插在管道最前面（如 Webhook 校验） | `OnPreApplicationInitialization` |
| 一切就绪后再做（如把库里的任务同步进调度器） | `OnPostApplicationInitialization` |
| 释放资源 | `OnApplicationShutdown`（**逆序**执行，与其余六个钩子相反） |

拿 `IApplicationBuilder` 的 `context.GetApplicationBuilder()` 等扩展方法来自 `XiHan.Framework.Web.Core`，纯类库模块里没有。

---

## 依赖注入

### 运行期抛「无法解析服务」

最常见的是**领域服务**。领域服务接口通常不带 `ITransientDependency` / `IScopedDependency` / `ISingletonDependency` 标记接口，约定注册扫不到，必须手写：

```csharp
services.AddScoped<IInvoiceDomainService, InvoiceDomainService>();
```

对比：应用服务（`ApplicationServiceBase` 已实现 `ITransientDependency`）与仓储（基类带 `IScopedDependency`）都会被自动注册，不用手写。

### 我实现了同一个接口，但用的还是框架的默认实现

框架用 **`TryAdd`** 注册默认实现，且**先于**你的模块执行。你再 `TryAdd`（包括很多 `AddXxx` 扩展方法内部就是 `TryAdd`）会被**静默忽略**。

覆盖必须用 `Replace`：

```csharp
services.Replace(ServiceDescriptor.Singleton<IAiProviderConfigStore, DbAiProviderConfigStore>());
```

或走特性：`[Dependency(ServiceLifetime.Scoped, ReplaceServices = true)]`。

替换时**生命周期要与被替换者一致**，否则解析时抛作用域校验异常（Singleton 依赖 Scoped 是非法的）。

### 一个实现类实现了多个接口，只有一个能注入

约定注册默认只把实现类暴露为「类名去掉前导 `I` 后同名」的那个接口（`OrderService` → `IOrderService`）。要额外暴露：

```csharp
[ExposeServices(typeof(INotifiable), IncludeDefaults = true)]
public class OrderService : IOrderService, INotifiable, IScopedDependency { }
```

同接口多实现按 key 区分用 `[ExposeKeyedService<IPaymentGateway>("alipay")]`，消费侧 `[FromKeyedServices("alipay")]`。

### 想让某个类完全不被自动注册

类上加 `[DisableConventionalRegistration]`，然后自己手写注册。

---

## AOP 与拦截器

### `[UnitOfWork]` / `[Cacheable]` 标了没生效

**最可能的原因：服务的注册类型不是接口。**

框架的 AOP 走 Castle 的 `CreateInterfaceProxyWithTarget`，`AddCastleDynamicProxy` 只处理 `ServiceType.IsInterface` 为真的服务描述器。所以：

```csharp
services.AddScoped<MyService>();               // ❌ 注册为自身类型 → 不会被代理，特性静默失效
services.AddScoped<IMyService, MyService>();   // ✅ 接口 → 会被代理
```

其次检查：类型有没有被列进 `DynamicProxyIgnoreTypes`。


### 匿名端点调用应用服务时永久挂起

匿名端点（Minimal API、OAuth 回调等）不经过工作单元中间件，而应用服务被 Castle 代理包着，拦截器会急切开事务从而死锁。

解法是**绕开代理**：注入真正的依赖直连，或用 `ProxyHelper.UnProxy(service)` 取真实目标实例再调。

### 怎么加自己的拦截器

写一个 `IXiHanInterceptor` 实现 + 一个决定「拦谁」的注册器，在 `PreConfigureServices` 里 `services.OnRegistered(注册器)` 挂上。完整示例见 [扩展与二次开发 · 自定义 AOP 拦截器](./extending#配方-c-自定义-aop-拦截器)。

---

## 工作单元与事务

### 我的应用服务方法没有事务

**没标 `[UnitOfWork]` 就没有环境工作单元。** 框架判定「这个类型要不要被工作单元拦截」的条件是：类或任一方法上有 `[UnitOfWork]`，**或者**类型实现了 `IUnitOfWorkEnabled`。而 `IUnitOfWorkEnabled` 在框架里**没有任何实现者**，`ApplicationServiceBase` 也没实现它。

所以写操作要事务，就在方法（或类）上显式标注：

```csharp
[UnitOfWork(true)]   // true = 需要事务
public async Task<InvoiceDto> CreateInvoiceAsync(InvoiceCreateDto input) { … }
```

### 内层调了 `RollbackAsync`，外层还能提交吗

不能。回滚之后再提交会抛 `XiHanException`。

父子工作单元共用同一物理事务，内层判定失败回滚就意味着整体终止——让外层若无其事地提交会产出「接口返回 200 但数据一行没写」。

同理 `SqlSugarClientResolver` 的钉住连接判定包含「是否已回滚」：检出已回滚的事务型工作单元会抛出并提示改用 `Begin(requiresNew: true)`，避免回滚之后的写入落在一条已无事务的连接上被逐条自动提交。

### `requiresNew` 到底给不给我一个独立事务

给。`requiresNew` 同时意味着**新的逻辑工作单元**和**新的物理连接**——两者缺一，内层就还是跑在外层的连接和事务上。

用的时候记住语义代价：内层提交后**不再受外层回滚影响**。所以**不要在已经修改过某些行的事务里，再用 `requiresNew` 去改同一批行**——两条独立连接改同一批行，轻则互相覆盖，重则死锁。

### 本地事件和分布式事件的发布时机有什么区别

时机不同，且这个差别是有意为之：

- **本地事件**：**提交前**发布。其处理器可能继续写库，这些写入必须落在同一个事务里。
- **分布式事件**：循环内只累积，**事务提交成功后**再按 `EventOrder` 统一发布。这样事务回滚时下游不会收到一份从未落库的数据。

代价是提交成功后若投递失败事件会丢。发件箱是进程内实现，进程一停即丢——要强投递保证请自行接持久化发件箱。

---

## 动态 API

### 生成的路由和我预期的不一样

三条最容易忘的规则：

1. **动词前缀会被剥离**：`GetUserPageAsync` → 动作名 `UserPage`（不是 `GetUserPage`），`Async` 后缀也去掉。
2. **没有匹配动词前缀时默认 POST**：`SwitchTenantAsync` → `POST /api/Auth/SwitchTenant`。
3. **路由段只由显式 `[FromRoute]` 参数产生**。普通参数一律落到查询串或请求体，**不会**自动变成 `/{id}` 路径段。

前缀匹配要求**词边界**（前缀之后必须是大写字母或下划线），所以 `AddressBook` 不会被 `Add` 命中而变成 `POST /ressBook`，`EditorTemplate` 也不会被 `Edit` 命中。

完整动词表见 [动态 API](./dynamic-api)。

### 分页方法收不到请求体

方法名以 `Get` 开头会被推导为 GET，请求体自然绑不上。**分页方法必须显式标 `[HttpPost]`**。方法上显式标注的 `[HttpGet]` / `[HttpPost]` 优先于前缀推导。

### 改了动词映射表，前端全 404 了

改 `Conventions.HttpMethodConventions`、`PreserveRoutePredicate`、`UseLowercaseRoutes`、`Routes.*` 中任何一个，都会让**已有接口的 URL 变化**。上线后再动这些开关等于一次破坏性变更，前端必须同步改。

---

## 数据访问

### 给实体加了字段，部署后报「列不存在」

`DbInitializer` **表存在就跳过建表**（日志里是「表已存在，跳过创建」），它**从不为已有表补列**。

要么重建数据库，要么手动 `ALTER TABLE` 补列。框架的定位是「首次启动自动建表 + 播种」，不是迁移工具。

### 实体变更日志（Diff）一条都没有

两个条件必须同时成立：

1. 配置 **`XiHan:Data:SqlSugarCore:EnableDiffLog` 设为 `true`**——**默认是 `false`**，不开则 Diff AOP 根本不挂载，收集到的差异被直接丢弃；
2. 写操作走仓储并调用了 `.EnableDiffLogEvent(businessData)`。

生产的 `appsettings` 常被 gitignore，最容易漏的就是第一条。

### 写操作报 `Parameter '@constant1001' already been defined`

仓储里**显式调用了 `.EnableQueryFilter()`**。

框架默认 `EnableAutoUpdateQueryFilter` / `EnableAutoDeleteQueryFilter` 均为 `true`，SqlSugar 的 `Updateable<T>()` / `Deleteable<T>()` 工厂内部**已经自动挂了一次**过滤。你再显式挂一次，同一份过滤会被烘进 WHERE 两遍、生成同名参数；一旦叠加 Diff 的 `GetDiffTable` 重查旧值就崩（MySQL 驱动直接抛，PG 驱动容忍重名故不崩，但仍是冗余死条件）。

**解法：把仓储里所有显式 `.EnableQueryFilter()` 删掉。** `.EnableDiffLogEvent()` 保留，它单独用是安全的。

### 租户上下文里改不了全局数据

这是**有意的写路径边界**：全局租户过滤器为「读共享」放行 `TenantId=0` 的平台全局行，但**写路径不复用这个口径**——租户上下文内禁止改写/删除非本租户行（含全局行），预读守卫会校验取回行的 `TenantId`，条件写会自动追加当前租户 Where。

维护全局 / 跨租户数据的唯一合法入口是**平台态**（无租户上下文，`ICurrentTenant.Change(null)`）。

---

## 缓存与事件

### 本地事件处理器不触发

事件总线只自动发现「**以接口为服务类型**」的注册。裸 `services.AddTransient<MyHandler>()`（注册为具体类）**不会被订阅，静默失败**。

处理器必须显式加入 `XiHanLocalEventBusOptions.Handlers`：

```csharp
services.AddTransient<MyHandler>();
services.Configure<XiHanLocalEventBusOptions>(o => o.Handlers.AddIfNotContains(typeof(MyHandler)));
```

BasicApp 把这两步封装成了 `AddSaasLocalEventHandler<T>()`。

### 怎么在缓存上执行 Lua 脚本

Lua 执行不在 `IDistributedCache` 这个通用契约上，而是**可选能力接口** `ICacheSupportsLuaScript`——通用缓存抽象不应该焊上某个客户端的类型。

用法是先做类型判断：

```csharp
if (cache is ICacheSupportsLuaScript lua)
{
    CacheScriptResult result = await lua.ScriptEvaluateAsync(script, keys, args);
}
```

入参是中立的 `object?[]`，返回 `CacheScriptResult`（支持标量、整数、布尔与嵌套数组）。`IRedisDelayQueue` / `IRedisStreamQueue` 是名字里就带 Redis 的专用抽象，直接用即可。

---

## 还没解决？

- [扩展与二次开发](./extending)：模块、替换默认实现、拦截器、动态 API 定制的完整配方
- [模块系统](./modularity) / [生命周期](./lifecycle) / [依赖注入](./dependency-injection)：机制本身
- [模块总览](../packages/)：逐包的配置项、API 与注意事项
- [BasicApp 常见问题](https://basicapp.docs.xihanfun.com/faq)：业务系统层面的坑（认证、权限、菜单、前端）
- 仍未解决可到 [GitHub Issues](https://github.com/XiHanFun/XiHan.Framework/issues) 提问
