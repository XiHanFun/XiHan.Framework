# XiHan.Framework.Authorization

## 概述
XiHan.Framework.Authorization 提供授权与权限控制的基础能力，统一权限检查、角色策略与授权结果模型。

## 核心能力
- 权限与角色授权的基础抽象
- 授权策略与权限检查流程
- 与认证、缓存等能力协同
- 可配置条件的求值（`Conditions`）：属性登记、条件编译与求值，供应用把库里配置的条件（如仅办公网、仅工作时间）接进授权或请求拦截

## 依赖关系
- 通过 `XiHanAuthorizationModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 授权规则通过 Options 类型承载
- 建议在业务模块中定义权限与策略边界

## 使用方式
```csharp
[DependsOn(typeof(XiHanAuthorizationModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义权限存储与校验逻辑
- 授权策略与规则解析扩展
- 条件属性登记（`AddXiHanConditionAttributes`）与属性来源（`IConditionAttributeSource`）

## 目录结构
```text
XiHan.Framework.Authorization/
  README.md
  XiHanAuthorizationModule.cs
```
