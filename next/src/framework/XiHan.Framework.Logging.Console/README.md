# XiHan.Framework.Logging.Console

## 项目定位

`XiHan.Framework.Logging.Console` 提供基于微软控制台日志组件的默认控制台日志实现。

## 责任边界

本项目负责：

1. 对接微软控制台日志 Provider
2. 提供最基础的本地开发期日志输出能力

本项目不负责：

1. 日志抽象定义
2. 结构化日志平台实现

## 依赖方向

本项目依赖 `Logging.Abstractions`，优先采用微软官方控制台日志包。

## 当前阶段

当前项目用于提供最小控制台日志实现，后续可继续补充与宿主装配相关的注册扩展。
