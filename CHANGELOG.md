# 更新日志 · XiHan.Framework

本文件记录 XiHan.Framework 各版本的变更。每条标注 **新增 / 修复 / 优化 / 调整 / 升级 / 移除** 类别。框架以 NuGet 包形式发布，升级前请留意「调整」类中的破坏性变更。

## v3.4.0 (2026-07-08)

- **修复** 修复多租户行过滤全程失效的严重隔离缺陷：7 个 SqlSugar 多租户基类仅添加 TenantId 列却未实现 IMultiTenantEntity，导致 `AddTableFilter<IMultiTenantEntity>` 全程 no-op（所有租户互相可见）；现补齐接口并将过滤器改为标量哨兵
- **新增** 新增 Gitee 第三方 OAuth 登录 Provider
- **升级** 升级依赖，发布 v3.4.0

## v3.3.0 (2026-07-08)

- **新增** 分布式事件总线三 Broker Provider：新增 RabbitMQ / Kafka / Redis 三种跨进程事件传输 Provider，本地事件总线抽象可平滑切换到分布式部署
- **新增** F3 后台作业管理器：新增 IBackgroundJobManager「即发即忘」一次性作业（轮询 Worker + 默认内存存储），并补齐 Redis 持久化作业存储
- **调整** 审计日志通用件下沉新包 XiHan.Framework.Auditing：审计写入器 / 上下文从应用层剥离为独立框架包，供各应用复用
- **调整** 密码哈希器与选项全收归 XiHan.Framework.Security（Authentication 去重），IPasswordHasher 由 Security 自注册，修复跨模块依赖倒置
- **修复** CurrentPrincipalAccessor.Principal 匿名请求兜底，防匿名访问触发 NRE
- **优化** 优化默认测试项目
- **升级** 升级依赖，发布 v3.3.0

## v3.2.0 (2026-07-06)

- **新增** AI 能力体系从零落地：新建 XiHan.Framework.AI.Abstractions 抽象包，OpenAI 兼容 Provider 解析 + 会话门面，Provider 解析支持 Invalidate 配置热切换
- **新增** RAG 检索增强底座：嵌入 Provider + 向量抽象与默认实现（构建于 Microsoft.Extensions.VectorData）
- **新增** Agent 与 MCP 桥接：Agent 门面（AsAIAgent/AgentSession）、技能注册表自动收纳并经官方桥接投影为 MCP 工具
- **新增** AI 横切能力：护栏中间件（DelegatingChatClient fail-closed）、遥测/缓存管道开关（默认关）、提示词库默认源
- **新增** SqlSugar 主从读写分离配置完整暴露：新增构建前钩子 ConfigureConnectionConfigs 交出原生连接配置供完整定制、AppendDataExecuting 追加式 AOP（核心注入不可覆盖、仅支持追加）、可选从库健康探针（默认关）、租户连接支持从库
- **修复** 从库读权重 HitRate 无法经 appsettings 绑定导致从库不分担读，现构建时归一化为默认权重
- **修复** MySQL 初始化强制 utf8mb4，修复 emoji 写入报错
- **调整** 移除 MySQL 存量表 utf8mb4 兜底转换（前向单一格式）
- **升级** 升级依赖（CodeAnalysis / SqlSugarCore 等）、统一文件头，发布 v3.2.0

## v3.1.0 (2026-07-03)

- **新增** Bot.Sms 短信子包与 Telegram 多机器人平台（MultiBot 运行时、Webhook 中间件、内置 /start /help /myid 命令）
- **新增** 入站限流（按 IP 固定窗口）与三态熔断（滑动窗口统计 5xx，默认关闭，/health 豁免）
- **新增** 运行时按租户注册 SqlSugar 连接（ISqlSugarTenantConnectionProvider，库隔离基元，可选，不注册则行为不变）
- **调整** Bot 库拆分为六个 Provider 子包（Email / Sms / Telegram / DingTalk / Lark / WeCom），统一目录结构、命名空间随目录、配置 store 化
- **调整** 返回码语义化并对齐 HTTP 官方标准（Failed → InternalServerError，补全协议码，新增 10000+ 业务码区段）
- **调整** IBotClient 发送方法签名变更：返回 BotDispatchResult、显式 channels + CancellationToken（可 fail-closed 判定成败）
- **修复** BotResult 工厂无限自递归导致的 StackOverflow、投递结果被门面丢弃、Email 取消被吞成发送失败
- **修复** IsPasswordReusedAsync 改为加盐哈希逐条比对（原实现对 PBKDF2 加盐哈希永不命中）
- **升级** 依赖升级（CodeAnalysis / SqlSugarCore / Scalar / Mapster / 云短信 SDK 等）并发布 v3.1.0

## v3.0.1 (2026-06-27)

- **新增** 新增国际化框架机制：请求文化中间件 + 异常可本地化 + 响应本地化兜底，ApiResponse.Message 按请求文化本地化，实现时区切换
- **新增** 外部传入的 C# 属性名排序字段解析为实体属性并映射为物理列名，标准化表名、列名
- **修复** 修复 JSON 本地化资源无 backing 程序集时因 ResourceManager 兜底崩溃的问题，加固响应本地化防崩
- **修复** ApplyFilter 健壮值强转，修复 In 类型不匹配及可空/未知字段的安全处理
- **调整** 撤销「分页方法统一走 POST」约定，改由各方法显式标注 [HttpPost]

## v3.0.0 (2026-06-20)

- **新增** 新增分布式锁、队列、延迟队列支持，兼容 Redis 包引用
- **新增** 新增一次性验证码服务 IOneTimeCodeService，实时通信新增 TaskProgress 后台任务进度事件常量
- **新增** 本地存储 Provider 支持预签名 URL，返回静态可访问 URL 不再抛 NotSupported
- **修复** 修复 DateTimeOffset 按 ISO 8601 带时区偏移序列化保留时区语义，ApiResponseCodes 按数值序列化
- **修复** 修复本地存储静态文件服务注册在鉴权前导致 /uploads 401 的问题
- **修复** 修复动态 API 特性 bool 默认值覆盖全局配置的陷阱，修复 Cron 时区统一/六段秒位/调度死亡显性化及 Delay 一次性触发语义
- **优化** 日志体系规范化：敏感脱敏、查询不落操作日志、软删恢复识别；RandomCoder 全面加密安全化，异常统一化输出
- **移除** 移除消息后台发送，改由业务层接管

## v2.5.0 (2026-05-30)

- **新增** 新增混合授权与 ABAC 策略，落地权限体系与登录日志
- **新增** 新增 XiHan.Framework.Analyzers 分析器包，新增请求链路追踪 ID 与 ApiLog 写入管道（ITraceableEntity 自动填充）
- **新增** 新增 IPasswordPolicyService、IMessageOutbox、EntityChangeInterceptor 及枚举元数据增强与动态 JSON 加载/变更重载
- **调整** 启动 next 架构重构：内核分层、启动层初始化、定义框架哲学，仓储参数注入移入 DataExecuting AOP
- **修复** 修复 EventBus 后台服务停止异常、DynamicApi 取消令牌绑定及命名空间路由/去重/绑定一致性问题
- **优化** 收紧 OpenApi 算法基线并强化防重放，AuditLog 命名优化为 DiffLog 减少歧义
- **移除** 移除分表仓储，移除基类索引改由子类创建以规避分表占位符编译问题

## v2.4.0 (2026-04-08)

- **新增** 新增第三方登录支持
- **新增** 新增 Castle 动态代理集成库，并添加缓存 AOP 拦截
- **新增** 新增动态 API 权限保护，将数据库初始化下沉到框架
- **调整** 将各 Module 内联 DI 注册提取为 AddXiHanXxx() 扩展方法，第三方可通过独立 NuGet 包直接注入而不依赖模块系统
- **调整** Contracts 接口去除实体泛型参数，Application.Contracts 移除对 Domain 的模块依赖，消除 Application 层 HTTP 形状泄漏
- **修复** long 序列化为 JSON 字符串避免 JavaScript 精度溢出，修复 HTTP 请求 string 类型重复序列化问题

## v2.3.3 (2026-03-14)

- **新增** 实现机器人功能，支持邮件、钉钉、飞书、企业微信，并新增 Telegram 支持
- **新增** 实现消息模块功能，重写 AI 功能
- **新增** 新增异步日志管道（Access/Operation/Exception），中间件/过滤器切换为可插拔管道；新增 RequestContext 统一请求上下文注入
- **新增** 新增 OpenAPI 签名加密与分布式安全升级引擎项目
- **新增** 新增动态 API 分组、标签能力与数据库元数据能力
- **修复** 修复 SqlSugarRepositoryBase 条件更新/删除的租户过滤缺口，修复 CrudApplicationServiceBase 的 DTO 到实体映射覆盖失效
- **调整** 后端接口返回默认改为 JsonNamingPolicy.CamelCase，统一返回结果封装，强化 UnitOfWork 失败回滚语义

## v2.2.0 (2026-02-17)

- **新增** 新增租户数据隔离、租户解析透传与多租户实体基类，租户 ID 由 GUID 统一改为 long
- **新增** 新增通用响应封装、错误消息混淆与统一文件头项目
- **优化** 优化分页查询与自动分页仓储，可指定 AppService 分页方法
- **修复** 修复分页不生效、单审计实体无法使用仓储的问题
- **修复** 修复 HTTP 库 Set/Without 等 Fluent API 设置不生效及动态 API 将参数误加入 path 的问题
- **优化** 优化 OpenAPI 文档与 API 文档 Tags 功能，优化 SqlSugar ID 生成与分布式 ID 配置注入

## v2.0.0 (2026-01-26)

- **升级** 全框架升级至 .NET 10 目标框架，同步升级所有依赖包
- **新增** 新增数据库初始化与种子数据初始化服务，分布式 ID 提供默认注入
- **新增** 实现认证授权体系，含鉴权、授权默认实现与密码配置项
- **新增** 文件系统支持云存储（本地/阿里云 OSS/腾讯云 COS/MinIO）及图片、视频处理
- **优化** 重构仓储结构与参数类型、优化聚合根与数据访问基类，减少调用转换
- **修复** 修复动态 API 多版本丢失、版本号不正确及服务未暴露的问题
- **调整** 部分包抽离为抽象层，优化项目结构与版本控制标识

## v1.4.6 (2026-01-26)

- **修复** 修复 Http 库 Set、Without 等 Fluent API 设置不生效的问题
- **优化** 若干细节优化

## v1.4.5 (2025-11-12)

- **调整** 仓储实现分页并进一步优化分页，统一审计字段，移动 page 功能，重构 CRUD 服务与表达式扩展
- **优化** Base 编码转换器全面性能优化，内存分配减少约 70-80%、编解码速度显著提升
- **优化** 优化分布式 Id 生成库与命名空间
- **修复** 修复 Id 工厂泛型类型、HTTP 请求序列化问题，以及 VS2026 将 Reverse 扩展误映射为 Span.Reverse 的问题
- **移除** 移除旧解决方案、默认 Id 注入、snk 签名及无用命名空间
- **升级** 多次升级依赖，发布 v1.4.5

## v1.4.3 (2025-10-27)

- **新增** 新增动态 WebApi 功能，并添加 Web.Api 测试项目
- **调整** 分页重构、统一仓储方法名，调整相关契约
- **新增** 补充更多基础类型转换能力
- **修复** 修复字符串强转可空数值类型（如 Nullable<Double>）时的转换报错
- **升级** 升级依赖，发布 v1.4.3

## v1.4.2 (2025-10-23)

- **新增** XiHan.Framework.Http 实现代理功能，并补充相应单元测试
- **优化** 优化扩展方法，避免与 System.Utility 产生二义性污染
- **升级** 升级依赖，发布 v1.4.2

## v1.4.1 (2025-10-19)

- **新增** 完善租户接口，优化租户解析与租户设置
- **修复** 修复依赖注入的默认实现
- **升级** 发布 v1.4.1

## v1.4.0 (2025-10-19)

- **新增** 新增高性能线程安全内存缓存方案，支持惰性清理、高级查询、事件通知、多种过期与淘汰策略及统计功能
- **升级** 日志组件由 Thread+BlockingCollection 升级为 Task+Channel 异步架构，新增性能统计、可插拔格式化器、多种日志轮转与背压策略
- **新增** 新增设置中心默认实现，并完善本地事件总线
- **新增** 新增顺序 GUID 生成器与锁扩展方法
- **优化** 优化日志清理逻辑与整体代码风格
- **升级** 升级依赖并补充全量包的测试依赖，发布 v1.4.0

## v1.3.6 (2025-10-17)

- **修复** 修复命令赋值错误
- **调整** 调整开发工具与命名空间组织
- **升级** 升级第三方依赖

## v1.3.5 (2025-10-16)

- **修复** 修复 HTTP 模块扩展相关问题
- **修复** 修复主键构造函数缺陷

## v1.3.2 (2025-10-10)

- **新增** 新增计划任务功能，完善 Tasks 模块的 ScheduledJobs 调度能力
- **调整** 重写任务调度，去除对第三方库的依赖，改为自研实现
- **优化** 优化并扩充仓储 Repository 功能与 ORM 实体建模
- **调整** 调整项目结构与命名空间，动态对象转换方法与对象映射引用重命名
- **优化** 清理部分编译警告，统一命名格式
- **升级** 升级相关第三方依赖

## v1.3.1 (2025-09-24)

- **优化** 优化 HTTP 请求包，改进请求处理相关能力
- **新增** 引入 DefaultTemplateEngine 默认模板引擎（暂存实现）
- **调整** 整理文件与代码结构，统一命名与格式

## v1.3.0 (2025-09-20)

- **新增** 新增 XiHan.Framework.Domain 领域模块并做全面重构，完善实体与 Id 建模
- **新增** 新增后台服务基类，配套后台任务示例，便于承载后台异步工作
- **新增** 新增一整套命令行 ConsoleTools：进度条、多任务进度、旋转加载指示器、彩色输出、交互式提示与菜单
- **新增** 新增 ConsoleTable 表格打印，支持自适应列宽行高、多种边框样式与配色
- **新增** 新增 XiHan.Framework.Web.Grpc 库，支持 gRPC 服务
- **优化** 重写并优化文件日志，提供极高性能的文件日志写入
- **调整** 计划任务及相关项目重命名、包名统一，Data 模块结构优化
- **升级** 多轮升级第三方依赖到较新版本

## v1.2.1 (2025-09-08)

- **调整** 调整解决方案目录结构，梳理各模块项目组织方式
- **修复** 修复包命名错误，纠正误改的控制台输出命名
- **优化** 精简与优化模块依赖引用

## v1.1.0 (2025-09-07)

- **调整** 大规模包重构与整合：AspNetCore.Mvc→Web.Api、Serilog→Logging、SqlSugarCore→Data、VirtualFileSystem→FileSystem、SignalR→Web.RealTime、Bot→Messaging，Ddd 系列与认证系列分别合并至 XiHan.Framework.Ddd / Authentication。
- **调整** 合并 Swagger/Scalar 为 Web.Docs，ObjectExtending/ObjectMapping.Mapster 合并为 ObjectMapping，BackgroundWorkers 合并入 BackgroundJobs，Caching.StackExchangeRedis 并入 Caching。
- **新增** 新增 XiHan.Framework.Security（承接从 Utils 迁出的加密功能）、XiHan.Framework.Script、XiHan.Framework.DevTools 等模块。
- **新增** 重写序列化器，新增 JSON 序列化转换器与序列化 Try 方法，新增动态 JSON 功能与 ComparableExtensions。
- **移除** 移除 BlobStoring、DataFiltering、AspNetCore.Refit 等模块及若干非核心功能，精简目录结构。
- **优化** 扩充表达式功能，优化重试、系统信息获取与对象映射，整体优化解决方案架构。
- **升级** 升级依赖，解决方案完成未来版适配后回落为基础版，发布 v1.1.0。

## v0.11.7 (2025-08-02)

- **调整** XiHan.Framework.AspNetCore.Serilog 包更名为 XiHan.Framework.Logging，提供更通用的日志能力，并重构元数据包。
- **新增** 新增渐变控制台打印、项目信息展示，并扩充 DynamicJsonValue 与数字扩展方法。
- **优化** 优化日志模块、应用启动流程与默认序列化选项，扩充序列化扩展方法。
- **移除** 移除 [Serializable] 标记，优先使用 System.Text.Json（特殊场景考虑 MessagePack/MemoryPack），并清理冗余方法。
- **新增** IEnumerable 新增随机取项能力，提升数字计算性能。
- **升级** 升级依赖并发布 v0.11.7。

## v0.11.3 (2025-07-03)

- **优化** Windows 硬件信息采集由 wmic 替换为 PowerShell，并优化硬件信息缓存时长。
- **移除** 移除运行时已自带的重复功能及项目内多余测试代码与文本文件。
- **新增** 恢复基础测试项目。
- **升级** 升级依赖并发布 v0.11.3。

## v0.11.2 (2025-06-29)

- **移除** 移除 EFCore 库，默认统一使用 SqlSugarCore 作为 ORM；移除框架已内置的时间戳获取功能。
- **修复** 修复 JSON 字符串格式化问题。
- **升级** 升级依赖并发布 v0.11.2。

## v0.11.1 (2025-06-25)

- **新增** 定义事件总线抽象接口，新增事件处理器工厂与事件追踪 ID 接口，搭建事件驱动能力底座。
- **新增** 新增对象扩展包、类型辅助类、验证异常，并为字典与请求原始数据补充扩展方法。
- **调整** 重构本地化包，Utils 包 AssemblyHelper 扩充并重命名为 ReflectionHelper，统一多租户/文化帮助类/lock 等命名空间。
- **优化** 优化 HTTP 模块返回结果与序列化，修正 object/dynamic 类型返回默认值应为 null 的行为。
- **优化** 优化 RSA 加密默认参数、脱敏逻辑、RandomCoder，并增强设备与硬件/运行时信息采集。
- **修复** 修复 DynamicJsonObject 访问不存在属性时报错的问题，改为返回 null。
- **升级** 同步升级依赖并发布 v0.11.1。

## v0.9.4 (2025-06-05)

- **新增** 扩充 Utils 包：新增农历、异步屏障、数据预测、摩尔斯编码及特殊字符转义功能
- **新增** 新增双向链表、堆栈、队列扩展方法与 ClaimsPrincipal 抽象基类
- **新增** 新增 OpenAPI 支持并优化相关依赖
- **升级** 升级三方依赖包

## v0.9.3 (2025-06-03)

- **新增** 新增事件总线，支持本地与分布式事件处理器
- **新增** 新增 XiHan.Framework.Data 包
- **修复** 修复创建服务提供器时 ServiceProvider 为空及 AI 模块注入问题
- **调整** 对象访问器由 Utils 包迁移至 Core 包，梳理异常与相关命名空间
- **优化** 优化启动日志、解决方案目录与 xml/yaml 类

## v0.9.1 (2025-06-02)

- **新增** 新增领域服务与仓储接口，搭建领域包基础
- **新增** 工作单元管理器支持子工作单元

## v0.9.0 (2025-06-02)

- **新增** 新增 XiHan.Framework.Script 包，支持脚本安全执行与 Debug 调试功能
- **调整** 数据检测器 CheckHelper 重命名为 Guard
- **优化** 优化 Http 包引用与整体包结构

## v0.8.35 (2025-05-31)

- **新增** 新增 XiHan.Framework.DistributedIds、Validation、Authorization 包，ID 算法补充 NanoId
- **新增** 扩充编码方案，新增 Base32/Base36/Base58/Base62/Base95 及自定义进制编码
- **调整** XiHan.Framework.Data 重命名为 XiHan.Framework.DataFiltering，ValidateCoder 重命名为 RandomCoder
- **修复** 修复包引用错误与脚本文件 UTF-8 BOM 编码问题
- **移除** 移除旧版解决方案、历史测试及 UUID 生成器
- **升级** 升级至 9.0.5 版本依赖

## v0.8.31 (2025-05-17)

- **升级** 升级 XiHan.Framework.AI 依赖，并修复升级后接口兼容问题
- **修复** 修复测试文件
- **移除** 移除非常用功能

## v0.8.30 (2025-05-16)

- **新增** 新增 Sqids 短 ID 编解码功能，并支持代码动态加载
- **修复** 修复 Sqids 算法缺陷
- **优化** 优化 Python 解释器、日志输出与若干字符串方法
- **升级** 升级三方依赖包

## v0.8.28 (2025-05-06)

- **新增** 新增 XiHan.Framework.Data 数据库访问库与 XiHan.Framework.SqlSugar 包，完善 SqlSugarCore 集成
- **新增** 新增雪花 ID（SnowflakeId）、UUID 生成器与基于 Unicode 的文本数字水印工具
- **新增** 新增 MCP 包、SSE 通讯支持、模板引擎、SFTP/SSH 连接及简易 YAML 解释器与缓存管理
- **新增** 新增简单的国际化处理器
- **优化** 优化 RSA 加密与程序集依赖包相关方法，梳理命名空间与目录结构
- **升级** 多轮升级三方依赖包

## v0.8.20 (2025-04-02)

- **新增** 新增 XiHan.Framework.Http.Client 包。
- **优化** 优化工作单元处理逻辑。
- **修复** 将 ==null / !=null 判空替换为 is null / is not null，规避运算符重载风险。
- **升级** 升级依赖包。

## v0.8.18 (2025-03-19)

- **优化** 细节优化。

## v0.8.17 (2025-03-18)

- **新增** 新增 XiHan.Framework.Localization 本地化包，核心模块接入本地化工具。
- **新增** 新增混合缓存功能、深度合并配置与主题枚举。
- **升级** 升级至 .NET 10 及相关依赖。
- **修复** 修复模块间循环依赖，回退部分 9.0 相关改动。
- **优化** 优化 Settings、多租户模块引用及测试项目结构，更换 Logo。
- **移除** 移除多余包引用。

## v0.8.15 (2025-02-25)

- **新增** 新增 XiHan.Framework.Security 安全库与 XiHan.Framework.Settings 设置库。
- **新增** 新增 DDD 实体、聚合根与领域事件基础能力。
- **新增** 新增 XiHan.Framework.ObjectMapping.Mapster 对象映射包，并集成 Ollama 与 OpenAI 调用及依赖注入。
- **新增** 新增数据脱敏工具类，并强化虚拟文件系统的文件监听能力。
- **优化** 优化 HTTP 请求处理、Swagger/Scalar 注入与虚拟文件系统包。
- **调整** 重命名 AspNetCore.Serilog、AspNetCore.SignalR 等包并调整项目结构，启用新的解决方案后缀。
- **移除** 移除虚拟文件系统的文件缓存功能及多余包引用。

## v0.8.10 (2025-01-26)

- **优化** 优化分页处理逻辑。
- **调整** 调整命名空间组织。
- **升级** 升级依赖包。

## v0.8.9 (2025-01-21)

- **升级** 升级依赖包并推进版本号，含预览版发行验证。
- **修复** 补丁修复若干问题。
- **优化** 大量代码与注释优化，改善可读性与一致性。

## v0.8.7 (2025-01-05)

- **升级** 升级第三方依赖包版本。
- **优化** 多处代码质量与结构细节优化，提升可读性。
- **调整** 整理开源许可证相关内容。

## v0.8.5 (2024-12-16)

- **新增** 新增工作单元 XiHan.Framework.Uow、线程 Threading 与多租户 MultiTenancy 模块
- **新增** 新增文本模板 TextTemplating 与虚拟文件系统 VirtualFileSystem 模块
- **新增** 新增 XiHan.Framework.Caching.StackExchangeRedis 缓存包
- **新增** 新增 XiHan.Framework.AspNetCore.Scalar 接口文档包
- **调整** 优化项目文件夹结构与代码格式

## v0.8.4 (2024-12-10)

- **修复** 修复 XiHan.Framework.AspNetCore 包 AddObjectAccessor 的注入问题

## v0.8.3 (2024-12-10)

- **新增** 新增 OTP 与 HMAC 生成能力
- **移除** 移除 XiHan.Framework.AspNetCore.MVC 目录
- **优化** 优化构建与发布脚本、更新关键词

## v0.8.0 (2024-12-06)

- **新增** 新增 XiHan.Framework.AspNetCore.MVC 库
- **新增** 新增 XML 解析与表达式扩展方法
- **优化** 优化树节点处理功能
- **调整** 拆分 props 文件、统一命名，调整项目结构

## v0.7.5 (2024-12-04)

- **新增** 通用分页支持多条件筛选（选择）与多字段排序
- **新增** 新增数据过滤能力，配套树形数据处理功能
- **新增** 新增国密算法支持
- **新增** 引入机器学习与人工智能相关依赖
- **升级** 多目标支持 .NET 8.0 / 9.0，并针对 .NET 9.0+ 做性能提升
- **优化** 补充单元测试、清理代码格式与注释

## v0.7.0 (2024-11-13)

- **升级** 框架整体升级到 .NET 9，同步升级 NuGet 包版本
- **修复** 修复时间转换为简易字符串时的结果错误
- **优化** 改进版本更新脚本，统一换行符为 LF

## v0.5.9 (2024-10-28)

- **优化** 细节优化

## v0.5.8 (2024-10-28)

- **优化** 优化模块化服务配置基类

## v0.5.7 (2024-10-28)

- **优化** 优化应用扩展实现

## v0.5.6 (2024-10-28)

- **新增** 新增核心包应用扩展
- **优化** 核心包功能收尾完善

## v0.5.5 (2024-10-27)

- **调整** 更新工具包命名空间
- **调整** 更新项目引用关系

## v0.5.4 (2024-10-27)

- **新增** 新增核心包模块功能并完善核心包能力
- **移除** 移除 PlugIn 插件包
- **升级** 新增 NuGet 发布脚本

## v0.5.3 (2024-10-26)

- **升级** 发布 v0.5.3 版本
- **优化** 细节优化

## v0.5.2 (2024-10-20)

- **优化** 优化工具类实现
- **优化** 优化加密相关工具

## v0.5.1 (2024-10-11)

- **新增** 奠基解决方案与基础库，搭建框架整体骨架
- **新增** 新增鉴权授权、工作单元、后台任务、领域驱动、缓存等核心基础设施项目
- **新增** 新增 ORM 数据库访问与客户端库、序列化项目
- **新增** 新增代码生成、Swagger、网关、插件、工具等配套项目
- **升级** 统一 .NET 版本管理并补齐 NuGet 打包所需文件
