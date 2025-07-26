![logo](./assets/logo.png)

[English](README.md)

# XiHan.Framework

曦寒框架存储库。快速、轻量、高效、用心的开发框架，基于 .NET 9 构建。

[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/XiHanFun/XiHan.Framework)

## 项目概览

**XiHan.Framework** 是一个基于 .NET 9 的现代化、模块化的企业级开发框架，专为前后端分离的 ASP.NET Core 应用设计。框架优先使用 .NET 9 原生功能，减少第三方依赖，确保模块化、可扩展和易用性。

### 🚀 核心特性

- **📦 模块化架构** - 高度可扩展的模块化设计，按需选择
- **⚡ 快速启动** - 下载即可运行，快速体验完整的 Web API 项目
- **🎯 .NET 9 优先** - 充分利用 .NET 9 原生功能（DI、日志、序列化、AOT）
- **🏗️ DDD 支持** - 完整的领域驱动设计架构支持
- **🔒 企业级安全** - 完善的认证授权和安全机制
- **🌐 前后端分离** - 专为现代 Web 应用设计
- **📊 监控与日志** - 完整的监控、日志和性能分析

## 架构设计

### 包架构

框架采用模块化包设计，用户可按需选择：

#### 🏆 快速启动包（核心）

```bash
# 最小化配置 - 5 分钟启动 Web API
dotnet add package XiHan.Framework
dotnet add package XiHan.Framework.Web.Api
dotnet add package XiHan.Framework.Web.Docs
dotnet add package XiHan.Framework.Logging
```

#### 📋 完整功能包（推荐）

```bash
# 生产级 Web API 解决方案
dotnet add package XiHan.Framework.Authentication  # 认证
dotnet add package XiHan.Framework.Data           # 数据访问
dotnet add package XiHan.Framework.Validation     # 数据验证
dotnet add package XiHan.Framework.Caching        # 缓存管理
```

#### 🔧 扩展功能包（按需）

```bash
# 高级功能扩展
dotnet add package XiHan.Framework.Web.RealTime      # 实时通信
dotnet add package XiHan.Framework.BackgroundJobs    # 后台任务
dotnet add package XiHan.Framework.Messaging         # 消息队列
dotnet add package XiHan.Framework.SearchEngines     # 全文搜索
dotnet add package XiHan.Framework.MultiTenancy      # 多租户
```

### 分层架构

```
┌─────────────────────────────────────────────────────────────┐
│                        表现层                                │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  Web API        │  │  SignalR        │  │  Swagger/Scalar │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
├─────────────────────────────────────────────────────────────┤
│                        应用层                                │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  Application    │  │  Background Jobs│  │  AI Services    │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
├─────────────────────────────────────────────────────────────┤
│                        领域层                                │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  Domain Models  │  │  Domain Events  │  │  Domain Services│ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
├─────────────────────────────────────────────────────────────┤
│                        基础设施层                            │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  Data Access    │  │  Caching & MQ   │  │  External APIs  │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

## 技术栈

### 核心技术

- **.NET 9** - 基础运行时，支持 AOT 编译
- **ASP.NET Core** - Web 框架
- **System.Text.Json** - 高性能序列化
- **Entity Framework Core** - ORM 框架
- **Swagger/Scalar** - API 文档

### 认证授权

- **JWT** - JSON Web Token
- **OAuth 2.0** - 开放授权协议
- **OpenID Connect** - 身份认证协议
- **ASP.NET Core Identity** - 身份管理

### 扩展技术

- **Redis** - 分布式缓存
- **SignalR** - 实时通信
- **Hangfire/Quartz.NET** - 后台任务
- **RabbitMQ/Kafka** - 消息队列
- **Elasticsearch** - 全文搜索
- **ML.NET** - 机器学习

## 快速开始

### 1. 创建项目

```bash
dotnet new webapi -n MyApi
cd MyApi
```

### 2. 安装框架（最小配置）

```bash
# 元数据包（必需）
dotnet add package XiHan.Framework

# Web API 核心包
dotnet add package XiHan.Framework.Web.Api
dotnet add package XiHan.Framework.Web.Docs
dotnet add package XiHan.Framework.Logging
```

### 3. 基础配置

```csharp
// Program.cs
using XiHan.Framework.Web.Api;

var builder = WebApplication.CreateBuilder(args);

// 添加框架服务
builder.Services.AddXiHanWebApi();
builder.Services.AddXiHanDocs();
builder.Services.AddXiHanLogging();

var app = builder.Build();

// 配置中间件
app.UseXiHanWebApi();
app.UseXiHanDocs();  // 自动生成 Swagger 文档

app.Run();
```

### 4. 运行项目

```bash
dotnet run
```

访问 `https://localhost:5001/swagger` 查看 API 文档！

### 5. 添加业务功能（可选）

```bash
# 添加认证和数据访问
dotnet add package XiHan.Framework.Authentication
dotnet add package XiHan.Framework.Data
dotnet add package XiHan.Framework.Validation
```

```csharp
// 更新 Program.cs
builder.Services.AddXiHanAuthentication();
builder.Services.AddXiHanData(options =>
{
    options.UseInMemoryDatabase("MyDb"); // 或使用 SQL Server
});
builder.Services.AddXiHanValidation();

app.UseXiHanAuthentication();
```

## 开发计划

### 🎯 2024 年 Q4 (当前阶段)

- ✅ 完成元数据包开发
- 🔄 完成快速启动核心包
- 🔄 完成 Web API 和文档包
- 🔄 **发布 v0.1.0-alpha** - 基础可运行版本

### 🚀 2025 年 Q1

- 完成认证授权和数据访问包
- **发布 v0.2.0-beta** - 完整 API 功能
- 完成系统功能包

### 📦 2025 年 Q2

- 完成高级数据包和开发工具
- **发布 v1.0.0** - 稳定生产版本

### 🔧 2025 年 Q3-Q4

- 完成所有扩展功能包
- **发布 v1.1.0+** - 完整功能版本

## 设计原则

### 🎯 快速启动优先

- 用户下载即可运行完整 Web API
- 最小化配置，最大化体验
- 交互式 API 文档开箱即用

### 🧩 模块化设计

- 单一职责，清晰依赖
- 用户按需选择模块
- 支持独立开发和测试

### ⚡ .NET 9 优先

- 优先使用内置功能（DI、日志、序列化）
- 仅在必要时兼容第三方库
- 支持 AOT 编译和高性能特性

### 🌐 国际化友好

- 提供中文文档和示例
- 支持国内 NuGet 镜像
- 兼容国际标准（OpenAPI、gRPC）

## 版本信息

- **当前版本**: 0.11.7-preview.3
- **目标框架**: .NET 9.0
- **许可证**: MIT
- **开发状态**: 活跃开发中

## 贡献

欢迎提交 Issue 和 Pull Request 来改进这个框架。

### 贡献指南

1. Fork 项目
2. 创建功能分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 打开 Pull Request

## 许可证

本项目采用 MIT 许可证 - 查看 [LICENSE](LICENSE) 文件了解详情。

## 联系方式

- **作者**: ZhaiFanhua
- **邮箱**: me@zhaifanhua.com
- **项目地址**: [GitHub](https://github.com/XiHanFun/XiHan.Framework) | [Gitee](https://gitee.com/XiHanFun/XiHan.Framework)
- **文档**: [开发文档](https://docs.xihan.fun)

---

_曦寒框架，让 .NET 开发更简单。_
