---
layout: home
title: 曦寒开发框架 5
titleTemplate: 语言中立的应用框架规范与 .NET 参考实现

hero:
  name: 曦寒开发框架 5
  text: 微内核、能力图谱与管道执行
  tagline: XiHan Spec 规范与 .NET 10 参考实现 · 一份操作契约经 HTTP、gRPC、MCP 与命令行暴露
  image:
    src: /images/logo.png
    alt: 曦寒开发框架
  actions:
    - theme: brand
      text: 快速上手
      link: /quickstart

    - theme: alt
      text: 框架简介
      link: /introduction

    - theme: alt
      text: 为什么选择曦寒
      link: /why

features:
  - title: 按能力 ID 装配
    details: 特性在清单里声明提供与依赖的能力，内核在启动前求解能力图谱、拓扑排序并打印结果。同一能力有多个 Provider 时按配置或清单角色选出一个，缺失与冲突在启动期报错。
    link: /guide/modularity
    linkText: 能力包与清单

  - title: 固定阶段的管道
    details: 每次调用依次经过 Ingress 到 Egress 十一个阶段。认证、租户、授权、校验、幂等、事务都是阶段里的中间件，顺序确定、可打印、可单独测试。
    link: /guide/pipeline
    linkText: 执行管道

  - title: 一份契约多协议
    details: 用 [Operation] 声明一次操作，源生成器产出登记代码与 JSON Schema；HTTP、gRPC、MCP 与命令行共用同一份 Schema、授权要求与错误码。
    link: /guide/operations
    linkText: 操作契约

  - title: 默认实现零外部依赖
    details: 每个子系统的抽象都配一个内存或文件系统实现，容量有界、通过一致性套件。换 Redis、EF Core、SqlSugar、RabbitMQ 只加包与配置，业务代码不变。
    link: /packages/
    linkText: 浏览包总览

  - title: 零反射与 Native AOT
    details: 原语层与内核不使用运行时反射，注册与 Schema 在编译期生成。示例应用以 Native AOT 发布作为 CI 冒烟项，非 AOT 的 Provider 在包页注明。
    link: /guide/aot
    linkText: 序列化与 Native AOT

  - title: 规范先于实现
    details: 原语、清单、调用上下文、管道阶段、协议映射与插件协议写在 XiHan Spec。Go SDK 与 .NET 参考实现通过同一套一致性向量，插件可用任意语言编写。
    link: /guide/plugins
    linkText: 插件
---
