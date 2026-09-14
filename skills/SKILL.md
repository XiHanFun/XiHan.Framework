---
name: xihan-framework
description: 开发、重构、审查、测试、记录或发布 XiHan.Framework 时使用。覆盖 .NET 10 模块分层、DependsOn 依赖、生命周期、公共 API、默认实现、外部资源边界、Microsoft.Testing.Platform 测试和 NuGet 发布；普通应用业务开发不要加载。
---

# XiHan.Framework

先识别任务类型，只读取对应资料。

## 路由

| 任务 | 必须读取 |
| --- | --- |
| 判断模块归属、依赖方向或公共抽象 | `references/architecture.md` |
| 新增模块、服务、选项、扩展方法或公共 API | `references/architecture.md` + `references/module-development.md` |
| 缓存、队列、会话、注册表、默认实现或外部存储 | `references/defaults-and-resources.md` |
| 测试、覆盖率、文档、版本、打包或发布 | `references/testing-and-release.md` |
| 跨模块重构 | 先读架构，再按涉及内容补读其余 reference |

不要一次加载与任务无关的 reference。

## 不可变约束

- 依赖按 Utils、Metadata、Core、Domain、Application/Contracts、Infrastructure、Web 单向流动。
- 模块依赖必须通过 `[DependsOn]` 显式声明，不制造环依赖或隐式服务定位。
- 抽象下沉，具体 Provider 上浮；底层模块不引用应用、Web 或外部基础设施实现。
- 框架默认实现使用 `DefaultXxx`，零外部基础设施依赖且容量有界。
- Nullable、取消、异常和生命周期顺序都是公共契约，不用兜底值掩盖错误。
- 不添加推测性兼容、静默降级、无限重试或无界集合。
- 测试由 Microsoft.Testing.Platform 驱动；不把命令改回 VSTest。
- 一项框架能力一个提交，不混入无关清理。

## 先查事实

在仓库根开始：

```bash
git status --short --branch
git log -5 --oneline
dotnet sln framework/XiHan.Framework.slnx list
```

实现前至少确认：

1. 目标能力是否已存在于其他模块。
2. 应归属的层和直接依赖。
3. 相邻模块的 Module 类、扩展方法、选项和测试写法。
4. 公开 API、XML 文档、包依赖和兼容性影响。
5. 是否涉及外部资源、容量、并发、取消或生命周期边界。

## 修改边界

- 用户要求解释、审查或诊断时只做只读检查，不擅自实现。
- 用户要求实现时，完成契约、实现、测试、文档和必要包元数据的同一功能闭环。
- 应用专用数据库、Redis、消息和部署实现留在应用仓库；Framework 只提供稳定契约与可独立工作的默认实现。
- 保留工作区中用户已有改动，不使用破坏性 Git 命令。

## 验证

按风险从小到大验证：

1. 目标测试项目或模块级构建。
2. `dotnet build framework/XiHan.Framework.slnx -c Release --no-restore -p:GeneratePackageOnBuild=false`。
3. 使用 MTP 形式运行受影响测试或完整解决方案。
4. 公开文档变化时运行 `pnpm --dir docs build`。
5. 发布任务按 `references/testing-and-release.md` 完成包和工作流核对。

不以编译成功代替行为测试，不以覆盖率数字代替断言质量。
