# XiHan.Framework.Hosts.Api

## 项目定位

`XiHan.Framework.Hosts.Api` 是默认 REST API 宿主项目。

## 责任边界

本项目负责：

1. 程序启动入口
2. 中间件管道装配
3. OpenAPI 集成
4. Bootstrap 应用初始化
5. 模块与框架能力装配

本项目不负责：

1. 领域规则定义
2. 应用服务契约定义
3. 基础设施抽象定义

## 依赖方向

本项目可以依赖应用层和部分框架实现层，但不得反向成为框架层依赖。

## 当前阶段

当前项目已接入 `Bootstrap` 外部服务提供器模式，并通过 `ApiHostModule` 打通：

1. 宿主服务注册
2. 应用创建
3. WebApplication 回写
4. 模块初始化
5. 路由装配

后续将继续补充统一异常处理、认证授权、配置绑定和 OpenAPI 细化配置。
