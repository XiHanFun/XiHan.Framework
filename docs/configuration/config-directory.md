# 配置目录与热更新

这一页讲宿主怎样装载一个挂载的配置目录：环境变量 `XIHAN_CONFIG_PATH` 指向哪里、目录里的文件按什么顺序装载、它们与 appsettings 和环境变量谁覆盖谁、文件改了之后哪些值会跟着变，以及容器里为什么要开轮询式文件监视。读完你能在本机模拟 Kubernetes 的 ConfigMap 挂载并看到热更新生效。前置知识是 [配置模型与选项](/configuration/options)。

## 为什么需要

镜像里的 `appsettings.json` 随镜像一起发布，改一个值就要重新构建镜像；环境变量能在部署时覆盖，但改了必须重启容器。Kubernetes 的做法是把配置做成 ConfigMap 挂成目录，kubelet 会在 ConfigMap 变化后更新目录里的文件。应用要利用这一点，得知道去哪个目录找文件、按什么顺序装载、怎样监听变化。

框架把这件事收进宿主：`Hosting.AspNetCore` 与 `Hosting.CommandLine` 的 `AddXiHan` 在装配内核之前读环境变量 `XIHAN_CONFIG_PATH`，把那个目录里的 JSON 文件作为可重载的配置源插进 `appsettings.{环境}.json` 之后。应用代码不用管目录在哪，只要通过 `IOptionsMonitor<T>` 读值，文件变化就会反映出来。`xihan pack` 生成的部署产物会一并设置这个环境变量与挂载。

## 配置目录的规则

| 项 | 值 |
| --- | --- |
| 环境变量 | `XIHAN_CONFIG_PATH`（常量 `ConfigurationDirectory.Variable`） |
| 缺省挂载点 | `/etc/xihan`（`xihan pack` 应用清单里 `config.mountPath` 的缺省值） |
| 查找规则 | 内核的 `ConfigurationDirectory.Resolve` |
| 装载方式 | 每个文件一个 JSON 配置源，缺文件不报错，`reloadOnChange` 打开 |

`ConfigurationDirectory.Resolve` 找文件的规则：

1. 只取目录顶层的 `*.json`，不进子目录；跳过以 `.` 开头的条目（ConfigMap 挂载工具放置的 `..data` 等）。
2. `appsettings.json`（常量 `ConfigurationDirectory.BaseFileName`）最先装载，其余文件按文件名的序数顺序随后装载，后装载的覆盖先装载的同名键。
3. 目录不存在或环境变量为空时什么都不加，应用照常启动。

文件列表在启动时确定：运行中往目录里新增文件要重启才装载；已装载文件的内容变化会触发 `IConfiguration` 的重载令牌，`IOptionsMonitor<T>` 随之更新。

## 配置源的优先级

这些文件紧接构建器里最后一个 appsettings 文件源（`ConfigurationDirectory.IsApplicationSettings` 认出的 `appsettings.json` 或 `appsettings.{环境}.json`）插入。`WebApplication.CreateBuilder` 与 `Host.CreateApplicationBuilder` 的配置源因此从低到高是：

| 顺序 | 配置源 |
| --- | --- |
| 1 | `appsettings.json` |
| 2 | `appsettings.{环境}.json` |
| 3 | `XIHAN_CONFIG_PATH` 目录里的 JSON 文件 |
| 4 | 用户机密（仅开发环境） |
| 5 | 环境变量 |
| 6 | 命令行参数 |
| 7 | 应用自己添加的配置源（远程配置、密钥等），按添加顺序 |

后者覆盖前者：挂载文件覆盖 appsettings；同一个键同时出现在挂载文件与环境变量里时，环境变量的值生效。`xihan pack` 把应用清单的 `shutdown.graceSeconds` 写成 Dockerfile 与 Deployment 里的环境变量 `XiHan__Kernel__StopTimeoutSeconds`，这个值因此不会被 ConfigMap 里的同名键改掉。构建器里没有 appsettings 文件源（应用关掉了默认配置）时，目录里的文件追加在末尾。

## 最小示例

我们在本机模拟一次挂载：建一个目录放配置文件，用环境变量指向它，运行应用后改文件看结果。`BannerOptions` 沿用[配置模型与选项](/configuration/options)的写法。

```csharp
// Program.cs
using Microsoft.Extensions.Options;
using Shop;
using XiHan.Framework.Hosting.AspNetCore;
using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Profiles.Standard;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<BannerOptions>(builder.Configuration.GetSection(BannerOptions.Section));

// AddXiHan 先装载 XIHAN_CONFIG_PATH 目录里的 JSON 文件并监听变化，再装配内核
builder.AddXiHan(xihan => xihan.UseStandardProfile());

var app = builder.Build();
app.UseXiHan();
app.MapXiHanHealth();

// 读 CurrentValue 而不是在启动时缓存，挂载文件更新后返回新值
app.MapGet("/config", (IOptionsMonitor<BannerOptions> banner, IConfiguration configuration) =>
    new ConfigView(
        banner.CurrentValue.Banner,
        configuration["XiHan:Kernel:StopTimeoutSeconds"],
        Environment.GetEnvironmentVariable(ConfigurationDirectory.Variable)));

app.Run();

public sealed record ConfigView(string Banner, string? StopTimeout, string? ConfigPath);
```

准备目录与文件，再带着环境变量启动（PowerShell 里用 `$env:XIHAN_CONFIG_PATH = "$PWD\mounted"`）：

```bash
mkdir -p ./mounted
cat > ./mounted/appsettings.json <<'EOF'
{ "Sample": { "Banner": "Hello from mounted file" }, "XiHan": { "Kernel": { "StopTimeoutSeconds": 20 } } }
EOF
export XIHAN_CONFIG_PATH=$PWD/mounted
dotnet run
```

`curl localhost:5000/config` 返回：

```json
{"banner":"Hello from mounted file","stopTimeout":"20","configPath":"/path/to/mounted"}
```

不停应用，把文件里的 `Banner` 改成 `Changed`，保存后再请求一次，`banner` 已经是 `Changed`，而 `stopTimeout` 即使改了也只是配置树里的值变了：内核在装配时已经读过 `KernelOptions`，停止超时仍是 20 秒。要以 Native AOT 发布这个示例，`ConfigView` 需要登记到 `JsonSerializerContext`，见 [JSON 序列化与 Native AOT](/common/serialization)。

## 容器与 Kubernetes 里的热更新

应用清单 `xihan.app.json` 的 `config.files` 列出要做成 ConfigMap 的文件，`xihan pack` 据此生成 ConfigMap（`<name>-config`），只读挂到 `mountPath`，并在 Deployment 里设两个环境变量。ConfigMap 不加密、读得到它的人多，文件里有带值的凭据键（`Password`、`Secret`、`ApiKey`、`ConnectionStrings` 节这类，规则同选项里的凭据）时 `pack` 以 `CLI_VALIDATION_007` 失败，凭据要放进绑定的 Secret 或环境变量（ADR-0222）。两个环境变量：

| 环境变量 | 值 | 作用 |
| --- | --- | --- |
| `XIHAN_CONFIG_PATH` | `mountPath`（缺省 `/etc/xihan`） | 宿主把这个目录里的 JSON 文件作为可重载配置源 |
| `DOTNET_USE_POLLING_FILE_WATCHER` | `1` | ConfigMap 以切换符号链接的方式更新，文件监视必须轮询 |

::: warning ConfigMap 更新要用轮询监视
kubelet 更新 ConfigMap 时切换的是符号链接，默认的文件系统通知收不到变化，`IOptionsMonitor<T>` 永远不会更新。容器里要设 `DOTNET_USE_POLLING_FILE_WATCHER=1`。自己写 Deployment 而不用 `xihan pack` 时，别漏掉这一项。
:::

kubelet 同步 ConfigMap 有延迟，`kubectl edit configmap` 之后通常要一分钟左右端点才返回新值。清单字段与产物的完整说明见[应用清单与 xihan pack](/deployment/pack)与 [Kubernetes](/deployment/kubernetes)。

## 热更新的边界

热更新只作用于读 `IConfiguration` 或 `IOptionsMonitor<T>` 的代码：

- 框架包的选项在装配时已经读完，改挂载文件里的 `XiHan:*` 键不会改变正在运行的特性，要滚动重启才生效。
- `IOptions<T>` 只绑定一次；需要跟随变化就注入 `IOptionsMonitor<T>`，读 `CurrentValue` 或用 `OnChange` 注册回调。
- 运行中新增的文件要重启才装载；已有文件改内容才会触发重载。
- 环境变量与命令行参数的优先级高于挂载文件，被它们覆盖的键改了文件也不会变。

## Native AOT 下的绑定与重载

`Configure<T>(IConfiguration)` 默认按反射绑定，AOT 下不可用。在项目文件里打开配置绑定源生成器，绑定代码在编译期生成，重载照常：

```xml
<PropertyGroup>
  <EnableConfigurationBindingGenerator>true</EnableConfigurationBindingGenerator>
</PropertyGroup>
```

## 常见问题

### 改了文件但 IOptionsMonitor 没变

按顺序排查：注入的是 `IOptionsMonitor<T>` 而不是 `IOptions<T>`；文件在目录顶层且扩展名是 `.json`；同一个键没有被环境变量或命令行参数覆盖；容器里设置了 `DOTNET_USE_POLLING_FILE_WATCHER=1`；文件不是启动后才新增的。

### 能把 XiHan:* 的键放进挂载文件吗

能，它们在启动时与 appsettings 一样被读取，适合按环境调整 `XiHan:Kernel:StopTimeoutSeconds`、`XiHan:Caching:Redis:KeyPrefix` 这类值。但改动要重启才对框架生效。

### 目录里能放子目录或 YAML 吗

不能。只认目录顶层的 `*.json`，子目录与其他扩展名都跳过。要分多份文件就都放在顶层，按文件名排序决定覆盖顺序。

### 开发机上怎样关掉

不设置 `XIHAN_CONFIG_PATH` 即可，目录不存在或变量为空时什么都不加。

### 命令行宿主也支持吗

支持。`Hosting.CommandLine` 的 `AddXiHan` 用同一套规则装载目录，见[命令行宿主](/protocols/command-line)。

## 相关包与下一步

- [Kernel](/packages/kernel)：`ConfigurationDirectory` 与 `KernelOptions`
- [Hosting.AspNetCore](/packages/hosting-aspnetcore)、[Hosting.CommandLine](/packages/hosting-commandline)：装载配置目录的两个宿主
- [应用清单与 xihan pack](/deployment/pack)：`config.files` 与 `config.mountPath`
- 下一页：[资源绑定](/configuration/bindings)
