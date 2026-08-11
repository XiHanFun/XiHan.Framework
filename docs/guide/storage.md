# 对象存储与虚拟文件

业务要存文件，但不该被绑死在某一家云厂商上；程序自己也要读文件（模板、配置、内置资源），但这些文件既可能在磁盘上也可能编译进了程序集。框架把这两件事拆成两个互不相干的包：`ObjectStorage` 管**业务文件的读写与生命周期**，`VirtualFileSystem` 管**程序自身资源的只读访问**。

## 先分清用哪个

| | XiHan.Framework.ObjectStorage | XiHan.Framework.VirtualFileSystem |
| --- | --- | --- |
| 面向 | 用户上传的业务文件 | 程序自带的资源 |
| 典型对象 | 头像、附件、导出文件 | 邮件模板、代码生成模板、内置 JSON |
| 后端 | 本地磁盘 / MinIO / 阿里云 OSS / 腾讯云 COS | 物理目录 + 程序集嵌入资源 |
| 读写 | 读写、删除、复制、移动 | **只读** |
| 核心入口 | `IFileStorageRouter`、`IFileStorageProviderManager` | `IVirtualFileSystem` |

::: tip 判断标准
文件是不是运行期由用户产生的？是就用 ObjectStorage，不是就用 VirtualFileSystem。
:::

---

## 对象存储

### 安装与启用

```bash
dotnet add package XiHan.Framework.ObjectStorage
```

```csharp
[DependsOn(typeof(XiHanObjectStorageModule))]
public class MyModule : XiHanModule { }
```

`XiHanObjectStorageModule` 在 `ConfigureServices` 里调 `services.AddXiHanObjectStorage(config)`，完成三件事：绑定 `XiHan:ObjectStorage` 及其四个子节；`TryAddSingleton` 注册 `IFileStorageProviderManager` 与 `IFileStorageRouter`；按 `EnabledProviders` + `DefaultProvider` 自动注册被启用的后端。两者都为空时回退到 `Local`。

### 上传一个文件

```csharp
public class AvatarAppService
{
    private readonly IFileStorageRouter _router;

    public AvatarAppService(IFileStorageRouter router) => _router = router;

    public async Task<string?> UploadAsync(Stream stream, string fileName)
    {
        // 按业务路由键选后端，未命中映射时回退 DefaultProvider
        var provider = _router.Route(routeKey: "avatar");

        var result = await provider.UploadAsync(new FileUploadRequest
        {
            FileStream = stream,
            FileName = fileName,
            StoragePath = $"avatars/{Guid.NewGuid():N}/{fileName}",
            ContentType = "image/png",
            Overwrite = true
        });

        // 必须检查 Success —— 失败不会抛异常
        return result.Success ? result.Url : null;
    }
}
```

::: danger 上传失败不抛异常
`FileStorageProviderBase.UploadAsync` 把子类 `UploadCoreAsync` 的异常统一收敛成 `FileUploadResult { Success = false, ErrorMessage = ... }`，顺带记下 `DurationMs`。

只写 `try/catch` 而不判 `result.Success`，失败会被当成成功继续往下走。**其余方法（下载、删除、元数据）则是正常抛异常的**，只有上传这一条路径特殊。
:::

### 后端怎么选

四个后端实现同一个 `IFileStorageProvider`，但能力不齐：

| 提供程序 | `ProviderName` | 分片上传 | 预签名 URL |
| --- | --- | --- | --- |
| `LocalFileStorageProvider` | `Local` | 支持（临时目录合并） | 返回静态直链，`expiresIn` 被忽略 |
| `AliyunOssStorageProvider` | `AliyunOSS` | 支持 | 真实签名 |
| `TencentCosStorageProvider` | `TencentCOS` | 声明支持，分片方法有坑（见下） | 真实签名 |
| `MinioFileStorageProvider` | `MinIO` | **不支持** | 真实签名 |

::: warning MinIO 的分片方法是占位实现
`MinioFileStorageProvider.SupportChunkedUpload` 为 `false`，但它**覆盖了**四个分片方法而不是让基类抛 `NotSupportedException`：`InitiateChunkedUploadAsync` 返回一个随便生成的 GUID，`UploadChunkAsync` 直接返回 `Success = true` 和形如 `chunk-1` 的假 ETag（**分片数据被丢弃**），`CompleteChunkedUploadAsync` 才返回 `Success = false`。

也就是说前面每一步都"成功"，最后一步才失败。走 MinIO 上传大文件请直接用 `UploadAsync`，由 SDK 自行处理分片。

**调分片方法前一律先判 `provider.SupportChunkedUpload`。**
:::

::: warning 腾讯云 COS 的 UploadChunkAsync 不写分片数据
`TencentCosStorageProvider.SupportChunkedUpload` 为 `true`，`InitiateChunkedUploadAsync` / `CompleteChunkedUploadAsync` 走的也是真实的 `InitMultipartUpload` / `CompleteMultiUpload`，但 `UploadChunkAsync` 调的是 `PutObject(bucket, key, request.StoragePath)`——既没用 `UploadPart`，也没读 `ChunkData`，而是把 `StoragePath` 当成本地文件路径整份上传。

走 COS 上传大文件同样请用 `UploadAsync`。
:::

### 分片上传流程

支持分片的后端按四步走，分片序号**从 1 开始**：

```csharp
var provider = _router.Route(routeKey: "attachment");
if (!provider.SupportChunkedUpload)
{
    // 退回整体上传
}

var uploadId = await provider.InitiateChunkedUploadAsync(new ChunkedUploadInitRequest
{
    FileName = fileName,
    StoragePath = storagePath,
    TotalSize = totalSize,
    ChunkSize = 5 * 1024 * 1024   // 默认即 5MB
});

var chunkInfos = new List<ChunkInfo>();
foreach (var (chunkStream, index) in chunks)   // index 从 1 开始
{
    var chunk = await provider.UploadChunkAsync(new ChunkUploadRequest
    {
        UploadId = uploadId,
        StoragePath = storagePath,
        ChunkNumber = index,
        ChunkData = chunkStream,
        ChunkSize = chunkStream.Length,
        TotalSize = totalSize,
        TotalChunks = totalChunks
    });

    if (!chunk.Success)
    {
        await provider.AbortChunkedUploadAsync(uploadId);
        throw new UserFriendlyException(chunk.ErrorMessage ?? "分片上传失败");
    }

    chunkInfos.Add(new ChunkInfo { ChunkNumber = chunk.ChunkNumber, ETag = chunk.ETag });
}

var result = await provider.CompleteChunkedUploadAsync(new ChunkedUploadCompleteRequest
{
    UploadId = uploadId,
    StoragePath = storagePath,
    ChunkInfos = chunkInfos
});
```

失败路径务必调 `AbortChunkedUploadAsync` 收尾。本地存储的分片落在系统临时目录 `chunked-uploads/{uploadId}` 下，`Complete` 或 `Abort` 都会清掉它；两个都不调，临时文件就一直留着。

::: warning 分片会话在进程内存里
本地存储的上传会话存在 `ConcurrentDictionary` 中。进程重启，或者多实例部署时分片请求被负载均衡打到了另一个实例，会拿到 `Upload session not found`。多实例下的分片上传需要会话粘滞。
:::

### 后端是怎么被选中的

`IFileStorageRouter.ResolveProviderName` 是一条三级瀑布：

| 优先级 | 来源 | 行为 |
| --- | --- | --- |
| 1 | 显式传入的 `providerName` | 非空即用，不再往下 |
| 2 | `routeKey` 查 `RouteProviderMappings` | 大小写不敏感，命中即用 |
| 3 | `DefaultProvider` | 兜底 |

第 2 步未命中时，`StrictRouteMatch = true` 会抛 `InvalidOperationException`，默认的 `false` 则静默回退到 `DefaultProvider`。

取到名字后交给 `IFileStorageProviderManager.GetProvider`：查 `XiHanObjectStorageProviderOptions.ProviderTypes` 拿类型，没登记就抛 `InvalidOperationException`；实例从容器解析出来后缓存进字典，后续复用。要静默判断用 `TryGetProvider`。

这套设计的意义是：**本地开发写磁盘、生产切云端，只改配置不改代码**。

```json
{
  "XiHan": {
    "ObjectStorage": {
      "DefaultProvider": "Local",
      "EnabledProviders": [ "Local", "AliyunOSS" ],
      "RouteProviderMappings": {
        "avatar": "Local",
        "attachment": "AliyunOSS"
      }
    }
  }
}
```

::: danger 配置节名 ≠ 提供程序名
配置**节**是 `Local` / `Minio` / `AliyunOss` / `TencentCos`（各 Options 的 `SectionName`）；提供程序**名**是 `Local` / `MinIO` / `AliyunOSS` / `TencentCOS`（`ObjectStorageProviderNames` 常量，用于 `EnabledProviders`、`DefaultProvider` 和映射值）。

拼写不同，别互抄。`EnabledProviders` 里写了识别不了的名字，启动期直接抛 `InvalidOperationException`。
:::

### 路径与存储桶

`StoragePath` 一律是**对象键**，不含桶名。桶由 `BucketName` 决定，为空则用该后端配置里的 `DefaultBucket`：

- `UploadAsync` 读 `FileUploadRequest.BucketName`；
- `DeleteAsync` / `ExistsAsync` / `GetMetadataAsync` 各有一个带 `bucketName` 参数的重载；基类的默认实现忽略该参数、转调单参版本，只有真正实现了它的后端才生效。

腾讯云 COS 的桶名会自动拼成 `{DefaultBucket}-{AppId}`，配置里填不带 AppId 的部分。阿里云 OSS 与腾讯云 COS 配了 `CdnDomain` 时，返回的 URL 走 `https://{CdnDomain}/{objectKey}`，不再走桶域名。

---

## 本地存储：RootPath、UrlPrefix 与静态文件挂载

本地存储是默认后端，也是最容易配错的一个，因为它牵扯两个各自独立读配置的组件。

### 两个字段

`LocalStorageOptions`（配置节 `XiHan:ObjectStorage:Local`）只有两个字段：

| 字段 | 默认值 | 作用 |
| --- | --- | --- |
| `RootPath` | `wwwroot/Uploads` | 文件真正落盘的目录 |
| `UrlPrefix` | `/uploads` | 拼 `FileUploadResult.Url` 用的路径前缀 |

`LocalFileStorageProvider` 构造时把 `RootPath` 过一遍 `Path.GetFullPath`，目录不存在就创建；`UrlPrefix` 归一化成 `"/" + 去掉首尾斜杠`。

于是 `StoragePath = "avatars/a.png"` 落盘在 `{RootPath}/avatars/a.png`，`Url` 是 `/uploads/avatars/a.png`。

### 静态文件是自动挂上的

`XiHanWebApiModule` 在管线里替你挂好了这个目录，无需自己写 `UseStaticFiles`：

```
UseCors()
  → 本地对象存储静态文件服务   ← 在这里
  → UseAuthentication()
  → UseAuthorization()
```

它经 `IConfiguration` 直接读 `XiHan:ObjectStorage:Local:RootPath` 和 `:UrlPrefix`（这样 Web.Api 不必编译期依赖 ObjectStorage 包），然后以 `PhysicalFileProvider` + `RequestPath = UrlPrefix` 调 `UseStaticFiles`。

::: warning 显式配置 RootPath
`LocalStorageOptions` 3.10.1 的默认落盘目录是 `wwwroot/Uploads`，但 Web.Api 在完全没有该配置节时仍回退到 `wwwroot/uploads`。在 Linux 等区分大小写的文件系统上，这会成为两个目录。Web 应用应显式配置 `RootPath`，并确保对象存储与静态文件挂载读取同一个值。
:::

::: warning 挂在鉴权之前，意味着匿名可读
这个位置是有意的——头像之类的公开资源要能匿名直链。反过来说，**落进 `RootPath` 的所有文件都是公开的**。

`FileUploadRequest.AccessControl` 对本地存储不起作用：本地提供程序压根不读这个字段（阿里云 OSS 会把它翻成 `public-read` / `private` 的 ACL，腾讯云 COS 原样交给 `SetCosACL`）。私密文件不要放本地存储的 `UrlPrefix` 目录下。
:::

### 三个易错点

::: danger 相对路径的基准不一致
提供程序用 `Path.GetFullPath(RootPath)` 解析——基准是**进程当前工作目录**；静态文件中间件用 `Path.Combine(ContentRootPath, RootPath)`——基准是**内容根目录**。

平时两者相同，但以 systemd、容器 `WORKDIR` 或 Windows 服务方式启动、工作目录被设成别处时，就会出现"文件明明写进去了，URL 访问却 404"。**生产环境把 `RootPath` 配成绝对路径**，两边就都不会猜。
:::

::: warning 未知扩展名一律 404
静态文件以 `ServeUnknownFileTypes = false` 挂载，MIME 类型识别不出来的文件不会被返回。上传前统一扩展名，或者让这类文件走后端下载接口而不是静态直链。
:::

::: warning UrlPrefix 不能配成 "/"
归一化后等于 `/` 时，挂载逻辑直接跳过，静态直链整体失效。
:::

### 一个顺手的机制

`NormalizeLocalPath` 会把路径开头的 `UrlPrefix` 段剥掉。所以 `FileUploadResult.Url`（`/uploads/avatars/a.png`）可以原样回传给 `DeleteAsync` / `ExistsAsync`，不用先手工去前缀：

```csharp
await provider.DeleteAsync(result.Url!);   // 与传 "avatars/a.png" 等价
```

另外两点：`FileUploadRequest.Overwrite` 默认 `false`，目标已存在会返回 `Success = false` + `"File already exists"`；设置了 `ProgressCallback` 时，回调的第二个参数取自 `FileStream.Length`，所以传进来的流必须能取长度。

---

## 虚拟文件系统

### 解决什么问题

模板、内置配置这类程序自带的资源，有两种存在形式：磁盘上的物理文件，和编译进程序集的嵌入资源。虚拟文件系统把它们叠成一个统一的路径空间——同一个 `/Templates/mail.html`，先查磁盘，磁盘没有再取程序集内置的。

这天然形成一个覆盖机制：**框架包内置一份默认模板，宿主应用在磁盘上放同名文件就能覆盖它**，不用改代码也不用重新打包。

### 安装与启用

```bash
dotnet add package XiHan.Framework.VirtualFileSystem
```

```csharp
[DependsOn(typeof(XiHanVirtualFileSystemModule))]
public class MyModule : XiHanModule { }
```

`IVirtualFileSystem` 以单例注册，直接注入即可。

### 默认挂载与优先级

无需任何配置，构造时就挂好了这些提供程序（**数值越大越优先**）：

| 优先级 | 来源 | 开关 |
| --- | --- | --- |
| 100 | 当前工作目录 | `IncludeCurrentDirectory`，默认 `true` |
| 90 | `AdditionalPhysicalPaths` 里的每个目录 | 默认空 |
| 80 | 应用基目录（`AppContext.BaseDirectory`） | `IncludeAppBaseDirectory`，默认 `true` |
| 50 | `AddEmbedded` 注册的程序集 | 需显式添加 |

`GetFile` 按优先级从高到低逐个问，第一个命中的就返回——所以物理目录天然压过嵌入资源。物理目录不存在时会被自动创建。

追加自己的来源有两种写法：

```csharp
// 启动期：在模块里配置
services.Configure<VirtualFileSystemOptions>(options =>
{
    options.AddPhysical("./templates", priority: 120);
    options.AddEmbedded<MyModule>(priority: 50);
});

// 运行期：动态挂载/卸载
_virtualFileSystem.Mount(new PhysicalFileProvider("/data/skins"), priority: 200);
_virtualFileSystem.Unmount(provider);
```

同一个物理目录或同一个程序集重复添加会覆盖前一次，不会挂两遍。

### 读取与枚举

```csharp
public class MailTemplateProvider
{
    private readonly IVirtualFileSystem _vfs;

    public MailTemplateProvider(IVirtualFileSystem vfs) => _vfs = vfs;

    public async Task<string> ReadAsync(string name)
    {
        var file = _vfs.GetFile($"~/Templates/{name}.html");
        if (!file.Exists)
        {
            throw new FileNotFoundException(name);
        }

        using var stream = file.CreateReadStream();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }

    // 递归列出全部 json，返回虚拟路径集合
    public IReadOnlyList<string> ListConfigs()
        => _vfs.EnumerateFiles("/Configs", "*.json", recursive: true);
}
```

`GetFile` 找不到时返回 `NotFoundFileInfo` 而不是抛异常，判 `file.Exists` 即可；`GetDirectoryContents` 同样不抛异常，返回的是 `PrioritizedDirectoryContents` 包装，目录不存在时 `Exists` 为 `false`。

路径写法由 `PathResolver` 统一处理：

| 写法 | 解析结果 |
| --- | --- |
| `/Templates/a.html` | 原样归一化 |
| `~/Templates/a.html` | 去掉 `~`，等价于上一行 |
| `embedded://MyAssembly/Templates/a.html` | 剥掉协议头与**第一段**，得到 `/Templates/a.html` |
| `memory://a.json`、`mem://a.json` | 剥掉协议头，得到 `/a.json` |

::: warning embedded:// 里的程序集名只是标记
`ResolveEmbeddedPath` 剥掉的是协议头到第一个斜杠之间的整段，剩下的路径仍按优先级在**全部**已挂载的提供程序里查找。写 `embedded://A/x.html` 不代表只在程序集 A 里找，物理目录里有同路径文件照样会先命中。
:::

### 嵌入资源

`AddEmbedded` 建立在 `EmbeddedFileProvider` 之上，未指定基命名空间，因此虚拟路径 `/Templates/mail.html` 对应资源名 `{程序集名}.Templates.mail.html`。文件必须真的被打包进去：

```xml
<ItemGroup>
  <EmbeddedResource Include="Templates/**/*.html" />
</ItemGroup>
```

漏了这行，`AddEmbedded` 注册成功但永远取不到文件。

### 变更监听

```csharp
_vfs.OnFileChanged += (_, e) =>
{
    // e.FilePath、e.ChangeType（Created / Modified / Deleted）
    _cache.Remove(e.FilePath);
};

// 关键：不调 Watch 就不会有任何事件
_vfs.Watch("**/*.json");
```

::: danger 只订阅事件不生效
`OnFileChanged` 的触发链是挂在 `Watch(filter)` 注册的回调上的。只订阅事件、从不调 `Watch`，事件永远不会触发。
:::

通配符规则：`**` 匹配任意层级，`*` 匹配单层内任意字符，`?` 匹配单字符；`*` 和 `**/*` 会短路成"全部匹配"。事件带防抖，间隔取 `ChangeDebounceMilliseconds`（默认 500）与 50 的较大值。不需要监听时把 `EnableChangeTracking` 设为 `false`——它会在挂载物理目录时递归扫描全部文件建立基线快照，目录大时启动开销可观。

### 文件版本快照

`IFileVersioningService` 提供 `Snapshot(IFileInfo)` 与 `Rollback(path, steps)`，把文件内容整份压进栈里，回滚时弹出并写回磁盘。

::: warning 进程内、不持久化
版本栈是内存里的 `ConcurrentDictionary`，进程重启即丢，且完整内容常驻内存。`Snapshot` 要求 `file.PhysicalPath` 非空（嵌入资源没有物理路径，会抛 `ArgumentNullException`），`Rollback` 也要求目标文件仍存在。它适合"改配置前留个后悔药"这类短周期场景，不是版本管理方案。
:::

---

## 配置速查

对象存储 `XiHan:ObjectStorage`：

| 键 | 默认值 | 说明 |
| --- | --- | --- |
| `DefaultProvider` | `Local` | 默认后端名 |
| `EnabledProviders` | `["Local"]` | 启用哪些后端，值必须是可识别的名字 |
| `RouteProviderMappings` | 空 | 业务路由键 → 后端名，大小写不敏感 |
| `StrictRouteMatch` | `false` | 路由键未命中时抛异常还是回退默认 |

子节：`:Local`（`RootPath`、`UrlPrefix`）、`:Minio`（`Endpoint`、`AccessKey`、`SecretKey`、`DefaultBucket`、`UseSSL`、`Region`）、`:AliyunOss`（`AccessKeyId`、`AccessKeySecret`、`Endpoint`、`DefaultBucket`、`CdnDomain`、`UseInternal`）、`:TencentCos`（`SecretId`、`SecretKey`、`AppId`、`Region`、`DefaultBucket`、`CdnDomain`）。

虚拟文件系统 `XiHan:VirtualFileSystem`：

| 键 | 默认值 | 说明 |
| --- | --- | --- |
| `IncludeCurrentDirectory` | `true` | 自动挂载当前工作目录（优先级 100） |
| `IncludeAppBaseDirectory` | `true` | 自动挂载应用基目录（优先级 80） |
| `AdditionalPhysicalPaths` | 空 | 追加的物理目录（优先级 90） |
| `EnableChangeTracking` | `true` | 是否启用变更追踪 |
| `ChangeDebounceMilliseconds` | `500` | 变更事件防抖，实际取值不低于 50 |

完整配置项见 [ObjectStorage 包](../packages/object-storage) 与 [VirtualFileSystem 包](../packages/virtual-file-system)。

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 上传失败但没有异常 | `UploadAsync` 把异常收敛进 `ErrorMessage`，必须判 `result.Success` |
| 文件写进去了，URL 却 404 | `RootPath` 用了相对路径而工作目录 ≠ 内容根；或扩展名 MIME 识别不出被 `ServeUnknownFileTypes = false` 拦掉 |
| 静态直链整体失效 | `UrlPrefix` 归一化后等于 `/`，挂载逻辑直接跳过 |
| 本地存储设了 `AccessControl` 仍能匿名访问 | 本地提供程序不读该字段，`UrlPrefix` 目录挂在鉴权之前，一律匿名可读 |
| MinIO 分片每步都成功，最后失败 | 分片方法是占位实现、数据被丢弃；改用 `UploadAsync` |
| 腾讯云 COS 分片上传报本地文件找不到 | `UploadChunkAsync` 把 `StoragePath` 当本地文件路径调 `PutObject`，从不读 `ChunkData`；改用 `UploadAsync` |
| 分片上传报 `Upload session not found` | 会话在进程内存里，进程重启或请求被打到别的实例 |
| `GetProvider` 抛 `InvalidOperationException` | 该名字不在 `EnabledProviders` / `DefaultProvider` 中，也没经 `AddFileStorageProvider` 注册 |
| 启动期抛"不支持的对象存储提供程序" | `EnabledProviders` 里的名字拼错，与配置节名混用了 |
| 本地 `GetMetadataAsync` 的 `ContentType` 回落成 `application/octet-stream` | 本地提供程序只内置 jpg/jpeg、png、gif、webp、pdf、zip、mp4、mp3 的映射，其余扩展名一律回落 |
| 预签名 URL 在本地存储上没有时效 | 本地返回的是静态直链，`expiresIn` 被忽略 |
| 虚拟文件读到的是另一个目录里的同名文件 | 优先级高的先命中：工作目录 100 > 附加目录 90 > 基目录 80 > 嵌入资源 50 |
| `AddEmbedded` 后取不到文件 | 文件没配 `EmbeddedResource`，或资源名与 `{程序集名}.{目录}.{文件名}` 对不上 |
| 订阅了 `OnFileChanged` 却从不触发 | 没调 `Watch(filter)`，事件链没挂上 |

## 下一步

- [Web 应用开发](./web)：静态文件挂载所在的中间件管线
- [配置与选项](./configuration)：配置节绑定与 Options 模式
- [模块系统](./modularity)：`DependsOn` 与模块装配
- [ObjectStorage 包](../packages/object-storage)：完整 API 清单与全部配置项
- [VirtualFileSystem 包](../packages/virtual-file-system)：完整 API 清单与全部配置项
