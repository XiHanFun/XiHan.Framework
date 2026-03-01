# XiHan.Framework.Application.Contracts

## 概述
XiHan.Framework.Application.Contracts 提供应用层契约定义，包括应用服务接口、DTO 与通用返回模型。

## 核心能力
- 应用服务接口与远程服务契约定义
- 通用 DTO、分页模型与请求/响应协议
- 与动态 API 暴露规则的契约协同

## 依赖关系
- 通过 `XiHanApplicationContractsModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 应用服务接口实现 `IApplicationService` 以纳入动态 API 生成
- DTO 与契约应保持稳定以支持版本兼容

## 使用方式
```csharp
[DependsOn(typeof(XiHanApplicationContractsModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义应用服务契约与 DTO
- 自定义应用级响应规范

## 目录结构
```text
XiHan.Framework.Application.Contracts/
  README.md
  XiHanApplicationContractsModule.cs
```
