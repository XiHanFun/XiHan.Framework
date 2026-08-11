# XiHan.Framework.SearchEngines.Elasticsearch

> 搜索引擎契约的 **Elasticsearch 实现**：连接与认证交给官方客户端传输层，请求体与响应解析以 JSON 直接构造。

- **NuGet**：`XiHan.Framework.SearchEngines.Elasticsearch`
- **模块类**：**无**（只提供实现类与选项，注册方式见下）
- **所在层**：基础设施层
- **关键依赖**：`Elastic.Clients.Elasticsearch`（9.x，Elasticsearch 官方 .NET 客户端）；框架内部 `XiHan.Framework.SearchEngines`

## 概述

`ElasticsearchSearchEngine` 实现 [`ISearchEngine`](./search-engines-abstractions#isearchengine)，是生产环境的推荐实现。

设计上的一个刻意取舍：**连接、认证与重试交给官方客户端的传输层，但请求体与响应解析由本类以 JSON 直接构造**，不使用客户端的强类型查询 DSL。原因是那套 DSL 在大版本间变动频繁，把它焊进实现会让本包的可用性跟着客户端版本走；而 Elasticsearch 的 REST 请求体格式在同一大版本内是稳定契约。

## 何时使用

- 生产环境需要真正的全文检索、分词与相关度排序。
- 已有 Elasticsearch 集群，希望业务代码只依赖 `ISearchEngine` 契约。

## 安装与注册

```bash
dotnet add package XiHan.Framework.SearchEngines.Elasticsearch
```

::: warning 需要自行注册
本包**不含模块类，也没有 `AddXxx` 扩展方法**。引用后要自己绑定选项并覆盖 `ISearchEngine` 的注册——`XiHanSearchEnginesModule` 已用 `TryAdd` 注册了进程内实现，**必须用 `Replace` 覆盖**，`TryAdd` 会被静默忽略。
:::

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;
using XiHan.Framework.SearchEngines;
using XiHan.Framework.SearchEngines.Elasticsearch;
using XiHan.Framework.SearchEngines.Elasticsearch.Options;

public override void ConfigureServices(ServiceConfigurationContext context)
{
    var services = context.Services;
    var configuration = services.GetConfiguration();

    services.Configure<ElasticsearchOptions>(
        configuration.GetSection(ElasticsearchOptions.SectionName));

    services.TryAddSingleton<ElasticsearchSearchEngine>();
    services.Replace(ServiceDescriptor.Singleton<ISearchEngine>(
        sp => sp.GetRequiredService<ElasticsearchSearchEngine>()));
}
```

模块需要 `[DependsOn(typeof(XiHanSearchEnginesModule))]`，且你的模块在依赖图里排在它之后（依赖它即可保证），`Replace` 才能覆盖到它的注册。

## 配置

配置节 `XiHan:SearchEngines:Elasticsearch`（`ElasticsearchOptions.SectionName`）：

| 字段 | 类型 | 默认值 | 含义 |
| --- | --- | --- | --- |
| `Uri` | `string` | `"http://localhost:9200"` | 集群地址 |
| `UserName` / `Password` | `string?` | `null` | 基础认证 |
| `ApiKey` | `string?` | `null` | API Key 认证（与用户名密码二选一） |
| `IndexPrefix` | `string` | `""` | 索引名前缀，多环境/多租户共用集群时隔离用 |
| `RequestTimeoutSeconds` | `int` | `30` | 请求超时 |
| `AllowUntrustedCertificate` | `bool` | `false` | 是否允许不受信任的证书（**仅限开发环境**） |
| `NumberOfShards` | `int` | `1` | 创建索引时的分片数 |
| `NumberOfReplicas` | `int` | `1` | 创建索引时的副本数 |

```json
{
  "XiHan": {
    "SearchEngines": {
      "Elasticsearch": {
        "Uri": "http://127.0.0.1:9200",
        "ApiKey": "从环境变量或密钥库注入",
        "IndexPrefix": "prod-",
        "NumberOfShards": 3,
        "NumberOfReplicas": 1
      }
    }
  }
}
```

## 与契约的已知差异

契约是「Elasticsearch 与 PostgreSQL 全文检索的交集」，落到 Elasticsearch 上有两处行为差异，写代码时要知道：

1. **排序字段须为 `Keyword` / 数值 / 日期 / 布尔**。`Text` 字段在 Elasticsearch 上默认**不可排序**——需要排序的文本字段请定义成 `SearchFieldType.Keyword`。
2. **关键字检索走 `multi_match`**，其相关度模型与进程内实现的计数式打分不同。用进程内实现验证链路可以，验证排序效果不行。

## 使用示例

业务代码与用进程内实现时**完全一致**——只依赖 `ISearchEngine`：

```csharp
public class ArticleSearchService(ISearchEngine search)
{
    public Task<SearchResult<Article>> SearchAsync(string keyword)
        => search.SearchAsync<Article>(new SearchRequest("articles")
        {
            Keyword = keyword,
            Fields = ["title", "content"],
            Sorts = [SearchSort.ByScore],
            HighlightFields = ["title"],
            Take = 20,
        });
}
```

索引定义时注意上面第 1 条差异：

```csharp
await search.CreateIndexAsync(new SearchIndexDefinition("articles",
[
    new SearchFieldDefinition("title",   SearchFieldType.Text,    Searchable: true),
    // 需要排序 → 用 Keyword，不要用 Text
    new SearchFieldDefinition("tag",     SearchFieldType.Keyword, Sortable: true),
    new SearchFieldDefinition("views",   SearchFieldType.Integer, Sortable: true),
]) { Language = "zh" });
```

## 注意事项

- 写入对检索可见有延迟（Elasticsearch 的 refresh 间隔），需要立即可见时显式调 `RefreshAsync(index)`——**不要在高频写入路径上每条都刷**，代价很高。
- `IndexPrefix` 在实现内部统一拼接，业务代码传的索引名是**不带前缀**的逻辑名。
- `AllowUntrustedCertificate` 只应在开发环境打开。
- 生产建议用 `ApiKey` 而非用户名密码，且从环境变量或密钥库注入，不要写进 `appsettings.json`。

## 依赖模块

- [XiHan.Framework.SearchEngines](./search-engines) — 默认实现包（本包覆盖其 `ISearchEngine` 注册）。
- [XiHan.Framework.SearchEngines.Abstractions](./search-engines-abstractions) — 检索契约（经上者传递引用）。
- 第三方：`Elastic.Clients.Elasticsearch` 9.x。

## 相关模块

- [XiHan.Framework.AI](./ai) — 知识库 RAG 走的是向量库（Qdrant），与全文检索是两条不同的链路。
