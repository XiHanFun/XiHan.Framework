# XiHan.Framework.Validation.Abstractions

## 项目定位

`XiHan.Framework.Validation.Abstractions` 定义统一输入验证抽象。

## 责任边界

本项目负责：

1. 验证器抽象
2. 验证结果模型约定
3. 应用层和 Web 层可复用的验证接口

本项目不负责：

1. 具体验证框架实现
2. 具体业务规则定义

## 依赖方向

本项目可以依赖共享模型，但不依赖具体验证实现。

## 当前阶段

当前项目已建立统一验证抽象，后续将继续补充验证错误模型和验证上下文契约。
