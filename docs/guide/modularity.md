# 模块系统

模块系统是 XiHan.Framework 的地基。理解了它，你就理解了整个框架的组织方式。

## 什么是"模块"

一个**模块**就是一个继承 `XiHanModule` 的类。它代表一块**可独立安装、可独立启用**的能力（一个 NuGet 包通常对应一个模块类）。模块自己负责三件事：

1. **注册服务** —— 把自己需要的服务加进 DI 容器
2. **声明依赖** —— 用 `[DependsOn]` 说明"我要用哪些别的模块"
3. **接入生命周期** —— 在合适的时机接入中间件、后台任务等

```csharp
using XiHan.Framework.Core.Modularity;

public class MyModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // 注册服务：context.Services 就是标准的 IServiceCollection
        context.Services.AddSingleton<IMyService, MyService>();
    }
}
```

## `[DependsOn]`：声明依赖

模块之间**不是靠你在 `Program.cs` 里手动排顺序**，而是靠 `[DependsOn]` 声明依赖关系：

```csharp
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Web.Api;
using XiHan.Framework.Data;

[DependsOn(
    typeof(XiHanWebApiModule),
    typeof(XiHanDataModule)
)]
public class MyAppModule : XiHanModule
{
}
```

这行声明的含义是：**加载 `MyAppModule` 之前，必须先加载 `XiHanWebApiModule` 和 `XiHanDataModule`**。而这两个模块又各自 `[DependsOn]` 更底层的模块……于是整棵依赖树被完整地表达出来。

## 自动拓扑排序加载

启动时你只需要指定**一个根模块**：

```csharp
await builder.AddApplicationAsync<MyAppModule>();
```

框架会：

1. 从根模块出发，沿着 `[DependsOn]` **递归收集**所有直接和间接依赖的模块（`XiHanModuleHelper.FindAllModuleTypes`），同一个模块被多处依赖时自动去重，只加载一次
2. 对收集到的模块做**拓扑排序**（`SortByDependencies`，被依赖的排在前面）；一旦发现循环依赖，或者某个模块声明依赖了一个不存在于依赖树里的类型，会在**启动阶段**直接抛异常，不会拖到运行时才出问题
3. 按这个顺序依次调用每个模块的生命周期钩子

启动日志会把这棵依赖树原样打印出来（重复出现的分支会标注"已跳过-重复加载"）。以上面 `MyAppModule` 的声明为例，实际展开后大致是这样（未继续下钻的分支各自还有依赖，规则相同，此处从略）：

```text
MyAppModule
├─ Web.Api
│  ├─ Web.Core
│  ├─ MultiTenancy
│  ├─ Serialization
│  └─ Auditing
└─ Data
   ├─ Domain
   │  └─ Domain.Shared
   ├─ Uow
   ├─ DistributedIds
   ├─ MultiTenancy（已加载，跳过）
   ├─ Security
   └─ Auditing（已加载，跳过）
```

**好处**：

- 加/减一块能力 = 加/减一行 `[DependsOn]`，不用管注册顺序
- 依赖图不允许有环，一旦成环或缺失依赖，应用**启动时**就会失败，不会等到跑到某个功能才发现
- 每个模块可以独立测试、独立发布

## 自动服务注册

除了模块级别的装配，框架还提供**约定式的服务注册**：实现了标记接口的类会被自动扫描并注册进 DI，无需逐个 `services.AddXxx`。

```csharp
using XiHan.Framework.Core.DependencyInjection.ServiceLifetimes;

// 实现 ITransientDependency，自动以 Transient 生命周期注册
public class OrderService : IOrderService, ITransientDependency
{
}
```

细节见 [依赖注入](./dependency-injection)。

## 插件式动态加载模块（进阶）

除了用 `[DependsOn]` 在编译期把依赖关系写死，`AddApplicationAsync` 的 `optionsAction` 里还暴露了 `options.PlugInSources`（`PlugInSourceList`），支持在**启动时从外部程序集动态发现模块** —— 用于插件化场景，即某些模块不随主程序一起编译，而是运行时按需装配：

```csharp
await builder.AddApplicationAsync<MyAppModule>(options =>
{
    // 扫描某个文件夹下的所有程序集，收纳其中继承 XiHanModule 的类型
    options.PlugInSources.AddFolder(@"C:\Plugins", SearchOption.TopDirectoryOnly);

    // 直接指定已加载程序集里的具体模块类型
    options.PlugInSources.AddTypes(typeof(SomePlugInModule));

    // 按文件路径逐个加载程序集
    options.PlugInSources.AddFiles(@"C:\Plugins\Foo.dll");
});
```

插件模块会和 `[DependsOn]` 声明的模块合并进同一棵依赖树，走同一套拓扑排序与生命周期调用逻辑，也会级联展开自己的 `[DependsOn]`。这是一个偏底层的扩展点，绝大多数业务模块用不到，仅在真正需要"运行时插拔功能"时才会用到。

## 无模块类的包

少数包是**纯工具库、框架基础设施或分析器**，没有模块类，也不参与 `[DependsOn]`：

- `XiHan.Framework.Utils` —— 零依赖工具库，直接引用、直接调用静态方法
- `XiHan.Framework.Metadata` —— 框架元数据常量
- `XiHan.Framework.Core` —— 模块系统本身的定义所在（`XiHanModule`、`[DependsOn]`、`ModuleLoader` 等都在这个包里），几乎所有模块工程都会引用它，但它自己没有模块类，不能出现在别人的 `[DependsOn]` 列表里
- `XiHan.Framework.Analyzers` —— Roslyn 分析器，作为 `Analyzer` 引用，编译期生效

这些包 `dotnet add package` 之后即可使用，不需要写 `[DependsOn]`。

## 小结

| 概念 | 作用 |
| --- | --- |
| `XiHanModule` | 模块的基类，一块能力的载体 |
| `[DependsOn]` | 声明模块依赖，驱动自动装配 |
| `AddApplicationAsync<T>` | 指定根模块，收集 + 拓扑排序 + 加载整棵依赖树 |
| `PlugInSourceList` | `options.PlugInSources`，运行时从文件夹/文件/类型动态发现模块，用于插件化场景 |
| 标记接口 | `ITransientDependency` 等，驱动约定式服务注册 |

## 下一步

- [生命周期](./lifecycle)：模块被加载时，具体会调用哪些钩子
- [依赖注入](./dependency-injection)：约定式注册与选项模式
- [动态 API](./dynamic-api)：应用服务如何变成 REST 接口
