# 搜索引擎

框架把全文检索收敛成一个很小的接口 `ISearchEngine`：业务代码只写「建索引、写文档、查」，开发期跑进程内实现，生产切到 Elasticsearch，业务代码一行不改。

## 三个包的分工

| 包 | 内容 | 何时引用 |
| --- | --- | --- |
| `XiHan.Framework.SearchEngines.Abstractions` | 契约本体：`ISearchEngine`、请求/结果/索引定义类型。**无任何实现、无第三方依赖** | 只想依赖契约的类库项目 |
| `XiHan.Framework.SearchEngines` | `XiHanSearchEnginesModule` + `InMemorySearchEngine`（进程内兜底实现） | 应用宿主，必引 |
| `XiHan.Framework.SearchEngines.Elasticsearch` | `ElasticsearchSearchEngine` + `ElasticsearchOptions`。**没有模块类** | 生产接 Elasticsearch 时追加 |

## 契约是「交集」，不是「并集」

`ISearchEngine` 按「Elasticsearch 与 PostgreSQL 全文检索都能落地」的交集设计，签名里不出现任一后端的类型。这带来两条要接受的约束：

- **字段类型只有六种**：`Text`、`Keyword`、`Integer`、`Double`、`Boolean`、`DateTime`。分片、副本、分析器链这类后端独有的调优项不进抽象，由各实现从自己的配置节读。
- **过滤条件之间恒为「与」**。同字段多值用 `SearchFilterOperator.In` 表达；**跨字段的「或」不在契约范围内**，需要时由调用方拆成多次查询自己合并。

`ISearchEngine` 的九个方法：

| 方法 | 语义 |
| --- | --- |
| `IndexExistsAsync(index)` | 索引是否存在 |
| `CreateIndexAsync(definition)` | 幂等建索引，返回**本次是否真的创建了** |
| `DeleteIndexAsync(index)` | 幂等删索引，返回本次是否真的删除了 |
| `IndexAsync<T>(index, document)` | 写单个文档，标识已存在时**整体覆盖** |
| `IndexManyAsync<T>(index, documents)` | 批量写，返回写入条数 |
| `DeleteAsync(index, id)` | 按标识删文档 |
| `GetAsync<T>(index, id)` | 按标识取文档，不存在返回 `null` |
| `SearchAsync<T>(request)` | 检索，返回 `SearchResult<T>` |
| `RefreshAsync(index)` | 让此前的写入立即对检索可见 |

完整类型清单（`SearchRequest`、`SearchFilter`、`SearchSort`、`SearchHit<T>` 等各成员）见 [Abstractions 包](../packages/search-engines-abstractions)。

## 安装与启用

```bash
dotnet add package XiHan.Framework.SearchEngines
```

```csharp
[DependsOn(typeof(XiHanSearchEnginesModule))]
public class MyModule : XiHanModule { }
```

依赖上模块后即可无条件注入 `ISearchEngine`，此时拿到的是 `InMemorySearchEngine`。模块自身只做兜底注册：

```csharp
context.Services.TryAddSingleton<InMemorySearchEngine>();
context.Services.TryAddSingleton<ISearchEngine>(sp => sp.GetRequiredService<InMemorySearchEngine>());
```

::: warning 进程内实现能做什么、不能做什么
`InMemorySearchEngine` 面向**单机开发与自动化测试**：索引状态放在并发字典里，**进程重启即丢**，多实例部署时各进程各有一份、互不可见；关键字匹配是**大小写不敏感的子串包含**，命中几个字段就得几分，**不分词、没有相关度模型**。

用它验证「链路通不通」可以，验证「搜得准不准」不行。生产必须换实现。
:::

## 核心用法

三步：建索引 → 写文档 → 查。

```csharp
using XiHan.Framework.SearchEngines;
using XiHan.Framework.SearchEngines.Documents;
using XiHan.Framework.SearchEngines.Indexing;
using XiHan.Framework.SearchEngines.Querying;
using XiHan.Framework.SearchEngines.Results;

public class ArticleSearchService(ISearchEngine search)
{
    private const string Index = "articles";

    // 建索引：已存在时返回 false，不做任何事
    public Task<bool> EnsureIndexAsync()
    {
        return search.CreateIndexAsync(new SearchIndexDefinition(Index,
        [
            new SearchFieldDefinition("title", SearchFieldType.Text, Searchable: true),
            new SearchFieldDefinition("content", SearchFieldType.Text, Searchable: true),
            // 要排序/精确过滤的文本字段用 Keyword，不要用 Text
            new SearchFieldDefinition("tag", SearchFieldType.Keyword, Sortable: true),
            new SearchFieldDefinition("views", SearchFieldType.Integer, Sortable: true),
            new SearchFieldDefinition("publishTime", SearchFieldType.DateTime, Sortable: true),
        ])
        {
            Language = "zh"
        });
    }

    // 写入：标识已存在时整体覆盖
    public async Task IndexAsync(Article article)
    {
        await search.IndexAsync(Index, new SearchDocument<Article>(article.Id.ToString(), article));
    }

    public Task<int> IndexManyAsync(IEnumerable<Article> articles)
    {
        return search.IndexManyAsync(Index,
            articles.Select(a => new SearchDocument<Article>(a.Id.ToString(), a)));
    }

    // 检索
    public Task<SearchResult<Article>> SearchAsync(string keyword, int skip)
    {
        return search.SearchAsync<Article>(new SearchRequest(Index)
        {
            Keyword = keyword,
            Fields = ["title", "content"],
            Filters =
            [
                new SearchFilter("tag", SearchFilterOperator.In, values: ["dotnet", "vue"]),
                new SearchFilter("views", SearchFilterOperator.GreaterThanOrEqual, 100),
            ],
            Sorts = [SearchSort.ByScore],
            HighlightFields = ["title"],
            Skip = skip,
            Take = 20,
        });
    }
}
```

结果里 `Hits` 是当前页，`TotalCount` 是不受分页影响的命中总数；每个 `SearchHit<T>` 带 `Id`、`Document`、`Score` 和 `Highlights`（键为字段名）。

::: danger 字段名用序列化后的 JSON 名，不是 C# 属性名
两个实现都用 `JsonSerializerDefaults.Web` 序列化文档，属性名默认转成**小驼峰**。索引定义的字段名、`Filters` 的 `Field`、`Sorts` 的 `Field`、`Fields`、`HighlightFields` 全都要写 JSON 里的那个名字（或 `JsonPropertyName` 指定的名字）。

写成 C# 的 `PublishTime` 而不是 `publishTime`，过滤会**默默匹配不到任何文档**，不报错。
:::

字段名支持点分隔的嵌套路径，如 `author.name`。

## 关键机制

### 索引必须先建

两个实现都拒绝往不存在的索引写数据，抛 `InvalidOperationException`。

Elasticsearch 实现里这是**刻意拦截**：集群默认允许写入时自动建索引，但自动建索引走动态映射，本该是 `keyword` 的字段会被推断成 `text`，其上的精确过滤随即失效**且不报错**。实现先 `HEAD` 校验索引存在，校验结果按索引名缓存，每个索引只在首次写入时多一个 `HEAD` 请求。

所以应用启动或首次使用前，请显式调一次 `CreateIndexAsync`——它是幂等的，重复调用只返回 `false`。

### 写入可见性

契约规定：写入对检索可见的延迟由实现决定，需要立即可见时调 `RefreshAsync(index)`。

- 进程内实现写入即可见，`RefreshAsync` 是空操作。
- Elasticsearch 有刷新间隔，写完立刻查可能查不到；`RefreshAsync` 会打到 `_refresh`。

::: warning 不要在高频写入路径上每条都刷
刷新代价很高。只在「写完必须马上能查到」的场景（例如导入后立刻跳转结果页、集成测试的断言前）调一次。
:::

### 排序与相关度

`Sorts` 为空时按相关度降序。要显式按相关度排就用 `SearchSort.ByScore`（字段名常量是 `SearchSort.ScoreField`，即 `_score`）。

`Score` 只在按相关度检索时有意义——没有关键字时，进程内实现给所有文档 0 分。

### 批量写入的失败判定

Elasticsearch 的批量接口整体返回 200 的同时可能有个别条目失败。实现会解析响应体的 `errors` 标志，有失败条目就抛异常并带上服务端返回内容——不会把部分失败当成全部成功。

### 索引名前缀

`ElasticsearchOptions.IndexPrefix` 由实现内部统一拼接。业务代码传的始终是**不带前缀的逻辑名**，多环境或多租户共用一个集群时靠它隔离。

## 切到 Elasticsearch

```bash
dotnet add package XiHan.Framework.SearchEngines.Elasticsearch
```

::: danger 本包没有模块类，也没有 AddXxx 扩展方法
框架的约定注册只扫描**模块所在的程序集**。这个包不含模块类，它的程序集不会被扫描，`ElasticsearchSearchEngine` 上的 `ISingletonDependency` 不会自动生效——必须在你自己的模块里手写注册。

而且必须用 `Replace`：`XiHanSearchEnginesModule` 已经注册过 `ISearchEngine`，此时再 `TryAdd` 会被**静默忽略**，表现为「配置全填了，跑起来还是进程内实现」。
:::

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.SearchEngines;
using XiHan.Framework.SearchEngines.Elasticsearch;
using XiHan.Framework.SearchEngines.Elasticsearch.Options;

[DependsOn(typeof(XiHanSearchEnginesModule))]
public class MySearchModule : XiHanModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var services = context.Services;

        services.Configure<ElasticsearchOptions>(
            services.GetConfiguration().GetSection(ElasticsearchOptions.SectionName));

        services.TryAddSingleton<ElasticsearchSearchEngine>();
        services.Replace(ServiceDescriptor.Singleton<ISearchEngine>(
            sp => sp.GetRequiredService<ElasticsearchSearchEngine>()));
    }
}
```

`[DependsOn(typeof(XiHanSearchEnginesModule))]` 是必需的：它保证你的模块在依赖图里排在被覆盖者之后，`Replace` 才能盖住那份兜底注册。

### 实现取舍

连接、认证与重试交给官方客户端的传输层，**请求体与响应解析由实现以 JSON 直接构造**，不使用客户端的强类型查询 DSL——那套 DSL 在大版本间变动频繁，焊进实现会让本包的可用性跟着客户端版本走。

## 两个实现的已知差异

同一份业务代码在两个实现上行为并不完全一致。写代码时按下表取交集，才能真正做到「切换实现不改代码」。

| 主题 | 进程内实现 | Elasticsearch 实现 |
| --- | --- | --- |
| 关键字匹配 | 大小写不敏感的子串包含，命中字段数即得分 | `multi_match`，走引擎的分词与相关度模型 |
| `Fields` 为空时 | 取索引定义中 `Searchable: true` 的字段 | 不带 `fields`，匹配全部字段 |
| 可排序字段 | 任意字段，字段缺失按空串参与排序 | `Text` 字段默认**不可排序**，需排序请定义为 `Keyword` |
| 写入可见性 | 立即可见，`RefreshAsync` 为空操作 | 有刷新间隔，需要立即可见须显式刷新 |
| 高亮 | 把整段字段文本里的关键字包上 `<em>`，返回整段 | 返回引擎生成的高亮**片段** |
| 读/删不存在的索引 | `GetAsync` / `DeleteAsync` / `SearchAsync` 均抛 `InvalidOperationException` | `GetAsync` 返回 `null`、`DeleteAsync` 返回 `false`、`SearchAsync` 抛异常 |
| 索引名大小写 | 不区分 | 由集群规则决定，别依赖大小写差异 |
| 持久性 | 进程内，重启即丢，多实例不共享 | 集群持久化 |

::: tip 用进程内实现写测试的正确姿势
断言「查得到 / 过滤对不对 / 分页对不对」是可靠的；断言「排在第几位」不可靠——相关度模型不同。
:::

## 配置

只有 Elasticsearch 实现有配置节：`XiHan:SearchEngines:Elasticsearch`（常量 `ElasticsearchOptions.SectionName`）。进程内实现无任何配置项。

```json
{
  "XiHan": {
    "SearchEngines": {
      "Elasticsearch": {
        "Uri": "http://127.0.0.1:9200",
        "ApiKey": "从环境变量或密钥库注入",
        "IndexPrefix": "prod-",
        "RequestTimeoutSeconds": 30,
        "NumberOfShards": 3,
        "NumberOfReplicas": 1
      }
    }
  }
}
```

最常调的几项：

| 键 | 默认值 | 说明 |
| --- | --- | --- |
| `Uri` | `http://localhost:9200` | 节点地址 |
| `ApiKey` | 空 | **优先于**用户名密码；生产推荐 |
| `UserName` / `Password` | 空 | 基础认证 |
| `IndexPrefix` | 空 | 索引名前缀，多环境/多租户隔离用 |
| `AllowUntrustedCertificate` | `false` | 跳过服务端证书校验，**仅限本地自签名证书的开发环境** |

全部配置项见 [Elasticsearch 包](../packages/search-engines-elasticsearch)。

::: warning 凭据不要写进 appsettings.json
`ApiKey` / `Password` 从环境变量或密钥库注入。`AllowUntrustedCertificate` 在生产打开等同于放弃传输层身份校验。
:::

## 常见问题

| 现象 | 原因 |
| --- | --- |
| 配置全填了，跑起来还是进程内实现 | 用 `TryAdd` 注册 Elasticsearch 实现，被静默忽略；必须 `Replace` |
| 引用了 Elasticsearch 包但注入不到它的实现 | 该包无模块类，程序集不参与约定注册扫描，必须自己 `TryAddSingleton` |
| 写入报「索引 'xxx' 不存在，请先创建索引」 | 没先调 `CreateIndexAsync`，两个实现都不允许隐式建索引 |
| 过滤条件匹配不到任何文档 | 字段名写成了 C# 属性名；应为序列化后的小驼峰名 |
| 排序在 Elasticsearch 上报错 | 对 `Text` 字段排序；改成 `SearchFieldType.Keyword` |
| 刚写完立刻查不到 | Elasticsearch 刷新间隔；需要立即可见时调 `RefreshAsync` |
| 改了字段类型但映射没变 | `CreateIndexAsync` 对已存在的索引直接返回 `false`，不会改映射；需先 `DeleteIndexAsync` 再重建并回灌 |
| 重启后索引空了 | 还在用进程内实现，数据本就不持久化 |
| 需要「A 命中 或 B 命中」 | 契约不支持跨字段「或」，拆成多次查询自行合并 |
| 构造 `SearchFilter` 抛 `ArgumentException` | `In` 必须给非空 `values`；除 `In`/`Exists` 外必须给 `value` |
| 构造 `SearchRequest` 抛 `ArgumentOutOfRangeException` | `Take` 必须大于 0（默认 20），`Skip` 不能为负 |

## 下一步

- [依赖注入](./dependency-injection)：`Replace` 与 `TryAdd` 的注册语义
- [配置与选项](./configuration)：选项绑定与配置节约定
- [模块系统](./modularity)：`DependsOn` 如何决定注册顺序
- [SearchEngines.Abstractions 包](../packages/search-engines-abstractions)：契约的完整类型与成员
- [SearchEngines 包](../packages/search-engines)：进程内实现细节
- [SearchEngines.Elasticsearch 包](../packages/search-engines-elasticsearch)：全部配置项表
