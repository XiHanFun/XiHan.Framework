---
name: xihan-framework-module-development
description: 设计、实现、重构或审查 XiHan.Framework 模块、分层、DependsOn 依赖、生命周期、服务注册、Options 和公共 API 时使用。缓存、队列等资源实现以及测试发布分别使用对应专项技能。
---

# XiHan.Framework 模块开发

判断层次和依赖时先读 `references/architecture.md`；新增服务、选项、扩展方法或公开 API 时再读 `references/module-development.md`。

## 先查事实

```bash
git status --short --branch
git log -5 --oneline
dotnet sln framework/XiHan.Framework.slnx list
```

实现前确认目标能力是否已存在、最低可成立层、直接依赖、相邻 Module 类和测试方式。

## 架构边界

- 依赖按 Utils、Metadata、Core、Domain、Application/Contracts、Infrastructure、Web 单向流动。
- 模块通过 `[DependsOn]` 显式声明依赖，不使用静态服务定位或复制代码形成隐式依赖。
- 抽象和契约下沉，具体 Provider 上浮；底层模块不引用应用、Web 或外部基础设施实现。
- Nullable、异常、取消、线程安全和生命周期顺序是公共契约，不用默认值掩盖非法状态。
- 异步 API 接受并传递 `CancellationToken`，不阻塞异步调用或吞异常。
- 破坏性 API 变化明确迁移方式，不保留推测性兼容或静默兜底。

## 完成条件

契约、实现、服务注册、测试、XML 文档、README 和必要包元数据保持一致。先运行目标项目测试，再执行 Release 构建；一项框架能力独立提交。
