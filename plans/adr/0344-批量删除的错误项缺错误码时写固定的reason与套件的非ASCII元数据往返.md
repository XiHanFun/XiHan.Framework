# ADR-0344 批量删除的错误项缺错误码时 `reason` 写固定文字，一致性套件补上非 ASCII 元数据值的往返

## 状态

已采纳（规范 RFC-0012「S3 Provider」「一致性」已改；`ObjectStorage.S3` 的 `S3BlobStore` 批量删除已改；`ObjectStorage.Conformance` 的 `BlobStoreConformance` 新增用例 16；错误码与消息键不变）。

## 日期

2026-11-27

## 背景

ADR-0333 之后留下两处，仓库所有者已拍板：

1. **批量删除的错误项没有 `<Code>`**：`DeleteObjects` 的应答（或 `DeleteObjectsException` 带的 `Response`）里某个错误项带着这一批里的键而没有错误码时，`S3BlobStore` 照样只报那一项失败，但逐项结果的 `reason` 是 `null`——`Unavailable(error.Code)` 原样取了缺失的码。拍板：照样只报那一项，`reason` 写固定的英文文字、不为空；内存替身与真实 SDK（`RecordingHttpListener` 回带错误项的 XML）各一条用例。
2. **一致性套件没有非 ASCII 的元数据值**：`BlobStoreConformance` 的元数据值只有 ASCII（ADR-0322 备选 25：S3 当时写不出非 ASCII 的值）。ADR-0333 让 S3 照 RFC 2047 编解码之后两边都能过（ADR-0333「没有解决的」）。拍板：补进套件，文件系统存储与 S3（替身与 RustFS 门控）都要过，编号照现有接着排，各 Provider 的一致性测试类都要跑到。

## 查明的事实

- AWS SDK for .NET 4.0（`AWSSDK.S3` 4.0.104.8）解组 `DeleteResult` 的 `<Error>`：缺 `<Code>` 时 `DeleteError.Code` 为 `null`，`<Code></Code>` 时为空串（回环上的 `RecordingHttpListener` 实测）；有错误项时照旧抛 `DeleteObjectsException`（ADR-0333）。
- `BlobStoreConformance` 有 15 个用例；继承它的一致性测试类只有两个：`FileSystemBlobStoreConformanceTests` 与 `S3BlobStoreConformanceTests`（对真实服务，未设置 `XIHAN_TEST_S3` 时跳过）。S3 的内存替身 `FakeS3Client` 只记键、不认整读，跑不了整套用例。

## 备选

### 缺错误码的 `reason`

1. **固定文字 `DeleteObjects returned an error without a usable Code`**：采纳。写法与同一处已有的 `DeleteObjects returned an error without a usable Key`（ADR-0313）一致：请求名开头、点名缺的元素、不带句号、不带应答里的值（错误项的 `<Message>` 照旧不进错误）。
2. **「没有」的口径**：`<Code>` 缺失、为空或只有空白（`string.IsNullOrWhiteSpace`）。S3 的错误码是不含空白的标识，只有空白的码同样分不出原因；键的判定（ADR-0313）只看缺失与为空，因为只有空白的键在 S3 里可以是合法的键。
3. **记 Warning**：采纳，这一批只记一条（与认不出键的错误项同一条日志、同一个原因文字）：错误码是 S3 的错误项必有的内容，缺了说明服务的应答不合规，日志是运维看得到的痕迹；逐项的结果仍是那一项失败，不像认不出键时整批报错。
4. **整批报错**：否决（拍板）。键认得出，失败的就是那一项，别的项删成与否不受影响。
5. **`reason` 为空串或某个占位码（`Unknown`）**：否决。空串同 `null` 一样看不出原因；占位码会被当成 S3 的错误码。

### 套件的非 ASCII 元数据值

6. **新增用例 16 `Metadata_WithNonAsciiValues_RoundTrips`**：采纳。一组值：中文（`2026 年度报告`）、平面以外的字符（`U+1F600 smile`）、带变音符号的拉丁字母（`café Ünïcödé`）、西里尔字母（`Привет`）、超过 45 个 UTF-8 字节的长值（100 个「中」，S3 上拆成 7 个 encoded-word）、非 ASCII 的文字里夹着 encoded-word（`说明 =?UTF-8?B?5Lit?=`，S3 整个编码、读回原样）。核对写入结果、整读、`Head` 与把读出的元数据写回另一个键都得到同一组值；实现了 `IBlobMultipartStore` 的再走一遍发起、上传一片与完成，完成结果与 `Head` 都是这组值。名与值合计约 410 个 UTF-8 字节，远在 2048 字节上限之内；不放整个是 encoded-word 形状的 ASCII 值（S3 写入时拒收，ADR-0343）与首尾的空格（不是非 ASCII，已由 S3 自己的用例覆盖）。
7. **S3 的替身**：用例 16 另由 `S3BlobStoreStubConformanceTests` 在内存替身上跑：`FakeS3Client` 改为单次写入记下内容、内容类型与 SDK 请求里的自定义元数据（分片上传记发起时的内容类型与元数据），HEAD 与新认的整读原样交回；不公开的嵌套类 `StubSuite` 继承 `BlobStoreConformance`，测试方法逐条调它的用例 16（测试框架不把不公开的类当测试类，整套用例不会在替身上跑）。替身交回的是 Provider 编码之后的头值，Provider 再解码，所以它核对的是经 SDK 的元数据集合编码与解码互逆；线上的写法与读法由 `S3MetadataWireTests` 核对，真实服务上的往返由门控的 `S3BlobStoreConformanceTests` 核对。
8. **把替身补全、让它跑整套用例**：否决。列举、范围读取、分片内容的拼接与并发的条件写入都要模拟，替身会变成又一个要维护的存储实现，测的是替身而不是 Provider。
9. **把用例 16 拆成按值的 `[Theory]`**：没有采用。套件的用例都是 `[Fact]`，按值拆开会让每个存储多建十几个对象、多发几十个请求；一个用例里核对一组值，失败时 `Assert.Equal` 列出哪一项不同。

## 决策

### 规范

1. RFC-0012「S3 Provider」（MINOR，写明以前不作规定的）：批量删除里单个键的失败只写 S3 的错误码；错误项带着这一批里的键却没有可用的错误码（`<Code>` 缺失、为空或只有空白）时照样只有那一项失败，`reason` 是固定的 `DeleteObjects returned an error without a usable Code`，这一批记一条 Warning。
2. RFC-0012「一致性」（MINOR）：用例补上非 ASCII 的自定义元数据值的往返。状态行追加「同日第四次修订」，「对应 ADR」加本 ADR。

### 实现

3. `S3BlobStore.RecordDeleteErrors`：认得出键的错误项没有可用的错误码时 `reason` 取常量 `DeleteErrorCodeMissingReason`，这一批有这样的错误项时记一条 `LogAnswerUnusable`。
4. `BlobStoreConformance`（`Preview`）：新增公开的用例 16 `Metadata_WithNonAsciiValues_RoundTrips`（`PublicAPI.Unshipped.txt` 已同步）。
5. 一致性包 README 与 S3 包 README 写明。

## 理由

- 逐项结果的 `reason` 是调用方与日志唯一能看的原因，`null` 既不说明是服务的应答缺了码，也会让按原因分类的代码遇到空值。
- 非 ASCII 的元数据值是契约允许的值（RFC-0012「标识」），两种存储都该原样往返；放进一致性套件，以后的 Provider 也要过。

## 影响

- **公开 API**：`BlobStoreConformance.Metadata_WithNonAsciiValues_RoundTrips`（新增）。错误码、消息键、配置键不变。
- **行为**（`ObjectStorage.S3`）：批量删除的错误项没有错误码时，那一项的 `reason` 由 `null` 改为固定文字，另记一条 Warning；失败的项与错误码不变。
- **一致性**：继承 `BlobStoreConformance` 的 Provider 多一个用例；参考实现的文件系统存储与 S3（替身、RustFS 门控）都通过。第三方 Provider 若写不出非 ASCII 的元数据值会在这个用例上失败——这正是契约要求的。
- **规范**（规范仓库已改，指针待更新）：见「决策」第 1–2 条；不新增向量，SDK 不受影响。
- **测试**：
  - `S3DeleteResponseTests` 补两条：替身上错误项的 `Code` 为 `null`、空串、只有空白（3 例）时只有那一项失败、`reason` 是固定文字、只记一条 Warning、别的错误项照旧报 S3 的码；线上真实 SDK 解出缺 `<Code>` 与 `<Code></Code>` 的错误项（2 例）同样如此，SDK 不重发。
  - 用例 16 在 `FileSystemBlobStoreConformanceTests`、`S3BlobStoreStubConformanceTests`（新增，替身）与 `S3BlobStoreConformanceTests`（门控）上跑；本机对 RustFS 1.0.6（本机镜像的临时容器，测完即删）跑全部 `ObjectStorage.S3.Tests`：483 条，0 失败，只跳过 `XIHAN_TEST_S3_UNCONDITIONAL` 那 1 条，用例 16 在 S3 上通过。
  - `FakeS3Client` 认整读，HEAD 交回写下的长度、内容类型与元数据（没记下内容的对象长度照旧是 0）。
  - 改实现之前 `S3DeleteResponseTests` 新增的 5 条都失败（`reason` 为空、没有 Warning）；用例 16 在以前的实现（ADR-0333）上就能过，它补的是套件的覆盖。
- **文档**：两个包 README 已改；文档站的 `object-storage/advanced`、`packages/objectstorage-conformance`（若有）与更新日志待改。
