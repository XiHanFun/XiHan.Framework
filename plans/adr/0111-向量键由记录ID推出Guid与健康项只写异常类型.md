# ADR-0111 知识库向量键由记录 ID 确定性推出 Guid，健康项的 reason 只写异常类型

## 状态

已采纳（`spec/rfcs/0022-ai.md` 随之修订）。修订 ADR-0110 第二节「`reason` 保留连接器的异常消息」。

## 日期

2026-11-04

本 ADR 记录第二十四批回灌（`ac-batch-backlog.md`，用户定「全按推荐」）的两项决定，每项一节。BasicApp N4 第二批的真实 Qdrant 接入在等第一项。

## 一、向量键由记录 ID 确定性推出 Guid

### 背景

ADR-0110 的 Docker 实测发现，`VectorDataKnowledgeStore` 在 Qdrant 上写不进去：摄入与检索都返回 `AI_UNAVAILABLE_002`，原因是 `Only ulong and Guid keys are supported.`。

- 向量记录的键是字符串 `<documentId>#<chunkIndex>`，而 Qdrant 连接器只认 `ulong` 与 `Guid`。
- 一致性套件之前只跑进程内替身，替身接受字符串键，所以一直没有暴露。

### 备选

1. **键改成由记录 ID 确定性推出的 `Guid`，记录 ID 另存字段**（采纳）。
2. **键类型做成选项，按连接器选 `string` 或 `Guid`**：存储里的泛型要分两路，配错了到运行时才发现，多一个配置键换不来别的好处。否决。
3. **维持 `string`，不支持 Qdrant**：BasicApp 用 Qdrant。否决。
4. **键用随机 `Guid`**：重写入不再覆盖旧记录，得先查后写。否决。

### 决策

1. `KnowledgeVectorRecord` 的键是 `Guid Key`，原来的 `Id` 改名 `RecordId`，成为普通数据字段。不留 `Id`：有的连接器把名为 `id` 的字段当保留字段。
2. 推导算法写进 RFC-0022：记录 ID 的 UTF-8 字节做 SHA-256，取前 16 字节，按 RFC 9562 写上版本 8 与变体位，按大端读成 UUID。
   - 不用 UUID v5：它基于 SHA-1，弱哈希分析器会报警。这里不需要与外部互通，版本 8 正是给自定义算法留的。
   - 碰撞空间是 122 位，一个集合里的块数远小于它，不做额外处理。
3. 对外不变：`KnowledgeRecord.Id`、`RetrievedChunk.Id` 仍是 `<documentId>#<chunkIndex>`。
4. 按文档删除与裁剪本来就按过滤条件取键再删，只是键的类型变了。
5. 用真实 Qdrant 把这件事固定下来：`AI.VectorData.Tests` 增加 `KnowledgeStoreConformance` 的 Qdrant 子类与摄入、检索用例，由 `XIHAN_TEST_QDRANT` 门控，CI 增加 Qdrant 服务。连接器只有测试项目引用，框架包不依赖。
6. 测试用 `CommunityToolkit.VectorData.Qdrant` 1.0.1。改键之后，`Microsoft.SemanticKernel.Connectors.Qdrant` 1.74.0-preview（这个包的最新版）写入能通过，搜索却报找不到 `VectorSearchFilter` 类型：它按 VectorData 10.1.0 编译，这个类型在框架依赖的 10.11.0 里已经没有了。官方文档的 .NET 连接器已迁到 `CommunityToolkit.VectorData.*`。

### 影响

- 已有集合的键形状不同，要重建。还没有发布，不做迁移。
- 连接器要接受 `Guid` 键。按官方文档，Qdrant、PostgreSQL、Redis、Azure AI Search 的 .NET 连接器都接受；不接受的连接器，写入与搜索返回 `AI_UNAVAILABLE_002`，`reason` 是连接器的消息。
- 公开 API：`KnowledgeVectorRecord` 去掉 `Id`，新增 `Key`、`RecordId`、`KeyOf(string)`。
- 接 Qdrant 的应用要引用 `CommunityToolkit.VectorData.Qdrant`，不能用 `Microsoft.SemanticKernel.Connectors.Qdrant`。

## 二、健康项的 reason 只写异常类型

### 背景

ADR-0110 让健康项的 `reason` 沿用连接器的异常消息，理由是 Qdrant 的消息不含主机与端口。这个 Provider 接任意连接器，Qdrant 的消息不含地址，不代表别的连接器也不含。

### 决策

1. 探针遇到连接器抛出时，`reason` 是异常类型的全名，做法同 ADR-0108。错误码与消息键不变。
2. 完整异常进日志，记在 `VectorDataKnowledgeStore` 这个类别下：
   - 这一种失败第一次记 Warning，带异常；
   - 同一种失败持续时记 Debug，带异常：向量库停着时，每个探测周期一条带堆栈的 Warning 会淹没日志；
   - 探测恢复后再失败，或异常类型变了，再记 Warning。
3. 为此 `VectorDataKnowledgeStore` 的构造增加 `ILoggerFactory`。经容器解析不受影响。

### 影响

- 健康快照里 `ai.vectordata.store` 的 `reason` 从连接器消息变成类型全名，如 `Microsoft.Extensions.VectorData.VectorStoreException`。排查时看日志。
- 存储操作（写入、删除、裁剪、搜索）返回的 `AI_UNAVAILABLE_002` 不在这项决定之内，`reason` 仍是异常消息。
