# XiHan.Framework.Security

## 概述
XiHan.Framework.Security 提供通用安全相关能力，包括加密、哈希与安全策略的基础实现与扩展点。

## 核心能力
- 安全算法与加密能力的基础封装
- 密钥与安全策略的统一管理入口
- 与认证、授权等模块协同

## 依赖关系
- 通过 `XiHanSecurityModule` 参与模块化生命周期
- 依赖关系通过 `DependsOn` 进行组合，具体依赖以模块类声明为准

## 配置与约定
- 安全相关配置通过 Options 类型承载
- 建议在应用层统一管理密钥与安全参数

## 使用方式
```csharp
[DependsOn(typeof(XiHanSecurityModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 自定义加密算法与安全策略
- 自定义密钥管理与轮换策略

## 目录结构
```text
XiHan.Framework.Security/
  README.md
  XiHanSecurityModule.cs
```
