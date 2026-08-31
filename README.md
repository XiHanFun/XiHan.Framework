<div align="center">
<img src="./assets/banner.png" alt="XiHan.Framework" />

<h1>XiHan.Framework</h1>

<p><b>A fast, lightweight, efficient and thoughtfully built modern modular framework for .NET</b></p>

<p>Built on .NET 10 · 66 modular components · <code>[DependsOn]</code> declarations · topologically sorted loading</p>

<p><b>English</b> | <a href="./README_cn.md">简体中文</a></p>

<p>
  <a href="https://github.com/XiHanFun/XiHan.Framework/stargazers"><img alt="GitHub Stars" src="https://img.shields.io/github/stars/XiHanFun/XiHan.Framework?style=flat-square&logo=github&label=Stars&color=1f6feb" /></a>
  <a href="https://gitee.com/XiHanFun/XiHan.Framework"><img alt="Gitee Stars" src="https://gitee.com/XiHanFun/XiHan.Framework/badge/star.svg" /></a>
  <a href="https://gitcode.com/XiHanFun/XiHan.Framework"><img alt="GitCode Stars" src="https://gitcode.com/XiHanFun/XiHan.Framework/star/badge.svg" /></a>
</p>
<p>
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet&logoColor=white" />
  <img alt="C#" src="https://img.shields.io/badge/C%23-Latest-239120?style=flat-square" />
  <img alt="Modules" src="https://img.shields.io/badge/Modules-66-1f6feb?style=flat-square" />
  <a href="https://www.nuget.org/packages?q=XiHan.Framework"><img alt="NuGet" src="https://img.shields.io/nuget/v/XiHan.Framework.Core?style=flat-square&logo=nuget&logoColor=white&label=NuGet&color=004880" /></a>
  <a href="https://www.nuget.org/packages/XiHan.Framework.Core"><img alt="Downloads" src="https://img.shields.io/nuget/dt/XiHan.Framework.Core?style=flat-square&logo=nuget&logoColor=white&label=Downloads&color=004880" /></a>
</p>



<p>
  <a href="./LICENSE"><img alt="License" src="https://img.shields.io/github/license/XiHanFun/XiHan.Framework?style=flat-square&color=green" /></a>
  <a href="https://github.com/XiHanFun/XiHan.Framework/commits"><img alt="Last Commit" src="https://img.shields.io/github/last-commit/XiHanFun/XiHan.Framework?style=flat-square&color=blueviolet" /></a>
  <img alt="Commit Activity" src="https://img.shields.io/github/commit-activity/m/XiHanFun/XiHan.Framework?style=flat-square" />
  <a href="https://github.com/XiHanFun/XiHan.Framework/issues"><img alt="Issues" src="https://img.shields.io/github/issues/XiHanFun/XiHan.Framework?style=flat-square" /></a>
  <a href="https://github.com/XiHanFun/XiHan.Framework/graphs/contributors"><img alt="Contributors" src="https://img.shields.io/github/contributors/XiHanFun/XiHan.Framework?style=flat-square" /></a>
  <img alt="Repo Size" src="https://img.shields.io/github/repo-size/XiHanFun/XiHan.Framework?style=flat-square" />
</p>

<p>
  <a href="https://deepwiki.com/XiHanFun/XiHan.Framework"><img alt="Ask DeepWiki" src="https://deepwiki.com/badge.svg" /></a>
  <a href="https://framework.docs.xihanfun.com"><img alt="Docs" src="https://img.shields.io/badge/Docs-framework.docs.xihanfun.com-2496ED?style=flat-square&logo=readthedocs&logoColor=white" /></a>
  <a href="https://qm.qq.com/q/qYp1Urv3z2"><img alt="QQ Group" src="https://img.shields.io/badge/QQ_Group-462371834-EB1923?style=flat-square&logo=tencentqq&logoColor=white" /></a>
</p>
<p>
  <a href="https://trendshift.io/repositories/83128?utm_source=trendshift-badge&amp;utm_medium=badge&amp;utm_campaign=badge-trendshift-83128" target="_blank" rel="noopener noreferrer"><img src="https://trendshift.io/api/badge/trendshift/repositories/83128/daily?language=C%23" alt="XiHanFun%2FXiHan.Framework | Trendshift" width="250" height="55"/></a>
</p>

</div>

## Overview

XiHan.Framework is a modular backend framework for enterprise applications, designed for ASP.NET Core services in a decoupled frontend/backend setup. It prefers what .NET already provides over third-party libraries, and puts the emphasis on clear module boundaries, controlled dependencies and maintainable extension points. Modules declare their dependencies with the `[DependsOn]` attribute and are loaded in topological order; application services plus dynamic API conventions give every module the same way to expose its endpoints.

## Documentation

| Destination | Contents |
| --- | --- |
| [Documentation site](https://framework.docs.xihanfun.com) | Full guides and per-package API docs for all 66 packages |
| [Framework engineering notes](./framework/README.md) | Layered architecture, module catalog, directory layout, dependencies |
| [Changelog](https://framework.docs.xihanfun.com/changelog) | Release notes and upgrade advisories |
| [Contributing guide](./CONTRIBUTING.md) | Branch conventions, commit rules, local build and test |

## Design Principles

- **Layered architecture** — follow clear layering, no circular dependencies
- **Dependency inversion** — higher layers do not depend on lower ones; both depend on abstractions
- **Single responsibility** — each package owns exactly one functional area
- **Open/closed** — open for extension, closed for modification, customizable through interfaces and base classes
- **.NET 10 first** — use the built-ins (DI, logging, serialization) and reach for third-party libraries only when necessary
- **Performance** — built on .NET 10's high-performance features; AOT is out of scope (SqlSugar / Castle DynamicProxy / Newtonsoft.Json are not trimming-compatible yet)

## Tech Stack

| Category | Technology |
| --- | --- |
| Runtime | .NET |
| Language | C# |
| ORM | SqlSugarCore |
| Logging | Serilog.AspNetCore |
| Caching | Microsoft.Extensions.Caching.Hybrid + StackExchangeRedis |
| AOP | Castle.Core (DynamicProxy) |
| Cryptography | BouncyCastle.Cryptography |
| Serialization | System.Text.Json (built-in) + Newtonsoft.Json |
| Templating | Scriban |
| AI | Microsoft.Extensions.AI + Microsoft.Agents.AI + MCP |
| HTTP resilience | Microsoft.Extensions.Http.Polly |
| gRPC | Grpc.AspNetCore |
| Realtime | ASP.NET Core SignalR |
| API docs | Scalar.AspNetCore + Swashbuckle.AspNetCore |
| IP geolocation | IP2Region.Net |
| Notifications | MailKit + Telegram.Bot |
| Search | Elastic.Clients.Elasticsearch |
| Testing | xunit.v3 + Microsoft.Testing.Platform (with the CodeCoverage extension) |

Exact versions live in the `PackageReference` entries under `framework/src`.

## Getting Started

### Install

Install the modules you need from NuGet:

```bash
# Core module
dotnet add package XiHan.Framework.Core

# Web API module (includes the full middleware pipeline)
dotnet add package XiHan.Framework.Web.Api

# API documentation module
dotnet add package XiHan.Framework.Web.Docs

# Data access module
dotnet add package XiHan.Framework.Data
```

### Define a Module

Every module derives from `XiHanModule` and declares its dependencies with `[DependsOn]`:

```csharp
using XiHan.Framework.Core.Modularity;
using XiHan.Framework.Web.Api;
using XiHan.Framework.Data;

[DependsOn(
    typeof(XiHanWebApiModule),
    typeof(XiHanDataModule)
)]
public class MyAppModule : XiHanModule
{
    public override Task ConfigureServicesAsync(ServiceConfigurationContext context)
    {
        // Register services
        return Task.CompletedTask;
    }

    public override Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        // Initialize the application
        return Task.CompletedTask;
    }
}
```

### Bootstrap the Application

```csharp
using XiHan.Framework.Core.Extensions.DependencyInjection;
using XiHan.Framework.Web.Core.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
await builder.AddApplicationAsync<MyAppModule>();

var app = builder.Build();
await app.InitializeApplicationAsync();
await app.RunAsync();
```

### Module Lifecycle

Each module exposes 7 lifecycle hooks, executed in topological order:

```text
Service registration                  Application initialization
┌──────────────────────┐            ┌────────────────────────────────┐
│ PreConfigureServices │            │ OnPreApplicationInitialization │
│ ConfigureServices    │     →      │ OnApplicationInitialization    │
│ PostConfigureServices│            │ OnPostApplicationInitialization│
└──────────────────────┘            └────────────────────────────────┘
                                                  ↓
                                    ┌────────────────────────────────┐
                                    │ OnApplicationShutdown          │
                                    └────────────────────────────────┘
```

## NuGet Packages

Every module is published to [NuGet.org](https://www.nuget.org/packages?q=XiHan.Framework); package names match project names:

```bash
# Search all XiHan.Framework packages
dotnet package search XiHan.Framework
```

| Common package | Purpose |
| --- | --- |
| `XiHan.Framework.Core` | Modularity core (required) |
| `XiHan.Framework.Web.Api` | Full Web API middleware pipeline |
| `XiHan.Framework.Web.Docs` | Scalar + Swagger documentation |
| `XiHan.Framework.Data` | SqlSugar data access |
| `XiHan.Framework.Caching` | HybridCache + Redis |
| `XiHan.Framework.Authentication` | JWT / OAuth2 authentication |
| `XiHan.Framework.Authorization` | RBAC authorization |
| `XiHan.Framework.EventBus` | Event bus + outbox |
| `XiHan.Framework.AI` | Microsoft.Extensions.AI + MCP |

The full module catalog lives in the [framework engineering notes](./framework/README.md#module-catalog).

## Requirements

| Dependency | Version |
| --- | --- |
| .NET SDK | 10.0+ |
| C# | Latest |
| Platforms | Windows / Linux / macOS |

## Ecosystem

- [XiHan.Framework](https://github.com/XiHanFun/XiHan.Framework) - A fast, lightweight, efficient and thoughtfully built modern modular framework for .NET
- [XiHan.UI](https://github.com/XiHanFun/XiHan.UI) - A fast, lightweight, efficient and thoughtfully built framework-agnostic headless UI component library
- [XiHan.BasicApp](https://github.com/XiHanFun/XiHan.BasicApp) - A beautifully crafted general-purpose admin kernel built on .NET (XiHan.Framework) and TypeScript (XiHan.UI)

## Contributing

Issues and pull requests are welcome — see the [contributing guide](./CONTRIBUTING.md).

## Acknowledgements

In no particular order.

| Project                                    | Thanks for                                        |
| ------------------------------------------ | ------------------------------------------------- |
| [Abp](https://github.com/abpframework/abp) | Inspiring parts of the architecture and design    |
| Other third-party dependencies             | Being the foundation this project is built upon   |


## Support & Sponsorship

If this project helps your work, feel free to buy the author a coffee.

Official sponsorship page: https://docs.xihanfun.com/cosmos/sponsor


## License

Copyright (c) 2021-Present XiHanFun and contributors.

Released under the MIT License — see [License](./LICENSE).

The XiHan.Framework logo and name belong to the author; third-party dependencies and services are governed by their own licenses and terms.

This project is provided for study and reference; the author assumes no liability for any use of the software.
