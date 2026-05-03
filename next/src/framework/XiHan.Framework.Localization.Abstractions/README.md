# XiHan.Framework.Localization.Abstractions

## 项目定位

`XiHan.Framework.Localization.Abstractions` 定义国际化能力的稳定抽象，用于隔离资源来源、文化上下文和字符串解析实现。

## 责任边界

本项目负责：

1. 本地化字符串提供器抽象
2. 文化上下文抽象
3. 资源解析器抽象

本项目不负责：

1. 资源文件读取实现
2. 数据库存储实现
3. HTTP 层语言解析实现

## 依赖方向

本项目只依赖 `Kernel` 或 BCL，不依赖具体本地化实现项目。

## 当前阶段

当前项目已建立最小本地化抽象，后续将继续补充资源提供器、文化上下文与资源命名约定。
