# XiHan.Framework.VirtualFileSystem

> 虚拟文件系统：把**本地物理目录**与**程序集嵌入资源**按优先级统一挂载到一套虚拟路径下访问、枚举与监控，高优先级同名文件覆盖低优先级；另附内存版本快照与回滚。**不含云存储适配**（那是 ObjectStorage）。

- **NuGet**：`XiHan.Framework.VirtualFileSystem`
- **模块类**：`XiHanVirtualFileSystemModule`
- **所在层**：基础设施层
- **关键依赖**：`Microsoft.Extensions.FileProviders` 系列（Composite / Physical / Embedded）；框架内部依赖 `XiHan.Framework.Core`（模块化）与 `XiHan.Framework.Utils`（`Debouncer`、`HashHelper`、`Guard`）。

## 概述

XiHan.Framework.VirtualFileSystem 在 `Microsoft.Extensions.FileProviders` 之上做了一层统一封装。它让你不用关心一个文件到底来自本地磁盘目录还是打包进程序集的嵌入资源，都用同一套虚拟路径去读取、枚举、判断存在与监控变化。

多个来源可以按优先级叠加挂载：内部用 `VirtualCompositeFileProvider` 把所有提供程序按优先级（数值越大越优先）排序聚合，高优先级的同名文件覆盖低优先级的，从而实现「默认嵌入资源 + 外部物理目录覆盖」的组织方式。此外提供 `IFileVersioningService` 对物理文件做内存版本快照，出错时可回滚。

**能力边界（以源码为准）**：本包只做「本地物理目录 + 程序集嵌入资源」两类来源的统一虚拟访问，**不包含任何云存储/对象存储适配**。面向 OSS/COS/MinIO 等云端读写请用 [XiHan.Framework.ObjectStorage](./object-storage)。

## 何时使用

- 需要把配置、模板、静态资源等以统一入口访问，无论它们来自磁盘还是嵌入资源。
- 希望用「外部物理目录覆盖内置嵌入资源」的方式做资源定制，而不改动内置文件。
- 需要监控某类文件（如 `**/*.json`）变化并触发热更新。
- 需要在写入物理文件前对其做内存快照，出错时把内容回滚到上一个版本。
- **不该用它的场景**：需要读写云存储/对象存储 → 用 ObjectStorage；需要持久化的文件版本历史 → 本包的版本快照仅在内存中、进程重启即失，不是持久版本库。

## 安装与启用

```bash
dotnet add package XiHan.Framework.VirtualFileSystem
```

```csharp
[DependsOn(typeof(XiHanVirtualFileSystemModule))]
public class MyModule : XiHanModule { }
```

模块在 `ConfigureServices` 中调用 `AddXiHanVirtualFileSystem(config)`：

- `AddOptions<VirtualFileSystemOptions>()` 并（当传入 `IConfiguration` 时）绑定配置节 `XiHan:VirtualFileSystem`。
- `TryAddSingleton` 注册 `IVirtualFileSystem`（默认 `VirtualFileSystem`）与 `IFileVersioningService`（默认 `FileVersioningService`），均为 **Singleton**。

## 工作原理

- **自动挂载默认来源**：`VirtualFileSystem` 构造时据 Options 自动挂载——`IncludeCurrentDirectory`（当前工作目录，优先级 100）、`IncludeAppBaseDirectory`（应用基目录 `AppContext.BaseDirectory`，优先级 80）、`AdditionalPhysicalPaths`（附加物理目录，优先级 90）。三者默认全开。
- **优先级聚合**：所有 `PrioritizedFileProvider` 按 `Priority` 降序排列，交给 `VirtualCompositeFileProvider` 聚合成一个 `IFileProvider`；查文件时高优先级先命中，实现同名覆盖。无任何提供程序时回退 `NullFileProvider`。
- **去重键**：物理提供程序按规范化绝对路径去重、嵌入提供程序按程序集 `FullName` 去重（`GetProviderKey`）；重复挂载会替换旧的。
- **虚拟路径解析**（`PathResolver.ResolveVirtualPath`）：支持前缀 `~/`（应用相对）、`embedded://<assembly>/<resource>`（嵌入资源）、`memory://` 与 `mem://`（内存前缀），以及普通 `/xxx` 路径；统一规范化为以 `/` 开头、无尾斜杠的路径。
- **变更监控**（`Watch` + `OnFileChanged`）：底层 `IChangeToken` 触发后，经 `Debouncer` 防抖（`ChangeDebounceMilliseconds`，下限 50ms），扫描物理目录与嵌入资源名单，比对内存 `_fileStateCache`（物理按最后写入时间、嵌入按资源名存在性）得出 `Created`/`Modified`/`Deleted` 列表并逐个触发事件，随后自动重新注册监听。`EnableChangeTracking=false` 时不做追踪。
- **初始化失败收敛**：构造异常统一包成 `XiHanException("虚拟文件系统初始化失败", ex)`。

## 核心能力

- **统一虚拟访问**：`IVirtualFileSystem` 提供 `GetFile` / `GetDirectoryContents` / `FileExists` / `DirectoryExists` / `EnumerateFiles`；文件/目录不存在时底层返回 `NotFoundFileInfo` / `NotFoundDirectoryContents`（`Exists=false`），不抛异常。
- **多来源挂载**：内置物理目录、嵌入资源两类提供程序，可通过 `Mount(provider, priority)` / `Unmount(provider)` 动态挂载卸载，也支持挂载任意自定义 `IFileProvider`。
- **优先级覆盖**：每个提供程序带优先级（数值越大越优先），高优先级同名文件覆盖低优先级，由 `VirtualCompositeFileProvider` 聚合。
- **变更监控**：`Watch(filter)` 返回 `IChangeToken`，配合 `OnFileChanged` 事件感知文件变化，支持通配符 `*` / `**` / `?`、防抖与开关。
- **内存版本快照**：`IFileVersioningService` 对物理文件做内存快照（内容字节 + 哈希 + 大小 + 时间戳），并可回滚指定步数——**版本仅存内存栈、不持久化**，但回滚会把目标版本内容**写回物理文件**。

## 主要 API / 类型

### `IVirtualFileSystem` 方法签名

| 方法 | 说明 |
| --- | --- |
| `event EventHandler<FileChangedEventArgs> OnFileChanged` | 文件变化事件 |
| `IFileInfo GetFile(string virtualPath)` | 取文件信息，不存在返回 `NotFoundFileInfo` |
| `IDirectoryContents GetDirectoryContents(string virtualPath)` | 取目录内容，不存在返回 `NotFoundDirectoryContents` |
| `bool FileExists(string virtualPath)` / `bool DirectoryExists(string virtualPath)` | 存在性判断 |
| `IReadOnlyList<string> EnumerateFiles(string virtualPath, string searchPattern = "*", bool recursive = true)` | 枚举目录下文件（返回虚拟路径） |
| `IChangeToken Watch(string filter)` | 监控变化，如 `Watch("**/*.json")` |
| `void Mount(IFileProvider provider, int priority = 0)` | 挂载提供程序 |
| `bool Unmount(IFileProvider provider)` | 卸载提供程序 |

### 配置与提供程序类型

| 类型 | 说明 |
| --- | --- |
| `IVirtualFileSystem` / `VirtualFileSystem` | 统一虚拟文件系统入口（挂载、访问、枚举、监控）；`VirtualFileSystem` 实现 `IDisposable` |
| `VirtualFileSystemOptions` | 配置项 + 流式构建方法（见下） |
| `VirtualPhysicalFileProvider` | 带优先级的物理磁盘提供程序（继承 `PhysicalFileProvider`，构造需绝对路径） |
| `VirtualEmbeddedFileProvider` | 带优先级的程序集嵌入资源提供程序（继承 `EmbeddedFileProvider`） |
| `VirtualCompositeFileProvider` | 按优先级聚合多个提供程序的复合提供程序 |
| `PrioritizedFileProvider` | 提供程序 + 优先级的封装（`Provider` / `Priority`） |
| `PrioritizedFileInfo` / `PrioritizedDirectoryContents` | 带优先级信息的 `IFileInfo` / `IDirectoryContents` |
| `PathResolver` | 静态虚拟路径工具：`ResolveVirtualPath` / `NormalizeVirtualPath` / `CombineVirtualPath` / `IsPathUnder` / `ResolveEmbeddedPath` / `ResolveMemoryPath` |

`VirtualFileSystemOptions` 流式构建方法：

| 方法 | 说明 |
| --- | --- |
| `AddPhysical(string rootPath, int priority = 100)` | 添加物理目录（自动创建不存在的目录、转为绝对路径） |
| `AddPhysicalRange(IEnumerable<string> rootPaths, int priority = 100)` | 批量添加物理目录 |
| `AddEmbedded<TAssembly>(int priority = 50)` / `AddEmbedded(Assembly, int priority = 50)` | 添加程序集嵌入资源 |
| `AddProvider(IFileProvider, int priority = 0)` | 添加任意自定义提供程序（同 key 会替换） |

### 版本控制与事件

| 类型 | 说明 |
| --- | --- |
| `IFileVersioningService` / `FileVersioningService` | `void Snapshot(IFileInfo file)`（对物理文件压栈快照）、`bool Rollback(string path, int steps = 1)`（弹出指定步数版本并写回物理文件） |
| `FileVersion` | 版本信息：`Content`（字节）、`ContentHash`、`Length`、`Timestamp` |
| `FileChangedEventArgs` | `FilePath`、`ChangeType` |
| `FileChangeType` | 枚举 `Created` / `Modified` / `Deleted` |

## 配置

配置节 `XiHan:VirtualFileSystem`（`VirtualFileSystemOptions.SectionName`）：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `ChangeDebounceMilliseconds` | `int` | `500` | 文件变更事件防抖毫秒数（运行时下限强制 50ms） |
| `EnableChangeTracking` | `bool` | `true` | 是否启用文件变更追踪 |
| `IncludeCurrentDirectory` | `bool` | `true` | 是否自动挂载当前工作目录（优先级 100） |
| `IncludeAppBaseDirectory` | `bool` | `true` | 是否自动挂载应用基目录（优先级 80） |
| `AdditionalPhysicalPaths` | `List<string>` | 空 | 附加物理目录（优先级 90） |

> `Providers` 集合与 `AddPhysical` / `AddEmbedded` / `AddProvider` 等构建方法通常通过代码（`services.Configure<VirtualFileSystemOptions>(...)`）配置，不便直接写在 `appsettings.json` 里。上表标量字段可用配置文件绑定：

```json
{
  "XiHan": {
    "VirtualFileSystem": {
      "ChangeDebounceMilliseconds": 500,
      "EnableChangeTracking": true,
      "IncludeCurrentDirectory": true,
      "IncludeAppBaseDirectory": true,
      "AdditionalPhysicalPaths": [ "./resources" ]
    }
  }
}
```

## 使用示例

### 挂载物理目录覆盖嵌入资源，并按虚拟路径读取

```csharp
// 通过 Options 配置：嵌入资源（默认）+ 外部物理目录（更高优先级覆盖）
services.Configure<VirtualFileSystemOptions>(options =>
{
    options.AddEmbedded<MyModule>(priority: 50);      // 内置默认资源
    options.AddPhysical("./resources", priority: 100); // 外部覆盖，优先级更高
});

// 使用：按统一虚拟路径读取（~/ 表示应用相对）
public class ConfigLoader
{
    private readonly IVirtualFileSystem _vfs;
    public ConfigLoader(IVirtualFileSystem vfs) => _vfs = vfs;

    public string? ReadAppJson()
    {
        var file = _vfs.GetFile("~/config/app.json");
        if (!file.Exists)
        {
            return null;
        }

        using var stream = file.CreateReadStream();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
```

### 枚举 + 监控 JSON 文件变化

```csharp
public class JsonWatcher
{
    private readonly IVirtualFileSystem _vfs;
    public JsonWatcher(IVirtualFileSystem vfs)
    {
        _vfs = vfs;

        // 枚举所有 JSON（默认递归）
        var jsonFiles = _vfs.EnumerateFiles("~/config", "*.json", recursive: true);

        // 监控变化：Watch 返回 IChangeToken，实际变化经 OnFileChanged 事件派发
        _vfs.Watch("**/*.json");
        _vfs.OnFileChanged += (_, e) =>
        {
            // e.FilePath 为虚拟路径，e.ChangeType ∈ { Created, Modified, Deleted }
            Console.WriteLine($"{e.ChangeType}: {e.FilePath}");
        };
    }
}
```

### 物理文件快照与回滚

```csharp
public class SafeWriter
{
    private readonly IVirtualFileSystem _vfs;
    private readonly IFileVersioningService _versioning;

    public SafeWriter(IVirtualFileSystem vfs, IFileVersioningService versioning)
    {
        _vfs = vfs;
        _versioning = versioning;
    }

    public void WriteWithRollback(string virtualPath, string physicalPath, string newContent)
    {
        var file = _vfs.GetFile(virtualPath);
        if (file.Exists && file.PhysicalPath is not null)
        {
            _versioning.Snapshot(file);   // 写入前对物理文件做内存快照
        }

        try
        {
            File.WriteAllText(physicalPath, newContent);
        }
        catch
        {
            // 出错时回滚：把最近一次快照的内容写回物理文件
            _versioning.Rollback(physicalPath, steps: 1);
            throw;
        }
    }
}
```

## 扩展点 / 自定义

- **挂载自定义来源**：`Mount(IFileProvider, priority)` 可挂载任意 `Microsoft.Extensions.FileProviders.IFileProvider` 实现（如自定义内存/网络提供程序），纳入统一优先级聚合。
- **替换核心服务**：`IVirtualFileSystem` / `IFileVersioningService` 均以 `TryAddSingleton` 注册，可在模块中先注册自定义实现以覆盖默认。
- **优先级约定**：约定嵌入资源用较低优先级（默认 50）、物理覆盖目录用较高优先级（默认 100），即可获得「内置 + 外部覆盖」的效果。

## 注意事项与最佳实践

- **快照/回滚只针对物理文件**：`Snapshot` 要求 `IFileInfo.PhysicalPath` 非空——对嵌入资源（无物理路径）调用会抛 `ArgumentNullException`。`Rollback` 也只在目标物理文件仍存在时才写回。
- **版本不持久化**：版本历史存于进程内内存栈（`ConcurrentDictionary<string, Stack<FileVersion>>`），进程重启即丢失；它不是持久版本库，别当备份用。
- **回滚会写回磁盘**：`Rollback` 弹出指定步数的版本并 `File.WriteAllBytes` 覆盖物理文件、更新最后写入时间。这是「内存版本 → 物理文件」的还原，不是纯内存操作。
- **默认自动挂载当前/基目录**：默认 `IncludeCurrentDirectory` / `IncludeAppBaseDirectory` 均为 `true`，会把工作目录与应用基目录纳入虚拟根。若不希望暴露它们，显式置 `false`。
- **变更事件是「扫描比对」得出的**：底层 token 触发后由本包扫描目录/资源名单与内存状态缓存比对，得出增删改；`EnableChangeTracking=false` 时 `OnFileChanged` 不会派发变更。
- **不做云存储**：本包不含 OSS/COS/MinIO 适配；那属于 [XiHan.Framework.ObjectStorage](./object-storage)。

## 依赖模块

- [XiHan.Framework.Core](./core) — 模块化生命周期与依赖注入扩展、`XiHanException`。
- [XiHan.Framework.Utils](./utils) — `Debouncer`（防抖）、`HashHelper`（内容哈希）、`Guard`（参数校验）。

第三方核心依赖为 `Microsoft.Extensions.FileProviders` 系列（Composite / Physical / Embedded），虚拟文件能力构建其上。

## 相关模块

- [XiHan.Framework.ObjectStorage](./object-storage) — 面向对象存储/云存储的文件读写与生命周期管理，与本包「本地/嵌入虚拟路径统一只读访问」定位互补。
- [XiHan.Framework.Templating](./templating) — 模板渲染常从虚拟文件系统读取模板资源。
- [XiHan.Framework.Core](./core) — 提供模块与依赖注入基础。
