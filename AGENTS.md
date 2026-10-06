# AGENTS.md

本文件约束在 XiHan.Framework 仓库内工作的 AI Agent。回答、文档与提交说明使用中文；代码标识、公开 API 和标准技术术语保持英文。

## 仓库概览

XiHan.Framework 是面向 .NET 10 的模块化应用框架。仓库以分层模块、显式依赖和统一生命周期为核心，公共默认实现保持零外部基础设施依赖，具体数据库、缓存和消息实现由应用层提供。

### 技术栈

| 技术 | 要求 | 用途 |
| --- | --- | --- |
| .NET SDK | `global.json` 锁定的 10.0.1xx 功能带 | 编译与运行 |
| C# | Nullable + ImplicitUsings | 框架源码 |
| Microsoft.Testing.Platform | 由 `global.json` 启用 | 测试运行器 |
| xUnit v3 | 以 `framework/props/test.props` 为准 | 单元与集成测试 |
| VitePress | 以 `docs/package.json` 为准 | 文档站 |

## 开始前必须做

1. 按任务读取对应技能：模块、分层与公共 API 读取 `.agents/skills/module-development/SKILL.md`；缓存、队列和外部资源读取 `.agents/skills/resource-governance/SKILL.md`；测试、文档、打包和发布读取 `.agents/skills/testing-release/SKILL.md`。
2. 同时涉及多个职责时加载对应多个技能，不要一次读取无关资料。
3. 检查当前分支、`git status` 和最近提交；保留用户已有改动。在 worktree 中还要按「分支与 worktree」核对分支分组和基线。
4. 阅读目标模块及其直接依赖的 README、项目文件、Module 类和相邻实现。
5. 先确认能力应归属 Utils、Metadata、Core、Domain、Application、Infrastructure 还是 Web 层，再创建代码。
6. 查找已有接口、默认实现、扩展方法与测试，不重复建设相同能力。

## 目录结构

```text
/
├─ AGENTS.md
├─ .agents/skills/                 # 按模块、资源和发布分类的仓库技能
├─ .claude/skills -> ../.agents/skills
├─ CLAUDE.md -> AGENTS.md
├─ docs/                           # VitePress 文档站
├─ framework/
│  ├─ XiHan.Framework.slnx         # 主解决方案
│  ├─ props/                       # 公共构建、测试、包元数据
│  ├─ scripts/                     # 项目维护与 NuGet 脚本
│  ├─ src/                         # 正式模块
│  ├─ test/                        # 单元与集成测试
│  ├─ sample/                      # 示例
│  └─ tool/                        # 工具项目
└─ next/                           # 下一阶段设计与实验；不等同于正式 API
```

## 常用命令

在仓库根执行：

| 任务 | 命令 |
| --- | --- |
| 还原 | `dotnet restore framework/XiHan.Framework.slnx` |
| 常规构建 | `dotnet build framework/XiHan.Framework.slnx -c Release --no-restore -p:GeneratePackageOnBuild=false` |
| CI 同形测试 | `dotnet test --solution framework/XiHan.Framework.slnx --configuration Release --no-build -p:TestingPlatformCommandLineArguments="--coverage --coverage-output-format cobertura"` |
| 文档安装 | `pnpm --dir docs install --frozen-lockfile` |
| 文档构建 | `pnpm --dir docs build` |

`global.json` 已选择 Microsoft.Testing.Platform。不要改回 VSTest 形式，也不要删除 `--solution` 来规避测试运行错误。

## 架构约束

### 依赖方向

- 基础能力按 `Utils → Metadata → Core → Domain → Application/Contracts → Infrastructure → Web` 单向演进。
- 模块通过 `[DependsOn]` 声明依赖；不得通过静态服务定位、反射探测或复制代码形成隐式依赖。
- 抽象与契约放在下层或 `.Abstractions` 项目，具体 Provider 放在上层实现项目。
- 不允许为了复用少量代码让底层模块反向引用 Web、数据库、Redis 或应用模块。

### 模块与生命周期

- 模块命名遵循 `XiHan.Framework.<Module>`；抽象、Provider 和 Web 集成沿用现有后缀。
- 新模块或依赖变化必须检查完整拓扑，不制造环依赖。
- 初始化与关闭逻辑放入现有模块生命周期钩子；保持异步、顺序和异常语义一致。
- 服务注册应显式、幂等，并通过模块扩展方法组织；不要膨胀应用入口。

### 公共 API

- 公开类型、成员和选项必须有准确 XML 文档。
- Nullable 是契约的一部分；不要用 `!`、空字符串或默认实例掩盖非法状态。
- 异步 API 接受并传递 `CancellationToken`；不阻塞异步调用，不吞异常。
- 破坏性 API 变更必须明确说明迁移方式，不保留推测性兼容别名或静默兜底。
- 优先使用 .NET 内建能力；新增依赖必须有明确的框架级收益和依赖边界。

### 默认实现与外部资源

- 框架默认实现命名为 `DefaultXxx`，必须可独立工作、边界明确且有容量上限。
- 默认实现不得要求 Redis、数据库、消息队列或应用配置才能启动。
- 分布式与持久化实现由消费应用提供，框架只定义稳定契约和必要扩展点。
- 缓存、队列、会话和注册表不得无界增长；容量、过期、清理与并发语义必须测试。

## 测试

- 修改哪个正式模块，就在对应测试项目添加或更新测试。
- 优先验证公开可观察行为、错误、取消、并发、边界容量和生命周期顺序。
- 修复缺陷先写能复现问题的测试，再改实现。
- 涉及模块发现、依赖排序、配置绑定、动态代理或序列化时，补充跨项目集成验证。
- 不以覆盖率数字代替有效断言，不删除测试或放宽 CI 阈值来通过门禁。
- 资源受限且明确出现系统资源不足时，才可临时使用 `/m:4 /nr:false` 控制 MSBuild 并发；这不是默认命令。

## 文档与示例

- 改变公开 API、默认值、配置、生命周期或使用方式时，同步更新 XML 文档、模块 README、示例和文档站。
- 示例只展示推荐路径，不保留已废弃做法，也不把应用专用基础设施包装成框架默认方案。
- 文档命令在 `docs/` 执行；源码命令在仓库根或 `framework/` 对应位置执行。

## 发布

- `framework/props/nuget.props` 默认可在构建时生成包，日常验证应显式关闭 `GeneratePackageOnBuild`，避免制造无关包产物。
- 版本脚本、包撤销脚本、打标签和 NuGet 发布都属于外部状态变更，只有用户明确要求时才能执行。
- 发布前以 `.github/workflows/ci.yml` 和 `.github/workflows/release.yml` 为最终事实源，完成 Release 构建、MTP 测试和包清单核对。

## 分支与 worktree

在主检出上的日常改动直接提交到当前分支（通常是 `dev`），不另开分支。新功能或并行开发开启 worktree 时，分支按用途分组命名，不保留工具生成的 `claude/<名>`、`codex/<名>` 等前缀：

| 分组 | 用途 | 分支名 |
| --- | --- | --- |
| `feat/` | 新功能、增强，以及重构、性能、文档、测试等非缺陷改动 | `feat/<worktree 名>`，如 `feat/confident-morse-925efe` |
| `fix/` | 缺陷修复 | `fix/<worktree 名>`，如 `fix/confident-morse-925efe` |
| `issue/` | 处理指定的 GitHub Issue | `issue/<编号>`，如 `issue/46` |
| `pr/` | 审阅或接手已有 Pull Request | 拉取他人 PR 审阅用 `pr/<编号>-review`；没有编号时用 `pr/<worktree 名>` |

- 任务对应 Issue 或 PR 编号时归入 `issue/`、`pr/` 并以编号命名；其余按改动性质取 `feat/` 或 `fix/`，后缀沿用 worktree 目录名。
- 进入 worktree 后先看 `git branch --show-current`。分支是工具生成的 `claude/<名>` 等形式时，在首次提交前就地改名：`git branch -m <分组>/<名>`；worktree 目录名不变。改名后上游若指向 `main` 等别的分支，执行 `git branch --unset-upstream`。分支已推送到远端时不改名，先向用户说明。
- 自行创建时直接带分组：`git worktree add -b feat/<名> .claude/worktrees/<名> dev`。审阅他人 PR 时先 `git fetch origin pull/<编号>/head:pr/<编号>-review`，再 `git worktree add .claude/worktrees/pr-<编号> pr/<编号>-review`。
- 基线默认是本地 `dev`；热修复以对应 `hotfix/v<主版本>` 为基线，用户指定 `next` 或 `release/*` 时以其为准。工具从 `main` 或 `origin/main` 切出且还没有提交时，用 `git merge --ff-only dev` 对齐；不能快进就停下说明，不 reset。
- 一个 worktree 只承载一项任务、一条分支。并行任务会改到同一文件、同一公共 API 或同一包元数据时，先说明冲突风险再动手。
- 新 worktree 没有还原结果和构建产物，先执行 `dotnet restore framework/XiHan.Framework.slnx`，改文档时再安装 `docs/` 依赖。
- worktree 分支的合并、推送、创建 PR 以及 worktree 和分支的删除，只在用户明确要求时执行。

## Git 与提交

- 保留用户已有和无关改动；不要 reset、checkout 或清理它们。
- 使用 Conventional Commits：`<type>(<scope>): <中文说明>`。
- 每个模块能力或独立修复单独提交；契约、实现、测试和文档属于同一闭环时放在同一提交。
- 提交前检查 `git diff --check`、相关测试和必要的 Release 构建。
- 不推送、不发布、不创建远程 PR，除非用户明确要求。

## 禁止事项

- 不跨层放置实现，不以“方便”绕过模块依赖。
- 不添加静默兜底、无限重试或无界内存集合。
- 不把 BasicApp 或其他应用的具体存储实现放入框架默认路径。
- 不猜测 API、版本、模块数量或 CI 命令；以当前检出为准。
- 不手改构建产物、包目录或生成文件代替修改真源。
- 不运行版本升级、NuGet 撤销、发布或其他破坏性脚本，除非用户明确授权。
