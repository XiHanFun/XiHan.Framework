import { createRequire } from "node:module";
import type { DefaultTheme } from "vitepress";
import { defineXiHanConfig } from "@xihanfun/vitepress-theme/config";

// 导航末项显示的版本号取自本站 package.json，发版时只改那一处
const { version } = createRequire(import.meta.url)("../package.json");

const title: string = "曦寒开发框架文档";
const description: string = "语言中立的应用框架规范及其 .NET 参考实现";
const keywords: string =
  "曦寒,曦寒懿,开发框架,DotNet,微内核,能力图谱,操作契约,官方文档,开源,XiHanFun,XiHan.Framework";

/** 一章：一个目录，侧栏里是一个可折叠分组 */
interface Chapter {
  /** 章名 */
  text: string;
  /** 目录名，页面都在 docs/<dir>/ 下 */
  dir: string;
  /** 本章的页：[标题, 文件名]，按阅读顺序排列 */
  pages: [text: string, name: string][];
}

// 教程目录：每章都是一个分组，不放单篇文章；页面 H1 与条目标题一致，不带编号
const chapters: Chapter[] = [
  {
    text: "附录",
    dir: "appendix",
    pages: [
      ["介绍", "introduction"],
      ["示例说明", "samples"],
      ["支持曦寒", "support"],
      ["发展大事记", "milestones"],
      ["路线图", "roadmap"],
      ["框架定位", "positioning"],
      ["为什么是曦寒", "why"],
      ["设计哲学", "philosophy"],
      ["规范与多语言", "spec"],
      ["常见问题", "faq"],
      ["贡献指南", "contributing"],
    ],
  },
  {
    text: "入门指南",
    dir: "getting-started",
    pages: [
      ["环境与安装", "installation"],
      ["第一个应用", "first-app"],
      ["核心概念", "concepts"],
      ["组合包与选包", "profiles"],
      ["项目结构建议", "project-structure"],
    ],
  },
  {
    text: "应用启动",
    dir: "startup",
    pages: [
      ["装配入口", "bootstrap"],
      ["特性与清单", "features"],
      ["能力图谱与 Provider", "capabilities"],
      ["生命周期与健康", "lifecycle"],
      ["依赖注入", "dependency-injection"],
      ["执行管道", "pipeline"],
      ["调用上下文", "invocation-context"],
    ],
  },
  {
    text: "配置与选项",
    dir: "configuration",
    pages: [
      ["配置模型与选项", "options"],
      ["配置目录与热更新", "config-directory"],
      ["资源绑定", "bindings"],
      ["远程配置与密钥", "remote"],
    ],
  },
  {
    text: "Web 应用开发",
    dir: "web",
    pages: [
      ["ASP.NET Core 宿主", "hosting"],
      ["操作契约", "operations"],
      ["HTTP 路由与载荷", "routing"],
      ["端点里使用框架", "endpoints"],
      ["实时通信", "realtime"],
      ["规范化接口文档（OpenAPI）", "openapi"],
      ["结果与错误处理", "errors"],
      ["数据校验", "validation"],
    ],
  },
  {
    text: "数据库操作指南",
    dir: "persistence",
    pages: [
      ["领域模型", "domain"],
      ["持久化与 Provider", "overview"],
      ["仓储读写", "repositories"],
      ["工作单元与事务", "unit-of-work"],
      ["EF Core Provider", "entity-framework-core"],
      ["SqlSugar Provider", "sqlsugar"],
      ["建表约定与索引", "schema"],
      ["连接与运行时数据源", "connections"],
      ["标识生成", "identifiers"],
    ],
  },
  {
    text: "SaaS 多租户",
    dir: "multi-tenancy",
    pages: [
      ["租户解析与隔离", "resolution"],
      ["租户分片与分库", "sharding"],
      ["数据库租户目录", "database"],
    ],
  },
  {
    text: "安全鉴权",
    dir: "security",
    pages: [
      ["认证", "authentication"],
      ["授权与权限", "authorization"],
      ["RBAC 授予与条件", "grants"],
      ["IP 归属地", "ip-region"],
      ["出站地址策略", "outbound"],
    ],
  },
  {
    text: "分布式缓存",
    dir: "caching",
    pages: [
      ["缓存读写", "basics"],
      ["分布式锁与幂等", "locks"],
      ["两级缓存与键管理", "hybrid"],
    ],
  },
  {
    text: "消息与事件",
    dir: "messaging",
    pages: [
      ["发布与订阅", "pubsub"],
      ["领域事件与发件箱", "domain-events"],
      ["队列与流", "streams"],
    ],
  },
  {
    text: "多协议暴露",
    dir: "protocols",
    pages: [
      ["gRPC", "grpc"],
      ["MCP", "mcp"],
      ["CloudEvents", "cloudevents"],
      ["命令行宿主", "command-line"],
    ],
  },
  {
    text: "定时任务",
    dir: "scheduling",
    pages: [
      ["作业与计划", "jobs"],
      ["集群与管理", "cluster"],
    ],
  },
  {
    text: "工作流",
    dir: "workflow",
    pages: [
      ["定义与发布", "definitions"],
      ["实例与用户任务", "instances"],
      ["活动与扩展", "activities"],
    ],
  },
  {
    text: "对象存储",
    dir: "object-storage",
    pages: [
      ["读写与列举", "basics"],
      ["分片、预签名与命名实例", "advanced"],
    ],
  },
  {
    text: "通用能力",
    dir: "common",
    pages: [
      ["全球化和本地化", "localization"],
      ["审计", "auditing"],
      ["日志与可观测性", "observability"],
      ["弹性与限流", "resilience"],
      ["JSON 序列化与 Native AOT", "serialization"],
    ],
  },
  {
    text: "AI",
    dir: "ai",
    pages: [
      ["模型与对话", "chat"],
      ["工具与知识库", "tools"],
    ],
  },
  {
    text: "插件",
    dir: "plugins",
    pages: [
      ["隔离级别与清单", "overview"],
      ["进程外插件", "out-of-process"],
      ["Wasm 插件", "wasm"],
    ],
  },
  {
    text: "托管、部署、发布",
    dir: "deployment",
    pages: [
      ["应用清单与 xihan pack", "pack"],
      ["Kubernetes", "kubernetes"],
    ],
  },
  {
    text: "测试指南",
    dir: "testing",
    pages: [
      ["单元与集成测试", "basics"],
      ["一致性套件与门禁", "conformance"],
    ],
  },
  {
    text: "扩展与二次开发",
    dir: "extending",
    pages: [
      ["写一个 Provider", "provider"],
      ["能力包与中间件", "feature"],
    ],
  },
  {
    text: "AI 辅助开发",
    dir: "agents",
    pages: [
      ["For Agents", "agents"],
      ["MCP Server", "mcp"],
      ["xihan 工具", "cli"],
      ["LLMs.txt", "llms"],
    ],
  },
];

// 生成章节的侧栏分组：标题即章名，条目即页名，都不带编号
function chapterItem(chapter: Chapter): DefaultTheme.SidebarItem {
  return {
    text: chapter.text,
    collapsed: true,
    items: chapter.pages.map(([text, name]) => ({ text, link: `/${chapter.dir}/${name}` })),
  };
}

// 生成单个包参考条目
function pkg(text: string, name: string): DefaultTheme.SidebarItem {
  return { text, link: `/packages/${name}` };
}

// 包参考按领域分组：每组先列抽象包，再列默认实现与 Provider；环层写在各包页的信息表里
const packagesGroup: DefaultTheme.SidebarItem = {
  text: "包参考",
  link: "/packages/",
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
};

// 侧栏按路径分开：/packages/ 下只显示包参考（顶部导航「包」进入），其余页面显示教程章节；
// VitePress 按路径深度优先匹配，/packages/ 先于 / 生效；首页是 layout: home，不落侧栏
const sidebar: DefaultTheme.Sidebar = {
  "/packages/": [packagesGroup],
  "/": chapters.map(chapterItem),
};

const nav: DefaultTheme.NavItem[] = [
  {
    text: "指南",
    activeMatch: "^/(?!packages/|changelog)",
    items: [
      {
        text: "开始",
        items: [
          { text: "介绍", link: "/appendix/introduction" },
          { text: "环境与安装", link: "/getting-started/installation" },
          { text: "第一个应用", link: "/getting-started/first-app" },
          { text: "核心概念", link: "/getting-started/concepts" },
        ],
      },
      {
        text: "深入",
        items: [
          { text: "应用启动", link: "/startup/bootstrap" },
          { text: "Web 应用开发", link: "/web/hosting" },
          { text: "数据库操作指南", link: "/persistence/domain" },
          { text: "安全鉴权", link: "/security/authentication" },
          { text: "插件", link: "/plugins/overview" },
          { text: "常见问题", link: "/appendix/faq" },
        ],
      },
      {
        text: "AI 辅助开发",
        items: [
          { text: "For Agents", link: "/agents/agents" },
          { text: "MCP Server", link: "/agents/mcp" },
          { text: "xihan 工具", link: "/agents/cli" },
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
      { text: "NuGet", link: "https://www.nuget.org/profiles/XiHanFun" },
    ],
  },
];

// llms.txt 的栏目按章节顺序登记，栏目名与侧栏分组一致；根目录只有首页与更新日志
const llmsSections = [
  { dir: ".", label: "首页与更新日志" },
  ...chapters.map((chapter) => ({ dir: chapter.dir, label: chapter.text })),
  { dir: "packages", label: "包参考" },
];

export default defineXiHanConfig({
  title,
  description,
  keywords,
  repo: "XiHan.Framework",
  banner: {
    id: "2026-10-framework-preview",
    text: "XiHan Framework 处于预览阶段",
    link: "/getting-started/installation#框架处于预览阶段",
    linkText: "查看获取方式",
  },
  llms: {
    title: "曦寒开发框架",
    summary:
      "XiHan Framework 是一套语言中立的应用框架规范（XiHan Spec）及其 .NET 10 参考实现：原语层与微内核之上，子系统以抽象包、零外部依赖的默认实现与可替换的 Provider 组成；特性在清单里按能力 ID 声明依赖，内核求解能力图谱后显式装配；每次调用经过固定十一个阶段的管道；一份操作契约同时经 HTTP、gRPC、MCP 与命令行暴露。",
    sections: llmsSections,
    bundles: [
      { name: "guide", label: "教程", dirs: chapters.map((chapter) => chapter.dir) },
      { name: "packages", label: "包参考", dirs: ["packages"] },
    ],
  },
  themeConfig: {
    nav,
    sidebar,
    // 文档在 dev 分支的 docs/ 下，主题按 main 分支 docs/ 推出的编辑链接不适用
    editLink: {
      text: "在 GitHub 上编辑此页",
      pattern: "https://github.com/XiHanFun/XiHan.Framework/tree/dev/docs/:path",
    },
  },
});
