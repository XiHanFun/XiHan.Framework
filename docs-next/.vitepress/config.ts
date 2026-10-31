import { createRequire } from "node:module";
import type { DefaultTheme } from "vitepress";
import { defineXiHanConfig } from "@xihanfun/vitepress-theme/config";

// 导航末项显示的版本号取自本站 package.json，发版时只改那一处
const { version } = createRequire(import.meta.url)("../package.json");

const title: string = "曦寒开发框架 5 文档";
const description: string = "语言中立的应用框架规范及其 .NET 参考实现";
const keywords: string =
  "曦寒,曦寒懿,开发框架,DotNet,微内核,能力图谱,操作契约,官方文档,开源,XiHanFun,XiHan.Framework";

// 生成单个包文档条目
function pkg(text: string, name: string): DefaultTheme.SidebarItem {
  return { text, link: `/packages/${name}` };
}

// 开发指南：按能力域编号；内容页在 guide/ 下
function chapter(
  index: number,
  text: string,
  link: string,
): DefaultTheme.SidebarItem {
  return { text: `${index}. ${text}`, link: `/${link}` };
}

// 每章一页，全部落在 guide/ 下；包文档是另一册（参考手册），章末链接过去。
// 4.x 的机器人、消息通知、模板引擎、脚本引擎、搜索、HTTP 客户端、网关、加解密、对象映射在 5.0 不再由框架包装，
// 不设章节，去向写在「从 4.x 迁移」
const guideChapters: [text: string, name: string][] = [
  ["能力包与清单", "modularity"],
  ["生命周期与健康", "lifecycle"],
  ["依赖注入", "dependency-injection"],
  ["执行管道", "pipeline"],
  ["配置与选项", "configuration"],
  ["宿主与 Web 应用", "web"],
  ["操作契约", "operations"],
  ["HTTP 与 OpenAPI", "http"],
  ["gRPC、MCP 与 CloudEvents", "protocols"],
  ["命令行宿主", "command-line"],
  ["结果与错误", "errors"],
  ["数据校验", "validation"],
  ["领域模型", "domain"],
  ["持久化", "persistence"],
  ["工作单元与事务", "uow"],
  ["多租户", "multi-tenancy"],
  ["认证", "authentication"],
  ["授权", "authorization"],
  ["缓存与分布式锁", "caching"],
  ["消息与事件", "messaging"],
  ["调度与后台作业", "scheduling"],
  ["工作流", "workflow"],
  ["实时通信", "realtime"],
  ["对象存储", "storage"],
  ["国际化", "localization"],
  ["审计", "auditing"],
  ["可观测性", "observability"],
  ["序列化与 Native AOT", "aot"],
  ["标识生成", "distributed-ids"],
  ["AI", "ai"],
  ["弹性与限流", "resilience"],
  ["插件", "plugins"],
  ["部署与打包", "deployment"],
  ["测试与一致性套件", "testing"],
  ["从 4.x 迁移", "upgrade"],
  ["扩展与二次开发", "extending"],
  ["常见问题", "faq"],
];

const startSidebar: DefaultTheme.SidebarItem[] = [
  {
    text: "开始",
    collapsed: false,
    items: [
      { text: "框架简介", link: "/introduction" },
      { text: "为什么选择曦寒", link: "/why" },
      { text: "框架概述", link: "/overview" },
      { text: "快速上手", link: "/quickstart" },
    ],
  },
  {
    text: "AI 辅助开发",
    collapsed: false,
    items: [
      { text: "For Agents", link: "/ai/agents" },
      { text: "MCP Server", link: "/ai/mcp" },
      { text: "CLI", link: "/ai/cli" },
      { text: "LLMs.txt", link: "/ai/llms" },
    ],
  },
  { text: "更新日志", link: "/changelog" },
];

const guideSidebar: DefaultTheme.SidebarItem[] = [
  {
    text: "开发指南",
    collapsed: false,
    items: guideChapters.map(([text, name], i) =>
      chapter(i + 1, text, `guide/${name}`),
    ),
  },
];

// 包按领域分组：每组先列抽象包，再列默认实现与 Provider；环层写在各包页的信息表里
const packagesSidebar: DefaultTheme.SidebarItem[] = [
  {
    text: "包总览",
    link: "/packages/",
    collapsed: false,
    items: [
      {
        text: "原语与内核",
        collapsed: true,
        items: [
          pkg("Primitives 原语层", "primitives"),
          pkg("Kernel 内核", "kernel"),
          pkg("Kernel.Generators 操作生成器", "kernel-generators"),
          pkg("Kernel.Analyzers 分析器", "kernel-analyzers"),
        ],
      },
      {
        text: "领域与持久化",
        collapsed: true,
        items: [
          pkg("Domain 领域模型", "domain"),
          pkg("Persistence.Abstractions", "persistence-abstractions"),
          pkg("Persistence.InMemory", "persistence-inmemory"),
          pkg("Persistence.EntityFrameworkCore", "persistence-entityframeworkcore"),
          pkg("Persistence.SqlSugar", "persistence-sqlsugar"),
          pkg("Persistence.Generators", "persistence-generators"),
        ],
      },
      {
        text: "多租户 · 安全",
        collapsed: true,
        items: [
          pkg("MultiTenancy.Abstractions", "multitenancy-abstractions"),
          pkg("MultiTenancy.InMemory", "multitenancy-inmemory"),
          pkg("MultiTenancy.Database", "multitenancy-database"),
          pkg("Security.Abstractions", "security-abstractions"),
          pkg("Security.InMemory", "security-inmemory"),
          pkg("Security.Database", "security-database"),
          pkg("Security.Jwt", "security-jwt"),
          pkg("Security.OpenIdConnect", "security-openidconnect"),
          pkg("Security.Ip2Region", "security-ip2region"),
        ],
      },
      {
        text: "缓存 · 消息",
        collapsed: true,
        items: [
          pkg("Caching.Abstractions", "caching-abstractions"),
          pkg("Caching.InMemory", "caching-inmemory"),
          pkg("Caching.Redis", "caching-redis"),
          pkg("Caching.Hybrid", "caching-hybrid"),
          pkg("Caching.Remote", "caching-remote"),
          pkg("Messaging.Abstractions", "messaging-abstractions"),
          pkg("Messaging.InMemory", "messaging-inmemory"),
          pkg("Messaging.RabbitMQ", "messaging-rabbitmq"),
          pkg("Messaging.Kafka", "messaging-kafka"),
        ],
      },
      {
        text: "调度 · 工作流",
        collapsed: true,
        items: [
          pkg("Scheduling.Abstractions", "scheduling-abstractions"),
          pkg("Scheduling.InMemory", "scheduling-inmemory"),
          pkg("Scheduling.Database", "scheduling-database"),
          pkg("Scheduling.Kubernetes", "scheduling-kubernetes"),
          pkg("Workflow.Abstractions", "workflow-abstractions"),
          pkg("Workflow.InMemory", "workflow-inmemory"),
          pkg("Workflow.Database", "workflow-database"),
        ],
      },
      {
        text: "本地化 · 存储 · 配置",
        collapsed: true,
        items: [
          pkg("Localization.Abstractions", "localization-abstractions"),
          pkg("Localization.InMemory", "localization-inmemory"),
          pkg("Localization.Database", "localization-database"),
          pkg("ObjectStorage.Abstractions", "objectstorage-abstractions"),
          pkg("ObjectStorage.FileSystem", "objectstorage-filesystem"),
          pkg("ObjectStorage.S3", "objectstorage-s3"),
          pkg("Configuration.Abstractions", "configuration-abstractions"),
          pkg("Configuration.Nacos", "configuration-nacos"),
        ],
      },
      {
        text: "审计 · 可观测性 · AI",
        collapsed: true,
        items: [
          pkg("Auditing.Abstractions", "auditing-abstractions"),
          pkg("Auditing.InMemory", "auditing-inmemory"),
          pkg("Auditing.Database", "auditing-database"),
          pkg("Observability.Abstractions", "observability-abstractions"),
          pkg("Observability.Host", "observability-host"),
          pkg("Observability.OpenTelemetry", "observability-opentelemetry"),
          pkg("AI.Abstractions", "ai-abstractions"),
          pkg("AI.OpenAI", "ai-openai"),
          pkg("AI.VectorData", "ai-vectordata"),
        ],
      },
      {
        text: "宿主与协议",
        collapsed: true,
        items: [
          pkg("Hosting.AspNetCore", "hosting-aspnetcore"),
          pkg("Hosting.CommandLine", "hosting-commandline"),
          pkg("Protocols.Http", "protocols-http"),
          pkg("Protocols.Grpc", "protocols-grpc"),
          pkg("Protocols.Mcp", "protocols-mcp"),
          pkg("Protocols.CloudEvents", "protocols-cloudevents"),
        ],
      },
      {
        text: "插件",
        collapsed: true,
        items: [
          pkg("Plugins.Protocol", "plugins-protocol"),
          pkg("Plugins.InProcess", "plugins-inprocess"),
          pkg("Plugins.OutOfProcess", "plugins-outofprocess"),
          pkg("Plugins.Wasm", "plugins-wasm"),
        ],
      },
      {
        text: "组合包与工具链",
        collapsed: true,
        items: [
          pkg("Profiles.Minimal", "profiles-minimal"),
          pkg("Profiles.Standard", "profiles-standard"),
          pkg("Profiles.Full", "profiles-full"),
          pkg("Cli 命令行工具", "cli"),
          pkg("Testing 测试基座", "testing"),
          pkg("Conformance 一致性套件", "conformance"),
        ],
      },
    ],
  },
];

// 每个顶部导航板块各自一份侧栏，由路径前缀决定用哪一份；
// 首页是 layout: home，不落任何一份。
const sidebar: DefaultTheme.Sidebar = {
  "/guide/": guideSidebar,
  "/packages/": packagesSidebar,
  "/ai/": startSidebar,
  "/": startSidebar,
};

const nav: DefaultTheme.NavItem[] = [
  {
    text: "指南",
    activeMatch: "^/(introduction|why|overview|quickstart|(guide|ai)/.*)$",
    items: [
      {
        text: "快速开始",
        items: [
          { text: "介绍", link: "/introduction" },
          { text: "快速上手", link: "/quickstart" },
          { text: "常见问题", link: "/guide/faq" },
        ],
      },
      {
        text: "深入",
        items: [
          { text: "开发指南", link: "/guide/modularity" },
          { text: "从 4.x 迁移", link: "/guide/upgrade" },
        ],
      },
      {
        text: "AI 辅助开发",
        items: [
          { text: "For Agents", link: "/ai/agents" },
          { text: "MCP Server", link: "/ai/mcp" },
          { text: "CLI", link: "/ai/cli" },
        ],
      },
    ],
  },
  { text: "包", link: "/packages/", activeMatch: "/packages/" },
  {
    text: "生态",
    items: [
      {
        text: "官方生态",
        items: [
          { text: "开发框架", link: "/" },
          { text: "视图组件", link: "https://ui.docs.xihanfun.com" },
          { text: "基础应用", link: "https://basicapp.docs.xihanfun.com" },
        ],
      },
      {
        text: "规范与 SDK",
        items: [
          { text: "XiHan Spec", link: "https://github.com/XiHanFun/framework-spec" },
          { text: "Go SDK", link: "https://github.com/XiHanFun/framework-sdk-go" },
        ],
      },
      {
        text: "设计器",
        items: [
          { text: "打印设计器", link: "https://print.docs.xihanfun.com" },
          { text: "流程设计器", link: "https://flow.docs.xihanfun.com" },
          { text: "表单设计器", link: "https://form.docs.xihanfun.com" },
          { text: "报表设计器", link: "https://report.docs.xihanfun.com" },
        ],
      },
    ],
  },
  {
    text: "支持",
    items: [
      { text: "公约", link: "https://docs.xihanfun.com/cosmos/code-of-conduct" },
      { text: "参与", link: "https://docs.xihanfun.com/cosmos/contributing" },
      { text: "赞助", link: "https://docs.xihanfun.com/cosmos/sponsor" },
    ],
  },
  {
    text: `v${version}`,
    items: [
      { text: "更新日志", link: "/changelog" },
      { text: "4.x 文档", link: "https://framework.docs.xihanfun.com" },
    ],
  },
];

export default defineXiHanConfig({
  title,
  description,
  keywords,
  repo: "XiHan.Framework",
  banner: {
    id: "2026-10-framework-5-preview",
    text: "XiHan Framework 5 处于预览阶段，尚未发布到 nuget.org",
    link: "/quickstart#获取预览包",
    linkText: "查看获取方式",
  },
  llms: {
    title: "曦寒开发框架 5",
    summary:
      "XiHan Framework 5 是一套语言中立的应用框架规范（XiHan Spec）及其 .NET 10 参考实现：原语层与微内核之上，子系统以抽象包、零外部依赖的默认实现与可替换的 Provider 组成；特性在清单里按能力 ID 声明依赖，内核求解能力图谱后显式装配；每次调用经过固定十一个阶段的管道；一份操作契约同时经 HTTP、gRPC、MCP 与命令行暴露。5.0 处于预览阶段，尚未发布到 nuget.org。",
    sections: [
      { dir: ".", label: "开始" },
      { dir: "ai", label: "AI 辅助开发" },
      { dir: "guide", label: "开发指南" },
      { dir: "packages", label: "包总览" },
    ],
    bundles: [
      { name: "guide", label: "开发指南", dirs: ["guide"] },
      { name: "packages", label: "包总览", dirs: ["packages"] },
    ],
  },
  themeConfig: {
    nav,
    sidebar,
    // 5.0 的文档在 next 分支的 docs-next/ 下，主题按 main 分支 docs/ 推出的编辑链接不适用
    editLink: {
      text: "在 GitHub 上编辑此页",
      pattern: "https://github.com/XiHanFun/XiHan.Framework/tree/next/docs-next/:path",
    },
  },
});
