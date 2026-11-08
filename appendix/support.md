来源：https://framework.docs.xihanfun.com/appendix/support

# 支持曦寒

XiHan Framework 与整个曦寒懿生态自发布以来一直采用 MIT 协议：核心能力永久免费，个人与企业都可以使用和商用，开源版本完整可用、不做削弱，已经以 MIT 公开的能力不会再改成收费。这一页说明你可以怎样支持这个项目，以及遇到问题时去哪里。

## 一个 Star

最简单的支持。仓库在三处托管，Issue 与 Pull Request 在哪一处提都收得到：

| 平台 | 地址 |
| --- | --- |
| GitHub（主库） | [XiHanFun/XiHan.Framework](https://github.com/XiHanFun/XiHan.Framework) |
| Gitee（同步备库） | [XiHanFun/XiHan.Framework](https://gitee.com/XiHanFun/XiHan.Framework) |
| GitCode（同步备库） | [XiHanFun/XiHan.Framework](https://gitcode.com/XiHanFun/XiHan.Framework) |

## 反馈问题

- 缺陷与功能建议：[GitHub Issues](https://github.com/XiHanFun/XiHan.Framework/issues)。提缺陷时附上框架版本、复现步骤、启动日志里打印的能力图谱与管道，以及 Problem Details 里的错误码与 `traceId`。
- 规范问题（原语语义、清单 Schema、协议映射、插件协议）：提到 [framework-spec](https://github.com/XiHanFun/framework-spec)。
- 安全漏洞：不要公开提 Issue，发邮件到 me@xihanfun.com。
- 交流群：QQ 群 462371834。

## 参与贡献

贡献代码、文档、示例与一致性向量都欢迎。流程与规范见[贡献指南](/appendix/contributing)，组织层面的公约见[贡献者公约](https://docs.xihanfun.com/cosmos/code-of-conduct)。

几类特别欢迎的贡献：

| 方向 | 说明 |
| --- | --- |
| Provider | 为某个子系统写一个新的实现并通过一致性套件，见[写一个 Provider](/extending/provider) |
| 语言 SDK | 按 XiHan Spec 实现另一种语言的原语、清单、上下文与插件运行时，见[规范与多语言](/appendix/spec) |
| 文档 | 教程里讲不清的地方、示例跑不通的地方，直接改这一站的 Markdown |
| 一致性向量 | 发现实现之间行为不一致时，把它写成向量提到规范仓库 |

## 赞助

开发框架、视图组件、基础应用、四款设计器和相关文档站都由作者和贡献者长期维护。赞助用于在线预览环境、云服务器、数据库与对象存储、域名与证书、AI 服务与第三方平台费用，以及文档和功能的持续迭代。

赞助方式、等级与赞助榜单见[支持与赞助](https://docs.xihanfun.com/cosmos/sponsor)。按月赞助的企业或团队会在全部文档站的赞助位展示；个人支持者会进入赞助榜单。赞助后请来信告知要展示的名称、留言和链接。

## 生态里的其他项目

| 项目 | 说明 | 文档 |
| --- | --- | --- |
| XiHan.UI | 框架无关的 headless 视图组件库，生态的前端基座 | [ui.docs.xihanfun.com](https://ui.docs.xihanfun.com) |
| XiHan.BasicApp | 基于本框架与 XiHan.UI 的通用管理内核 | [basicapp.docs.xihanfun.com](https://basicapp.docs.xihanfun.com) |
| 打印、流程、表单、报表设计器 | 四款可嵌入的设计器 | [组织门户](https://docs.xihanfun.com) |
| framework-spec | XiHan Spec：RFC、术语表、一致性向量与黑盒运行器 | [GitHub](https://github.com/XiHanFun/framework-spec) |
| framework-sdk-go | Go SDK，用于编写进程外插件与远程 Provider | [GitHub](https://github.com/XiHanFun/framework-sdk-go) |

## 下一步

- [更新日志](/changelog)：各版本的使用者可感知变更
- [发展大事记](/appendix/milestones)：项目是怎么走到今天的
