# XiHan.Framework.Uow

## 概述
XiHan.Framework.Uow 提供工作单元能力，统一事务边界、提交与回滚策略。

## 核心能力
- 工作单元接口与生命周期管理
- 事务边界统一与资源协调
- 与数据层与仓储层协同

## 依赖关系
- 通过 `XiHanUowModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 工作单元行为以代码约定为主
- 业务侧可定义事务范围与提交策略

## 使用方式
```csharp
[DependsOn(typeof(XiHanUowModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义工作单元实现
- 自定义事务边界与提交策略

## 目录结构
```text
XiHan.Framework.Uow/
  README.md
  XiHanUowModule.cs
```
