![logo](./assets/logo.png)

[中文](README_CN.md)

# XiHan.Framework

XiHan framework repository. Fast, lightweight, efficient, and dedicated development framework. Built on .NET 10.

[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/XiHanFun/XiHan.Framework)

## Project Overview

**XiHan.Framework** is a modern, modular enterprise-level development framework based on .NET 10, specifically designed for front-end and back-end separated ASP.NET Core applications. The framework prioritizes .NET 10 native features, reduces third-party dependencies, and ensures modularity, extensibility, and ease of use.

### 🚀 Core Features

- **📦 Modular Architecture** - Highly extensible modular design with on-demand selection
- **⚡ Quick Start** - Download and run instantly, quickly experience complete Web API projects
- **🎯 .NET 10 First** - Fully leverage .NET 10 native features (DI, logging, serialization, AOT)
- **🏗️ DDD Support** - Complete Domain-Driven Design architecture support
- **🔒 Enterprise Security** - Comprehensive authentication, authorization, and security mechanisms
- **🌐 Frontend-Backend Separation** - Designed for modern web applications
- **📊 Monitoring & Logging** - Complete monitoring, logging, and performance analysis

## Architecture Design

### Package Architecture

The framework adopts a modular package design that users can select on demand:

#### 🏆 Quick Start Packages (Core)

```bash
# Minimal configuration - Start Web API in 5 minutes
dotnet add package XiHan.Framework
dotnet add package XiHan.Framework.Web.Api
dotnet add package XiHan.Framework.Web.Docs
dotnet add package XiHan.Framework.Logging
```

#### 📋 Complete Feature Packages (Recommended)

```bash
# Production-ready Web API solution
dotnet add package XiHan.Framework.Authentication  # Authentication
dotnet add package XiHan.Framework.Data           # Data access
dotnet add package XiHan.Framework.Validation     # Data validation
dotnet add package XiHan.Framework.Caching        # Cache management
```

#### 🔧 Extension Packages (As Needed)

```bash
# Advanced feature extensions
dotnet add package XiHan.Framework.Web.RealTime      # Real-time communication
dotnet add package XiHan.Framework.BackgroundJobs    # Background jobs
dotnet add package XiHan.Framework.Messaging         # Message queues
dotnet add package XiHan.Framework.SearchEngines     # Full-text search
dotnet add package XiHan.Framework.MultiTenancy      # Multi-tenancy
```

### Layered Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Presentation Layer                       │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  Web API        │  │  SignalR        │  │  Swagger/Scalar │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
├─────────────────────────────────────────────────────────────┤
│                    Application Layer                        │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  Application    │  │  Background Jobs│  │  AI Services    │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
├─────────────────────────────────────────────────────────────┤
│                    Domain Layer                             │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  Domain Models  │  │  Domain Events  │  │  Domain Services│ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
├─────────────────────────────────────────────────────────────┤
│                  Infrastructure Layer                       │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐ │
│  │  Data Access    │  │  Caching & MQ   │  │  External APIs  │ │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

## Technology Stack

### Core Technologies

- **.NET 10** - Base runtime with AOT compilation support
- **ASP.NET Core** - Web framework
- **System.Text.Json** - High-performance serialization
- **Entity Framework Core** - ORM framework
- **Swagger/Scalar** - API documentation

### Authentication & Authorization

- **JWT** - JSON Web Token
- **OAuth 2.0** - Open authorization protocol
- **OpenID Connect** - Identity authentication protocol
- **ASP.NET Core Identity** - Identity management

### Extension Technologies

- **Redis** - Distributed caching
- **SignalR** - Real-time communication
- **Hangfire/Quartz.NET** - Background jobs
- **RabbitMQ/Kafka** - Message queues
- **Elasticsearch** - Full-text search
- **ML.NET** - Machine learning

## Quick Start

### 1. Create Project

```bash
dotnet new webapi -n MyApi
cd MyApi
```

### 2. Install Framework (Minimal Configuration)

```bash
# Metadata package (required)
dotnet add package XiHan.Framework

# Web API core packages
dotnet add package XiHan.Framework.Web.Api
dotnet add package XiHan.Framework.Web.Docs
dotnet add package XiHan.Framework.Logging
```

### 3. Basic Configuration

```csharp
// Program.cs
using XiHan.Framework.Web.Api;

var builder = WebApplication.CreateBuilder(args);

// Add framework services
builder.Services.AddXiHanWebApi();
builder.Services.AddXiHanDocs();
builder.Services.AddXiHanLogging();

var app = builder.Build();

// Configure middleware
app.UseXiHanWebApi();
app.UseXiHanDocs();  // Auto-generate Swagger documentation

app.Run();
```

### 4. Run Project

```bash
dotnet run
```

Visit `https://localhost:5001/swagger` to view API documentation!

### 5. Add Business Features (Optional)

```bash
# Add authentication and data access
dotnet add package XiHan.Framework.Authentication
dotnet add package XiHan.Framework.Data
dotnet add package XiHan.Framework.Validation
```

```csharp
// Update Program.cs
builder.Services.AddXiHanAuthentication();
builder.Services.AddXiHanData(options =>
{
    options.UseInMemoryDatabase("MyDb"); // Or use SQL Server
});
builder.Services.AddXiHanValidation();

app.UseXiHanAuthentication();
```

## Development Roadmap

### 🎯 2024 Q4 (Current Phase)

- ✅ Complete metadata package development
- 🔄 Complete quick start core packages
- 🔄 Complete Web API and documentation packages
- 🔄 **Release v0.1.0-alpha** - Basic runnable version

### 🚀 2025 Q1

- Complete authentication, authorization, and data access packages
- **Release v0.2.0-beta** - Complete API functionality
- Complete system function packages

### 📦 2025 Q2

- Complete advanced data packages and development tools
- **Release v1.0.0** - Stable production version

### 🔧 2025 Q3-Q4

- Complete all extension packages
- **Release v1.1.0+** - Complete feature version

## Design Principles

### 🎯 Quick Start First

- Users can run complete Web API immediately after download
- Minimal configuration, maximum experience
- Interactive API documentation out of the box

### 🧩 Modular Design

- Single responsibility, clear dependencies
- Users select modules on demand
- Support independent development and testing

### ⚡ .NET 10 First

- Prioritize built-in features (DI, logging, serialization)
- Only use third-party libraries when necessary
- Support AOT compilation and high-performance features

### 🌐 Internationalization Friendly

- Provide Chinese documentation and examples
- Support domestic NuGet mirrors
- Compatible with international standards (OpenAPI, gRPC)

## Version Information

- **Current Version**: 0.11.7-preview.3
- **Target Framework**: .NET 10.0
- **License**: MIT
- **Development Status**: Actively Developing

## Contributing

We welcome Issue submissions and Pull Requests to improve this framework.

### Contribution Guidelines

1. Fork the project
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contact

- **Author**: ZhaiFanhua
- **Email**: me@zhaifanhua.com
- **Project URLs**: [GitHub](https://github.com/XiHanFun/XiHan.Framework) | [Gitee](https://gitee.com/XiHanFun/XiHan.Framework)
- **Documentation**: [Development Docs](https://docs.xihan.fun)

---

_XiHan Framework, making .NET development simpler._
