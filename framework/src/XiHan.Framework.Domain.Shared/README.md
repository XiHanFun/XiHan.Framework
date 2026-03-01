# XiHan.Framework.Domain.Shared

## 概述
XiHan.Framework.Domain.Shared 提供领域共享模型与基础定义，包含通用实体、值对象、枚举与跨领域共用契约。

## 核心能力
- 跨模块共享的领域模型与基础类型
- 通用实体基类与审计相关定义
- 领域共享常量与基础约定

## 依赖关系
- 通过 `XiHanDomainSharedModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 共享模型应保持稳定以降低模块耦合
- DTO 与领域模型边界需清晰区分

## 使用方式
```csharp
[DependsOn(typeof(XiHanDomainSharedModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 共享实体与值对象的扩展
- 通用领域模型的规范化约束

## 目录结构
```text
XiHan.Framework.Domain.Shared/
  README.md
  XiHanDomainSharedModule.cs
```
