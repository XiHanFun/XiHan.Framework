import { DefaultTheme, HeadConfig, defineConfig } from "vitepress";
import { release, withNavBadge } from "./versions";

const title: string = "曦寒开发框架文档";
const description: string = "基于 DotNet 的模块化开发框架";
const keywords: string =
  "曦寒,曦寒懿,开发框架,DotNet,模块化,官方文档,开源,XiHanFun,XiHan.Framework";
const logo: string = "/images/logo.png";
const head: HeadConfig[] = [
  ["meta", { name: "author", content: "XiHanFun" }],
  [
    "meta",
    {
      name: "keywords",
      content: keywords,
    },
  ],
  ["link", { rel: "icon", href: "/favicon.ico" }],
];

// 生成单个模块包文档条目
function pkg(text: string, name: string): DefaultTheme.SidebarItem {
  return { text, link: `/packages/${name}` };
}

// 开发指南：按能力域编号；内容页在 guide/ 下
function chapter(
  index: number,
  text: string,
  link: string
): DefaultTheme.SidebarItem {
  return { text: `${index}. ${text}`, link: `/${link}` };
}

// 每章一页，全部落在 guide/ 下；包文档是另一册（参考手册），章末链接过去
const guideChapters: [text: string, name: string][] = [
  ["模块系统", "modularity"],
  ["模块生命周期", "lifecycle"],
  ["依赖注入", "dependency-injection"],
  ["AOP 与拦截器", "aop"],
  ["配置与选项", "configuration"],
  ["Web 应用开发", "web"],
  ["动态 API", "dynamic-api"],
  ["统一响应与异常", "response"],
  ["数据校验", "validation"],
  ["数据访问", "data"],
  ["工作单元与事务", "uow"],
  ["多租户", "multi-tenancy"],
  ["认证", "authentication"],
  ["授权", "authorization"],
  ["数据加解密", "security"],
  ["缓存与分布式锁", "caching"],
  ["事件总线", "event-bus"],
  ["定时任务与后台作业", "tasks"],
  ["工作流", "workflow"],
  ["消息通知", "messaging"],
  ["机器人", "bot"],
  ["实时通信", "realtime"],
  ["对象存储与虚拟文件", "storage"],
  ["国际化", "localization"],
  ["模板引擎", "templating"],
  ["日志", "logging"],
  ["审计", "auditing"],
  ["可观测性", "observability"],
  ["HTTP 远程请求", "http"],
  ["对象映射与序列化", "mapping"],
  ["分布式 ID", "distributed-ids"],
  ["搜索引擎", "search"],
  ["AI 与 MCP", "ai"],
  ["网关与流量治理", "gateway"],
  ["脚本引擎", "script"],
  ["升级与迁移", "upgrade"],
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
  { text: "更新日志", link: "/changelog" },
];

const guideSidebar: DefaultTheme.SidebarItem[] = [
  {
    text: "开发指南",
    collapsed: false,
    items: guideChapters.map(([text, name], i) =>
      chapter(i + 1, text, `guide/${name}`)
    ),
  },
];

const packagesSidebar: DefaultTheme.SidebarItem[] = [
  {
    text: "模块总览",
    link: "/packages/",
    collapsed: false,
    items: [
      {
        text: "公共与核心",
        collapsed: true,
        items: [
          pkg("Utils 通用工具", "utils"),
          pkg("Metadata 元数据", "metadata"),
          pkg("Core 模块化核心", "core"),
          pkg("Analyzers 分析器", "analyzers"),
        ],
      },
      {
        text: "领域与应用",
        collapsed: true,
        items: [
          pkg("Domain.Shared", "domain-shared"),
          pkg("Domain 领域层", "domain"),
          pkg("Application.Contracts", "application-contracts"),
          pkg("Application 应用层", "application"),
        ],
      },
      {
        text: "数据与持久化",
        collapsed: true,
        items: [
          pkg("Data 数据访问", "data"),
          pkg("Uow 工作单元", "uow"),
          pkg("Caching 缓存", "caching"),
        ],
      },
      {
        text: "安全 · 认证 · 授权",
        collapsed: true,
        items: [
          pkg("Security 安全加密", "security"),
          pkg("Authentication 认证", "authentication"),
          pkg("Authorization 授权", "authorization"),
        ],
      },
      {
        text: "多租户 · 配置 · 校验",
        collapsed: true,
        items: [
          pkg("MultiTenancy.Abstractions", "multitenancy-abstractions"),
          pkg("MultiTenancy 多租户", "multitenancy"),
          pkg("Settings 设置", "settings"),
          pkg("Validation.Abstractions", "validation-abstractions"),
          pkg("Validation 校验", "validation"),
        ],
      },
      {
        text: "事件 · 消息 · 通信",
        collapsed: true,
        items: [
          pkg("EventBus.Abstractions", "eventbus-abstractions"),
          pkg("EventBus 事件总线", "eventbus"),
          pkg("EventBus.RabbitMQ", "eventbus-rabbitmq"),
          pkg("EventBus.Kafka", "eventbus-kafka"),
          pkg("EventBus.Redis", "eventbus-redis"),
          pkg("Messaging 消息", "messaging"),
          pkg("Http 客户端", "http"),
        ],
      },
      {
        text: "通用基础设施",
        collapsed: true,
        items: [
          pkg("Serialization 序列化", "serialization"),
          pkg("ObjectMapping 对象映射", "objectmapping"),
          pkg("Localization.Abstractions", "localization-abstractions"),
          pkg("Localization 国际化", "localization"),
          pkg("Logging 日志", "logging"),
          pkg("Auditing 审计日志", "auditing"),
          pkg("Castle AOP", "castle"),
          pkg("Threading 并发", "threading"),
          pkg("Timing 时间", "timing"),
          pkg("DistributedIds 分布式 ID", "distributed-ids"),
        ],
      },
      {
        text: "存储 · 模板 · 任务 · 治理",
        collapsed: true,
        items: [
          pkg("ObjectStorage 对象存储", "object-storage"),
          pkg("VirtualFileSystem 虚拟文件", "virtual-file-system"),
          pkg("Templating 模板", "templating"),
          pkg("Tasks 定时任务", "tasks"),
          pkg("Traffic 流量治理", "traffic"),
          pkg("Upgrade 升级引擎", "upgrade"),
          pkg("Script 脚本引擎", "script"),
          pkg("Workflow.Abstractions", "workflow-abstractions"),
          pkg("Workflow 工作流", "workflow"),
          pkg("SearchEngines.Abstractions", "search-engines-abstractions"),
          pkg("SearchEngines 搜索", "search-engines"),
          pkg("SearchEngines.Elasticsearch", "search-engines-elasticsearch"),
          pkg("Observability 可观测性", "observability"),
          pkg("DevTools 开发工具", "devtools"),
        ],
      },
      {
        text: "AI 与机器人",
        collapsed: true,
        items: [
          pkg("AI.Abstractions", "ai-abstractions"),
          pkg("AI 集成", "ai"),
          pkg("Bot 机器人核心", "bot"),
          pkg("Bot.Email 邮件", "bot-email"),
          pkg("Bot.Sms 短信", "bot-sms"),
          pkg("Bot.Telegram", "bot-telegram"),
          pkg("Bot.DingTalk 钉钉", "bot-dingtalk"),
          pkg("Bot.Lark 飞书", "bot-lark"),
          pkg("Bot.WeCom 企业微信", "bot-wecom"),
        ],
      },
      {
        text: "Web 层",
        collapsed: true,
        items: [
          pkg("Web.Core Web 核心", "web-core"),
          pkg("Web.Api 动态 API", "web-api"),
          pkg("Web.Docs API 文档", "web-docs"),
          pkg("Web.Gateway 网关", "web-gateway"),
          pkg("Web.Grpc gRPC", "web-grpc"),
          pkg("Web.RealTime 实时通信", "web-realtime"),
          pkg("Web.Mcp MCP 服务端", "web-mcp"),
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
  "/": startSidebar,
};

const nav: DefaultTheme.NavItem[] = [
  {
    text: withNavBadge("🧩 开发框架", release),
    link: "/",
    activeMatch: "^/$",
  },
  {
    text: "开始",
    link: "/introduction",
    activeMatch: "^/(introduction|why|overview|quickstart)$",
  },
  { text: "开发指南", link: "/guide/modularity", activeMatch: "/guide/" },
  { text: "模块总览", link: "/packages/", activeMatch: "/packages/" },
  { text: "更新日志", link: "/changelog" },
  {
    text: "探索未知",
    items: [
      {
        text: "关于我们",
        items: [
          {
            text: "官方网站",
            link: "https://www.xihanfun.com",
          },
          {
            text: "组织文档",
            link: "https://docs.xihanfun.com",
          },
        ],
      },
      {
        text: "生态文档",
        items: [
          {
            text: "前端 | 视图组件",
            link: "https://ui.docs.xihanfun.com",
          },
          {
            text: "用例 | 基础应用",
            link: "https://basicapp.docs.xihanfun.com",
          },
        ],
      },
      {
        text: "引用下载",
        items: [
          {
            text: "后端 | nuget",
            link: "https://www.nuget.org/profiles/XiHanFun",
          },
          {
            text: "前端 | npm",
            link: "https://www.npmjs.com/org/xihan-ui",
          },
        ],
      },
      {
        text: "在线体验",
        items: [
          {
            text: "后端 | 开发框架",
            link: "https://framework.xihanfun.com",
          },
          {
            text: "前端 | 视图组件",
            link: "https://ui.xihanfun.com",
          },
          {
            text: "用例 | 基础应用",
            link: "https://basicapp.xihanfun.com",
          },
        ],
      },
    ],
  },
  {
    text: "代码仓库",
    items: [
      {
        text: "Github主库(国际)",
        link: "https://github.com/XiHanFun/XiHan.Framework",
      },
      {
        text: "Gitee同步备库(国内)",
        link: "https://gitee.com/XiHanFun/XiHan.Framework",
      },
      {
        text: "Atomgit同步备库(国内)",
        link: "https://atomgit.com/XiHanFun/XiHan.Framework",
      },
    ],
  },
  {
    text: "参与贡献",
    items: [
      {
        text: "公约",
        link: "https://docs.xihanfun.com/cosmos/code-of-conduct",
      },
      {
        text: "指南",
        link: "https://docs.xihanfun.com/cosmos/contributing",
      },
      {
        text: "贡献者",
        link: "https://docs.xihanfun.com/cosmos/contributors",
      },
      {
        text: "支持&赞助",
        link: "https://docs.xihanfun.com/cosmos/sponsor",
      },
    ],
  },
];

function searchOptions(): Partial<DefaultTheme.AlgoliaSearchOptions> {
  return {
    placeholder: "搜索文档",
    translations: {
      button: {
        buttonText: "搜索文档",
        buttonAriaLabel: "搜索文档",
      },
      modal: {
        searchBox: {
          resetButtonTitle: "清除查询条件",
          resetButtonAriaLabel: "清除查询条件",
          cancelButtonText: "取消",
          cancelButtonAriaLabel: "取消",
        },
        startScreen: {
          recentSearchesTitle: "搜索历史",
          noRecentSearchesText: "没有搜索历史",
          saveRecentSearchButtonTitle: "保存至搜索历史",
          removeRecentSearchButtonTitle: "从搜索历史中移除",
          favoriteSearchesTitle: "收藏",
          removeFavoriteSearchButtonTitle: "从收藏中移除",
        },
        errorScreen: {
          titleText: "无法获取结果",
          helpText: "你可能需要检查你的网络连接",
        },
        footer: {
          selectText: "选择",
          navigateText: "切换",
          closeText: "关闭",
          searchByText: "搜索提供者",
        },
        noResultsScreen: {
          noResultsText: "无法找到相关结果",
          suggestedQueryText: "你可以尝试查询",
          reportMissingResultsText: "你认为该查询应该有结果？",
          reportMissingResultsLinkText: "点击反馈",
        },
      },
    },
  };
}

export default defineConfig({
  lang: "zh-CN",
  title: title,
  description: description,
  head: head,
  lastUpdated: true,
  cleanUrls: true,
  themeConfig: {
    logo: logo,
    socialLinks: [
      { icon: "github", link: "https://github.com/XiHanFun/XiHan.Framework" },
      { icon: "gitee", link: "https://gitee.com/XiHanFun/XiHan.Framework" },
      { icon: "git", link: "https://atomgit.com/XiHanFun/XiHan.Framework" },
    ],
    search: {
      provider: "local",
      options: searchOptions(),
    },
    nav: nav,
    sidebar: sidebar,
    docFooter: {
      prev: "上一页",
      next: "下一页",
    },
    outline: {
      label: "目录",
      level: "deep",
    },
    langMenuLabel: "多语言",
    returnToTopLabel: "回到顶部",
    sidebarMenuLabel: "菜单",
    darkModeSwitchLabel: "主题",
    lightModeSwitchTitle: "切换到浅色模式",
    darkModeSwitchTitle: "切换到深色模式",
    skipToContentLabel: "跳转到内容",
    notFound: {
      title: "页面未找到",
      quote:
        "但如果你不改变方向，并且继续寻找，你可能最终会到达你所前往的地方。",
      linkLabel: "前往首页",
      linkText: "带我回首页",
    },
    editLink: {
      text: "在 GitHub 上编辑此页",
      pattern:
        "https://github.com/XiHanFun/XiHan.Framework/tree/main/docs/:path",
    },
    lastUpdated: {
      text: "最后更新于",
    },
    footer: {
      message:
        "Released under The <a href='https://opensource.org/license/MIT' target='_blank'>MIT</a> License",
      copyright:
        "Copyright ©2021-Present <a href='https://www.xihanfun.com' target='_blank'>XiHanFun</a> and contributors.",
    },
  },
});
