来源：https://framework.docs.xihanfun.com/appendix/contributing

# 贡献指南

这一页写给想为 XiHan Framework 贡献代码、文档、示例或一致性向量的读者：从哪里开始、改动要走什么流程、代码要符合哪些规范、提交前要过哪些门禁。组织层面共用的贡献约定见[如何为项目贡献代码](https://docs.xihanfun.com/cosmos/contributing)与[贡献者公约](https://docs.xihanfun.com/cosmos/code-of-conduct)，本页只讲框架仓库特有的部分。

## 从哪里开始

| 想做的事 | 入口 |
| --- | --- |
| 报告缺陷或提建议 | [GitHub Issues](https://github.com/XiHanFun/XiHan.Framework/issues)，附框架版本、复现步骤、启动日志里的能力图谱与管道、错误码与 `traceId` |
| 改文档 | 本站每页底部有「在 GitHub 上编辑此页」；源文件在仓库 `docs/` 下 |
| 写一个 Provider | 先读[写一个 Provider](/extending/provider)，通过对应的一致性套件后提 PR |
| 改公开契约 | 先在规范仓库提 RFC，见下文「规范先于实现」 |
| 另一种语言的 SDK | 读[规范与多语言](/appendix/spec)，在对应的 `framework-sdk-<语言>` 仓库里做 |

## 准备环境

```bash
git clone --branch dev https://github.com/XiHanFun/XiHan.Framework.git
cd XiHan.Framework
git submodule update --init framework/spec
dotnet restore framework/XiHan.Framework.slnx
dotnet build framework/XiHan.Framework.slnx -c Release --no-restore
dotnet test --solution framework/XiHan.Framework.slnx --configuration Release --no-build
```

| 项 | 要求 |
| --- | --- |
| .NET SDK | 仓库 `global.json` 锁定的功能带；版本不对时 `dotnet` 会提示 |
| 测试运行器 | Microsoft.Testing.Platform，由 `global.json` 启用；`dotnet test` 用 `--solution` 或 `--project`，不要改回 VSTest 形式 |
| 文档站 | `pnpm --dir docs install --frozen-lockfile`，`pnpm --dir docs build` |
| 可选 | Go 工具链（Go 插件示例与测试）、Rust 工具链（重新编译 Wasm 示例）、Docker（Redis、RabbitMQ、Kafka、PostgreSQL、MinIO 等 Provider 的实测） |

Provider 的实测用例靠环境变量门控，没有设置时跳过：`XIHAN_TEST_REDIS`、`XIHAN_TEST_RABBITMQ`、`XIHAN_TEST_S3`、`XIHAN_TEST_POSTGRES` 等，各测试项目的 README 或夹具里写了具体变量名。

## 仓库布局

```text
/
├─ AGENTS.md                  开发约束的总入口（CLAUDE.md 是它的符号链接）
├─ .agents/skills/            按开发、资源治理、测试发布、版本发布分类的技能文档
├─ plans/                     设计文档：总体方案、architecture/、adr/
├─ docs/                      本文档站
└─ framework/
   ├─ XiHan.Framework.slnx    解决方案，目录按环层分组
   ├─ Directory.Build.props   版本、Nullable、警告即错误、文档文件
   ├─ Directory.Packages.props 中央包版本
   ├─ spec/                   XiHan Spec（framework-spec 仓库的子模块）
   ├─ src/                    正式包，物理目录扁平，包名由术语表与文法推出
   ├─ test/                   单元、架构、集成测试
   ├─ samples/                示例
   ├─ benchmarks/             基准
   ├─ incubator/              实验包，只能标 Experimental
   └─ scripts/                维护脚本
```

## 规范先于实现

公开契约的变更按固定顺序走，没有 RFC 的公开 API 变更不合并：

1. 在规范仓库 [framework-spec](https://github.com/XiHanFun/framework-spec) 的 `rfcs/` 新建提案。
2. 评审通过后在框架仓库的 `plans/adr/` 写一条架构决策记录：状态、日期、背景、备选、决策、理由、影响。
3. 写实现、一致性向量与文档；规范子模块指针随实现一起更新。

新增术语表条目、错误码、能力 ID、Baggage 键同样走 RFC。能力 ID、错误码、操作 ID 一经发布不改。

## 架构约束

这些规则由 `framework/test/XiHan.Framework.Architecture.Tests` 执行，违反即 CI 失败：

| 规则 | 内容 |
| --- | --- |
| 依赖方向 | 环层只能向下依赖；同环之间只经契约包（`.Abstractions` 与纯契约的 `Domain`） |
| 零第三方依赖 | 原语层、内核与抽象包只允许 `Microsoft.Extensions.*` 抽象包 |
| 零反射 | 原语层与内核不用反射；反射只允许在 `Plugins.InProcess` 并标注 `RequiresDynamicCode` |
| 包 ID 文法 | 包名由术语表与文法推出；命名空间等于包 ID，子命名空间最多一层；类型名不带 `XiHan` 前缀；`Default`、`Core`、`Common`、`Manager`、`Helper` 不进包名 |
| 批量优先 | 对集合的操作提供批量方法，框架内部不在循环里调单条方法 |
| 本地化 | 消息键常量与 `en-US.json` 一一对应，十种区域文件齐全、占位符一致，调用点只用常量 |
| 公开 API | 公开类型有 `[ApiLevel]` 与 XML 文档；`PublicAPI.Unshipped.txt` 同步更新，已发布行不改 |
| XML 文档 | 所有类型与成员（含私有）写中文 `<summary>`，三行、只写一句，细节进 `<remarks>` |

## 代码规范

细则在仓库的 `plans/architecture/03-代码规范.md`，这里列最常碰到的：

- 一个类型一个文件，泛型类型文件名加 `OfT`；目录即子命名空间。
- 方法内部的关键逻辑（锁与临界区、状态转移、回退链、容量与并发判定、安全判定顺序）写中文 `//` 注释说明为什么；注释句末不加句号。
- 面向人的文案不写在代码里：代码只引用消息键常量（`<领域>Messages`），每种语言一个文件 `Localization/<区域>.json`，新增键必须同时补十个文件。
- 其余运行时文本用英文：`FrameworkException` 与 `ArgumentException` 的消息（配置与编程错误）、日志模板、分析器诊断、测试断言；句子以句号结尾，标识符用单引号。
- 可恢复的失败返回 `Result` 与 `Error`，不抛异常；异步 API 接受并传递 `CancellationToken`，不阻塞异步调用，不吞异常。
- Nullable 是契约的一部分；不用 `!`、空字符串或默认实例掩盖非法状态。
- 命名与样式在构建期执行：私有字段 `_camelCase`，私有常量 PascalCase，未使用的 using 必须删除；不降低 `.editorconfig` 的严重级别，不加 `NoWarn`。
- 优先使用 .NET 内建能力；新增依赖必须有明确的框架级收益与依赖边界。

## 默认实现与 Provider 的要求

- 每个抽象配一个 `role: default` 的默认实现，按机制命名（`InMemory`、`FileSystem`），零外部设施、容量有界、通过一致性套件。
- 分布式与持久化实现放在 Provider 包；缓存、队列、会话和注册表不得无界增长，容量、过期、清理与并发语义必须测试。
- 批量方法明确部分失败语义、空集合与重复项处理、取消时机，并在测试里覆盖。
- 接入外部设施的 Provider 提供健康项，连通性探测走一次真实往返并按配置超时。

## 测试

- 修改哪个正式包，就在对应测试项目添加或更新测试；优先验证公开可观察行为、错误、取消、并发、边界容量和生命周期顺序。
- 修复缺陷先写能复现问题的测试，再改实现。
- 默认实现与 Provider 必须通过所属子系统的一致性套件；涉及能力解析、管道次序、配置绑定或序列化时补充跨包集成验证。
- 不以覆盖率数字代替有效断言，不删除测试或放宽阈值来通过门禁。

写法见[单元与集成测试](/testing/basics)与[一致性套件与门禁](/testing/conformance)。

## 文档

- 改变公开 API、默认值、配置、生命周期或使用方式时，同步更新 XML 文档、包 README、示例和文档站。
- 文档正文不写包版本：安装写 `dotnet add package <包>`，要表达版本用 `<版本>` 占位。
- 示例只展示推荐路径，不把应用专用的基础设施包装成框架默认方案。
- 文档命令在 `docs/` 执行，源码命令在仓库根执行。

## 提交与分支

- 日常开发在 `dev` 分支；`preview` 与 `main` 是发布分支，只接收发版。
- 使用 Conventional Commits：`<type>(<scope>): <中文说明>`，scope 用术语表领域词小写，或 `spec`、`docs`、`build`。
- 每项能力或独立修复单独提交；契约、实现、测试和文档属于同一闭环时放在同一提交。
- 提交前检查 `git diff --check`、相关测试和必要的 Release 构建。
- 新建源码文件加文件头：

```csharp
// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.
```

## 提交 Pull Request

1. Fork 仓库，从 `dev` 切出分支。
2. 完成改动，本地跑通构建、测试与架构测试；改了公开 API 时同步 `PublicAPI.Unshipped.txt`。
3. 同步 `dev` 最新代码，向 `dev` 发起 Pull Request，按模板写清改了什么、为什么改、怎么验证。
4. CI 会跑 Release 构建、全部测试、分析器、架构测试、包验证与 Native AOT 冒烟；全部通过后进入评审。

## 常见问题

### 我想加一个新的子系统

先在规范仓库提 RFC 定义能力 ID、抽象与错误码，再按[写一个 Provider](/extending/provider)的方式写抽象包、默认实现与一致性套件。新领域词要进术语表。

### 分析器报 XH 开头的诊断

`Kernel.Analyzers` 在正式包上执行稳定性标记、禁用词、`Internal` 引用与反射的规则。按诊断信息改代码，不要用 `NoWarn` 压掉。

### 架构测试报本地化相关的失败

多半是新增了消息键但没有补齐十个语言文件，或者 `Error` 的调用点没有用常量。按失败信息逐项补齐。

### 想做的改动涉及好几个包

拆成多个提交：先契约与默认实现，再 Provider，再文档；每个提交都能独立构建与测试。单个 PR 控制在合理规模，便于评审。

## 下一步

- [支持曦寒](/appendix/support)：除了代码还能怎样支持项目
- [规范与多语言](/appendix/spec)：RFC 流程与各语言 SDK
