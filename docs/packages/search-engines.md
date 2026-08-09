# XiHan.Framework.SearchEngines

> 搜索引擎默认实现包：提供**进程内**的 `ISearchEngine` 实现作为兜底。接入真实搜索引擎请引用对应的实现包。

- **NuGet**：`XiHan.Framework.SearchEngines`
- **模块类**：`XiHanSearchEnginesModule`
- **所在层**：基础设施层
- **关键依赖**：`XiHan.Framework.Core`、`XiHan.Framework.SearchEngines.Abstractions`。**无第三方依赖**

## 概述

本包做两件事：

1. 注册 `InMemorySearchEngine` 作为 `ISearchEngine` 的**默认兜底实现**——开发期不装 Elasticsearch 也能跑通检索链路，测试里不需要真实引擎。
2. 让契约始终有**第二个实现**来校验自己——只有一个实现的抽象无法证明自己没有泄漏该实现的概念。

::: warning 进程内实现的定位
`InMemorySearchEngine` 面向**单机开发与自动化测试**：数据只存在于当前进程，**重启即丢**，且**不做分词与相关度模型**。生产环境必须引用具体搜索引擎的实现包。
:::

## 何时使用

- 开发/测试环境想跑通检索链路又不想起一个 Elasticsearch。
- 作为兜底注册，让业务代码可以无条件注入 `ISearchEngine`。
- 需要 `ISearchEngine` 的契约测试基线。

## 安装与启用

```bash
dotnet add package XiHan.Framework.SearchEngines
```

```csharp
[DependsOn(typeof(XiHanSearchEnginesModule))]
public class MyModule : XiHanModule { }
```

模块的 `ConfigureServices` 只做两行注册（**`TryAdd` 语义**）：

```csharp
context.Services.TryAddSingleton<InMemorySearchEngine>();
context.Services.TryAddSingleton<ISearchEngine>(sp => sp.GetRequiredService<InMemorySearchEngine>());
```

接入真实引擎时，由实现包（或你自己）以 **`Replace`** 覆盖 `ISearchEngine` 的注册——`TryAdd` 一个新实现会被静默忽略。

## 主要 API / 类型

| 类型 | 说明 |
| --- | --- |
| `XiHanSearchEnginesModule` | 模块入口，注册进程内实现作为兜底 |
| `InMemorySearchEngine` | 进程内 `ISearchEngine` 实现（`ISingletonDependency`），索引状态存于并发字典 |

检索契约本身（`ISearchEngine`、`SearchRequest`、`SearchFilter`、`SearchIndexDefinition` 等）在 [Abstractions 包](./search-engines-abstractions)。

## 使用示例

```csharp
using XiHan.Framework.SearchEngines;
using XiHan.Framework.SearchEngines.Documents;
using XiHan.Framework.SearchEngines.Indexing;
using XiHan.Framework.SearchEngines.Querying;

public class ArticleSearchService(ISearchEngine search)
{
    private const string Index = "articles";

    public async Task EnsureIndexAsync()
    {
        // 幂等：已存在时返回 false，不做任何事
        await search.CreateIndexAsync(new SearchIndexDefinition(Index,
        [
            new SearchFieldDefinition("title",   SearchFieldType.Text,    Searchable: true),
            new SearchFieldDefinition("tag",     SearchFieldType.Keyword, Sortable: true),
            new SearchFieldDefinition("views",   SearchFieldType.Integer, Sortable: true),
        ]) { Language = "zh" });
    }

    public async Task IndexAsync(Article article)
    {
        // 标识已存在时整体覆盖
        await search.IndexAsync(Index, new SearchDocument<Article>(article.Id.ToString(), article));
        // 需要写入立即可见时刷新
        await search.RefreshAsync(Index);
    }

    public Task<SearchResult<Article>> SearchAsync(string keyword)
    {
        return search.SearchAsync<Article>(new SearchRequest(Index)
        {
            Keyword = keyword,
            Fields = ["title"],
            Filters = [new SearchFilter("tag", SearchFilterOperator.In, values: ["dotnet", "vue"])],
            Sorts = [SearchSort.ByScore],
            HighlightFields = ["title"],
            Take = 20,
        });
    }
}
```

## 注意事项

- 进程内实现**不分词**：关键字检索是计数式匹配，相关度与真实引擎不同。用它验证链路，不要用它验证搜索效果。
- 多实例部署下每个进程各有一份索引，互不可见。
- 切换到真实引擎时业务代码**一行不用改**——这正是契约设计的目的。

## 依赖模块

- [XiHan.Framework.SearchEngines.Abstractions](./search-engines-abstractions) — 检索契约。
- [XiHan.Framework.Core](./core) — 模块化与依赖注入基础。

## 相关模块

- [XiHan.Framework.SearchEngines.Elasticsearch](./search-engines-elasticsearch) — 生产用的 Elasticsearch 实现。
- [XiHan.Framework.Data](./data) — 数据访问基础设施，通常与索引同步配合使用。
