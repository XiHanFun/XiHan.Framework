![logo](./assets/logo.png)

[![Ask DeepWiki](./assets/badge.svg)](https://deepwiki.com/XiHanFun/XiHan.Framework)

# XiHan.Framework

快速、轻量、高效、用心的开发框架，基于 .NET 10 构建。

## 概述

XiHan.Framework 是面向企业级应用的模块化后端框架。强调模块清晰、依赖可控、扩展可维护，并以应用服务与动态 API 约定支持前后端分离场景。

## 设计目标
- 模块化分层清晰，依赖关系可追踪
- 以框架内置能力为优先，降低外部依赖
- 统一应用服务与动态 API 暴露规则
- 为业务模块提供一致的基础设施能力

## 架构概览
框架采用模块化分层组织，主要包括：
- 基础层：核心模块、工具与元数据
- 应用层：应用服务与契约定义
- 领域层：领域模型与共享模型
- 基础设施层：数据访问、日志、缓存、消息与安全
- Web 层：API、文档、网关、实时通信与 gRPC

## 模块清单
| 模块/项目 | 说明 |
| --- | --- |
| XiHan.Framework.AI | AI 能力的基础入口与扩展点 |
| XiHan.Framework.Application | 应用层基础设施与应用服务基类 |
| XiHan.Framework.Application.Contracts | 应用服务契约与 DTO 定义 |
| XiHan.Framework.Authentication | 认证流程与策略基础能力 |
| XiHan.Framework.Authorization | 授权与权限控制基础能力 |
| XiHan.Framework.Bot | 机器人接入与消息交互基础能力 |
| XiHan.Framework.Caching | 缓存能力的统一抽象与注册 |
| XiHan.Framework.Core | 模块化与应用生命周期核心基础设施 |
| XiHan.Framework.Data | SqlSugar 数据访问与仓储基础能力 |
| XiHan.Framework.DevTools | 开发期辅助与调试能力入口 |
| XiHan.Framework.DistributedIds | 分布式 ID 生成能力 |
| XiHan.Framework.Domain | 领域层基础设施与约定 |
| XiHan.Framework.Domain.Shared | 领域共享模型与基础类型 |
| XiHan.Framework.EventBus | 事件总线实现与集成入口 |
| XiHan.Framework.EventBus.Abstractions | 事件总线抽象契约 |
| XiHan.Framework.Http | HTTP 客户端与请求管道基础能力 |
| XiHan.Framework.Localization | 本地化能力实现与集成入口 |
| XiHan.Framework.Localization.Abstractions | 本地化抽象契约 |
| XiHan.Framework.Logging | 日志基础能力与扩展点 |
| XiHan.Framework.Messaging | 消息处理与发送能力入口 |
| XiHan.Framework.Metadata | 框架元数据与版本信息 |
| XiHan.Framework.MultiTenancy | 多租户能力实现与集成入口 |
| XiHan.Framework.MultiTenancy.Abstractions | 多租户抽象契约 |
| XiHan.Framework.ObjectMapping | 对象映射能力与规则管理 |
| XiHan.Framework.ObjectStorage | 对象存储能力与适配入口 |
| XiHan.Framework.Observability | 监控与可观测性能力入口 |
| XiHan.Framework.Script | 脚本引擎与执行能力 |
| XiHan.Framework.SearchEngines | 搜索引擎接入与扩展 |
| XiHan.Framework.Security | 安全与加密相关能力 |
| XiHan.Framework.Serialization | 序列化配置与策略管理 |
| XiHan.Framework.Settings | 设置管理与多来源配置 |
| XiHan.Framework.Tasks | 定时任务与后台服务能力 |
| XiHan.Framework.Templating | 模板渲染与资源加载能力 |
| XiHan.Framework.Threading | 并发控制与线程工具 |
| XiHan.Framework.Timing | 时间策略与时区能力 |
| XiHan.Framework.Traffic | 流量治理与限流能力 |
| XiHan.Framework.Uow | 工作单元与事务边界能力 |
| XiHan.Framework.Upgrade | 分布式升级引擎基础能力 |
| XiHan.Framework.Utils | 通用工具与辅助能力 |
| XiHan.Framework.Validation | 校验能力实现与集成 |
| XiHan.Framework.Validation.Abstractions | 校验抽象契约 |
| XiHan.Framework.VirtualFileSystem | 虚拟文件系统能力 |
| XiHan.Framework.Web.Api | Web API 与动态 API 支持 |
| XiHan.Framework.Web.Core | Web 基础设施与管道能力 |
| XiHan.Framework.Web.Docs | OpenAPI 文档能力 |
| XiHan.Framework.Web.Gateway | 网关能力与入口管理 |
| XiHan.Framework.Web.Grpc | gRPC 服务集成 |
| XiHan.Framework.Web.RealTime | 实时通信基础能力 |

## 基本使用
```csharp
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Web.Api;

[DependsOn(typeof(XiHanWebApiModule))]
public class MyAppModule : XiHanModule
{
}
```

```csharp
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Web.Core.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
await builder.AddApplicationAsync<MyAppModule>();

var app = builder.Build();
await app.InitializeApplicationAsync();
await app.RunAsync();
```
