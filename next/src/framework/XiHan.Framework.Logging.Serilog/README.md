# XiHan.Framework.Logging.Serilog

## 项目定位

`XiHan.Framework.Logging.Serilog` 提供基于 `Serilog` 的结构化日志实现。

## 责任边界

本项目负责：

1. 对接 `Serilog`
2. 提供结构化日志实现入口
3. 为后续接入文件、控制台、集中式日志平台做准备

本项目不负责：

1. 框架日志抽象定义
2. 宿主日志配置编排策略本身

## 依赖方向

本项目依赖 `Logging.Abstractions`，并使用社区成熟且活跃度高的 `Serilog` 生态。

## 当前阶段

当前项目已建立最小结构化日志适配器，后续将继续补充宿主注册、日志 enrichers 和输出目标接入。
