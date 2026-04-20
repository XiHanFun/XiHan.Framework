# XiHan.Framework.Next

`XiHan.Framework.Next` 是 `XiHan.Framework` 的下一代架构工作区。

本目录从零开始设计，目标不是在旧结构上继续堆叠，而是在充分吸收现有 `framework/src` 经验和问题的基础上，建立一套可长期演进、职责边界清晰、可替换能力明确、适合未来十年以上持续扩展的框架级工程结构。

当前设计还额外遵守以下三条硬约束：

1. 依赖包优先选择微软官方包，其次选择社区活跃、维护稳定、下载量高的成熟包。
2. 正式项目与测试项目在构建、打包、发布流程上必须严格隔离。
3. 物理目录不过度嵌套，目录只表达最必要的归类，细粒度层次主要交给解决方案目录、项目命名和代码结构表达。

## 核心目标

1. 高内聚、低耦合
   每个项目只负责单一职责，避免“万能核心包”和“工具大杂烩”再次出现。
2. 稳定分层
   严格区分内核层、领域层、应用层、横切能力层、宿主层和测试层，避免依赖方向漂移。
3. 可替换实现
   所有基础设施能力优先抽象化，再接入具体 Provider，如数据、缓存、消息、对象存储、AI 与搜索。
4. 长期演进
   目录结构、项目命名、文档体系和集中构建配置从第一天开始统一，降低未来重构成本。
5. 中文优先
   本工作区的说明文档、架构文档、项目说明、XML 注释、设计决策记录默认全部使用中文。

## 顶层目录说明

- `src`
  只放项目代码，是整个工作区唯一的项目代码根目录。
- `docs`
  放架构说明、设计决策记录、开发指南、约束说明等文档。
- `tests`
  放单元测试、集成测试、架构测试、契约测试和性能测试项目。
- `tools`
  放代码生成器、脚本、模板和研发辅助工具。
- `build`
  放构建、发布、质量门禁和 CI/CD 相关资产。
- `infra`
  放本地开发和测试使用的基础设施配置，如容器、Compose、模拟依赖等。
- `samples`
  放示例项目、参考实现和最小可运行样例。

## src 目录结构

`src` 目录只保留三类一级子目录：

- `framework`
- `modules`
- `hosts`

后续所有正式项目都会直接放在这三类目录下的相对扁平结构中，不再为了“看起来分层”而增加多余目录深度。

### `src/framework`

框架自身代码统一采用扁平项目目录，不再通过多层物理目录表达“构建块”和“Provider”。

当前采用的方式是：

1. 直接在 `src/framework` 下放项目目录
2. 通过项目命名表达职责与归属
3. 通过解决方案目录表达逻辑分层

例如：

- `src/framework/XiHan.Framework.Kernel`
- `src/framework/XiHan.Framework.Shared`
- `src/framework/XiHan.Framework.Domain.Primitives`
- `src/framework/XiHan.Framework.Domain`
- `src/framework/XiHan.Framework.Data.Abstractions`
- `src/framework/XiHan.Framework.Data.EntityFrameworkCore`
- `src/framework/XiHan.Framework.Data.SqlSugar`
- `src/framework/XiHan.Framework.Logging.Abstractions`
- `src/framework/XiHan.Framework.Logging.Console`
- `src/framework/XiHan.Framework.Logging.Serilog`

### `src/modules`

业务模块目录。每个模块仍然按统一五层设计，但项目目录会尽量扁平：

- `Domain`
- `Application.Contracts`
- `Application`
- `Infrastructure`
- `Web`

这样做的目的，是让模块本身具备清晰边界，并可以按模块独立扩展、替换、裁剪和组合。

目录层面上，模块采用“模块目录 + 扁平项目目录”的形式，而不是继续深挖文件夹层级：

- `src/modules/identity/XiHan.Framework.Modules.Identity.Domain`
- `src/modules/identity/XiHan.Framework.Modules.Identity.Application.Contracts`
- `src/modules/identity/XiHan.Framework.Modules.Identity.Application`
- `src/modules/identity/XiHan.Framework.Modules.Identity.Infrastructure`
- `src/modules/identity/XiHan.Framework.Modules.Identity.Web`

当前阶段遵循一个额外规则：

1. 不预先创建空模块项目目录
2. 只有在真正开始实现某个模块时才创建对应目录和项目

### `src/hosts`

宿主层目录，用于承载最终运行入口，例如：

- `src/hosts/XiHan.Framework.Hosts.Api`
- `src/hosts/XiHan.Framework.Hosts.Gateway`
- `src/hosts/XiHan.Framework.Hosts.Grpc`
- `src/hosts/XiHan.Framework.Hosts.RealTime`

宿主层只做程序集成、启动配置、对外协议适配和运行时装配，不承载核心业务规则和基础设施抽象定义。

## 构建与发布隔离

为避免正式项目和测试项目在生命周期上互相污染，本工作区后续会明确分离：

1. 正式项目解决方案
2. 测试项目解决方案
3. 正式项目打包入口
4. 测试执行入口
5. 正式发布入口

即使测试项目依赖正式项目，也不能进入正式打包和发布清单。

## 当前阶段

当前已完成：

1. `next` 工作区目录初始化
2. `src` 下框架、模块、宿主三大代码域划分
3. `Directory.Build.props` 和 `Directory.Packages.props` 集中治理
4. `XiHan.Framework.Next.slnx` 解决方案初始化
5. 正式项目、测试项目、总览解决方案三套入口建立
6. 目录结构已统一到扁平物理目录风格

下一阶段将继续完成：

1. 首个真实业务模块落地
2. 继续补足尚未实现的框架能力
3. 中文架构文档、ADR 模板和模块模板补全
4. 架构测试与模块模板固化
