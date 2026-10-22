# XiHan.Framework.Primitives

原语层（Ring 0）。零行为、零依赖的原语与契约，跨语言语义由 XiHan Spec（RFC-0001）定义。

## 责任边界

| 类型 | 用途 |
| --- | --- |
| `Result` / `Result<T>` | 成功或携带 `Error` 的失败；用 `Result.Success(value)`、`Result.Failure<T>(error)` 创建，值与错误可隐式转换；`default` 视为未初始化失败 |
| `Error` / `ErrorCode` | 错误码 `<领域>_<KIND>_<NNN>`、消息键与参数、内部错误；工厂方法校验 KIND 段；`Literal` 承载线上还原与异常转换的自由文本 |
| `MessageTemplate` / `MessageArguments` / `MessageKeyExtensions` | 消息键文法、`{name}` / `{0}` 模板格式化、参数字典、`key.L()` 按当前区域取文案（RFC-0010） |
| `IMessageResolver` / `MessageScope` | 解析器契约与环境作用域：内核按调用的 Baggage 区域安装，本地化特性设进程默认 |
| `ManifestMessages` / `ResultMessages` | 本包的消息键常量；文案在 `Localization/<区域>.json` |
| `Maybe` / `Maybe<T>` | 类型层面的"可能没有值"；`default` 即无值 |
| `ITypedId<TValue>` / `TypedId<TTag, TValue>` | 强类型标识，线上编码为底层值 |
| `InvocationContext` / `BaggageKeys` | 取消、截止、W3C Trace、Baggage、作用域对象袋与服务 |
| `Stability` / `ApiLevelAttribute` | 稳定性分级 |
| `LifecycleState` / `LifecycleStateExtensions` | 九态与合法迁移表 |
| `PipelineStage` / `PipelineStageExtensions` | 固定的十一个管道阶段及其清单名 |
| `FrameworkException` / `FrameworkInfo` | 带错误码的异常基类；框架身份（版本由构建期常量提供，不经反射） |
| `Manifests.*` | `xihan.manifest.json` 的对象模型与格式校验：`Manifest`、`CapabilityDeclaration`、`CapabilityRequirement`、`Binding`、`Contributions`、`PipelineContribution`、`ConfigContribution`、`CapabilityId`、`ManifestId` |

不包含：任何实现、基类、宿主、依赖注入、反射、序列化代码。线上 JSON 编码由协议适配器与生成器实现。语言文件只是嵌入资源，由 `Localization.InMemory` 代为装载。

## 依赖方向

不依赖任何包。被 `XiHan.Framework.Kernel` 及其上所有环引用。

## 承诺

只增不改。公开面以 `PublicAPI.Shipped.txt` 为准，已发布行禁止修改；若必须破坏，发布新包 ID 并存。

## 一致性向量

`spec/conformance/vectors/primitives/`：错误码格式、生命周期迁移、能力依赖文本、Baggage 键。单元测试直接回放。

## 状态

P1：公开面已按 RFC-0001 填充，记录在 `PublicAPI.Unshipped.txt`，5.0.0 发布时转入 Shipped 并冻结。
