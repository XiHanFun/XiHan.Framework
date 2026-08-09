# XiHan.Framework.SearchEngines.Abstractions

> 搜索引擎抽象包：索引、文档、检索请求与结果的统一契约。**不含任何搜索引擎实现，零第三方依赖。**

- **NuGet**：`XiHan.Framework.SearchEngines.Abstractions`
- **模块类**：无（纯契约包，直接引用即可）
- **所在层**：应用层（抽象）
- **关键依赖**：无

## 概述

`ISearchEngine` 是框架的搜索契约。它按「**Elasticsearch 与 PostgreSQL 全文检索都能落地**」的交集设计——签名中**不出现任一后端的类型**，因此换实现不需要改调用方。

投递语义：索引与删除均为**幂等的按标识覆盖/移除**；写入对检索可见的延迟由实现决定，需要立即可见时调 `RefreshAsync`。

## 何时使用

- 业务代码只想依赖检索能力，不想把 Elasticsearch 客户端类型渗进去。
- 想自己接一个后端（PostgreSQL 全文检索、Meilisearch、OpenSearch…）：实现 `ISearchEngine` 一个接口即可。
- 测试里想用进程内实现替换真实引擎。

## 安装

```bash
dotnet add package XiHan.Framework.SearchEngines.Abstractions
```

契约包没有模块类，引用后直接用类型即可。要拿到可注入的 `ISearchEngine`，引 [XiHan.Framework.SearchEngines](./search-engines)（进程内实现）或 [XiHan.Framework.SearchEngines.Elasticsearch](./search-engines-elasticsearch)。

## 主要 API / 类型

### `ISearchEngine`

| 方法 | 说明 |
| --- | --- |
| `IndexExistsAsync(index)` | 索引是否存在 |
| `CreateIndexAsync(definition)` | 创建索引，已存在时不做任何事；返回**本次是否实际创建** |
| `DeleteIndexAsync(index)` | 删除索引，不存在时不做任何事；返回**本次是否实际删除** |
| `IndexAsync<TDocument>(index, document)` | 写入单个文档，标识已存在时**整体覆盖** |
| `IndexManyAsync<TDocument>(index, documents)` | 批量写入，返回实际写入数 |
| `DeleteAsync(index, id)` | 按标识删除文档 |
| `GetAsync<TDocument>(index, id)` | 按标识取文档，不存在返回 `null` |
| `SearchAsync<TDocument>(request)` | 检索，返回 `SearchResult<TDocument>` |
| `RefreshAsync(index)` | 使此前的写入**立即对检索可见** |

### 索引定义

| 类型 | 说明 |
| --- | --- |
| `SearchIndexDefinition` | 索引定义：`Name`、`Fields`、`Language`（分词语言，可选） |
| `SearchFieldDefinition` | 字段定义（record）：`Name`、`Type`、`Searchable`（默认 `false`）、`Sortable`（默认 `false`） |
| `SearchFieldType` | `Text`（分词全文）/ `Keyword`（不分词精确）/ `Integer` / `Double` / `Boolean` |

### 文档

| 类型 | 说明 |
| --- | --- |
| `SearchDocument<TDocument>` | 「标识 + 文档体」的载体，索引与批量索引都用它 |

### 检索请求

`SearchRequest`（构造时传索引名，其余用 `init` 属性）：

| 属性 | 说明 |
| --- | --- |
| `Index` | 索引名 |
| `Keyword` | 关键字，为空时**只按过滤条件检索** |
| `Fields` | 关键字检索的字段范围 |
| `Filters` | `SearchFilter[]` 过滤条件 |
| `Sorts` | `SearchSort[]` 排序 |
| `HighlightFields` | 需要高亮的字段 |
| `Skip` / `Take` | 分页 |

`SearchFilter(field, op, value, values)`，`SearchFilterOperator` 取值：`Equal` / `NotEqual` / `In` / `GreaterThan` / `GreaterThanOrEqual` / `LessThan` / `LessThanOrEqual` / `Exists`。

> `In` 必须用 `values` 传集合，且不能为空——构造函数会直接校验并抛异常。

`SearchSort(field, direction)`，方向为 `SearchSortDirection.Ascending` / `Descending`；字段取 `SearchSort.ScoreField`（`"_score"`）表示按相关度排序，`SearchSort.ByScore` 是「相关度降序」的现成实例。

### 结果

| 类型 | 说明 |
| --- | --- |
| `SearchResult<TDocument>` | `Hits`（命中列表）+ `TotalCount`（总数），另有现成的 `Empty` |
| `SearchHit<TDocument>` | 单条命中（record）：`Id`、`Document`、`Score`（**未按相关度检索时为 0**）、`Highlights`（键为字段名） |

## 注意事项

- 契约是**交集设计**：能力更强的后端（如 Elasticsearch 的聚合、复杂 DSL）不在这层暴露，需要时直接用该后端的客户端。
- `CreateIndexAsync` / `DeleteIndexAsync` / `DeleteAsync` 都返回「本次是否实际发生变化」，方便写幂等的初始化代码。
- 排序字段的可用性由后端决定，见 [Elasticsearch 实现](./search-engines-elasticsearch#与契约的已知差异)。

## 相关模块

- [XiHan.Framework.SearchEngines](./search-engines) — 进程内实现（默认兜底）。
- [XiHan.Framework.SearchEngines.Elasticsearch](./search-engines-elasticsearch) — Elasticsearch 实现。
