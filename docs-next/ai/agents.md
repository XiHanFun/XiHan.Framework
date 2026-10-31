# For Agents

本页给 Claude Code、Cursor、Codex、GitHub Copilot 这类 AI 编程工具使用：一段可以直接放进项目说明文件的提示词，以及 5.0 为 AI 工具准备的资料从哪里取。

::: tip 和指南里的「AI」章不是一回事
本册讲的是用 AI 工具写基于 XiHan Framework 5 的代码。要在应用里接入大模型、把操作暴露成模型工具，见开发指南的 [AI](../guide/ai) 与 [gRPC、MCP 与 CloudEvents](../guide/protocols)。
:::

## 复制这段提示词

放进项目的 `AGENTS.md`、`CLAUDE.md`、`.cursor/rules` 或 `.github/copilot-instructions.md`：

```text
这个项目的后端用 XiHan Framework 5（5.0.0-preview.1，.NET 10）。写代码前读文档站的 llms.txt，按需取其中列出的页面（页面地址后加 .md 得到 Markdown）。

5.0 与 4.x 是两套不兼容的 API，不要写 4.x 的 XiHanModule、[DependsOn]、[DynamicApi]、ApplicationServiceBase、[UnitOfWork]、ICurrentUser、Castle 拦截器。5.0 的写法：
- 装配：特性实现 IFeature，带 Manifest（按能力 ID 声明 Requires），在 Program.cs 用 builder.AddXiHan(x => x.UseFeature<T>()) 显式加入；服务在 Configure 里显式注册。
- 接口：一个操作一个类，[Operation("<资源复数>.<动词>")] + IOperation<TRequest, TResponse>，请求与响应登记到 JsonSerializerContext，特性里调用 context.AddGeneratedOperations()。
- 失败：返回 Result / Error，不抛异常；错误码 <域>_<KIND>_<NNN>，Error 的第二个参数是消息键常量，文案写在 Localization/<区域>.json（10 种区域各一个文件）。
- 横切逻辑：写 IPipelineMiddleware，按 PipelineStage 登记；跨包依赖用 After/Before 声明次序。

类型、成员与命名空间以项目引用的框架源码或包的 PublicAPI.Shipped.txt / PublicAPI.Unshipped.txt 为准，不确定时先查，不要凭记忆补全。
改完后运行应用的 graph、pipeline、operations 命令（或 xihan graph --project <项目>）核对能力图谱、管道顺序与操作登记。
```

文档站地址以部署为准；在 XiHan.Framework 仓库内工作时，直接读 `docs-next/` 下的 Markdown 源文件，内容与站点相同。

## AI 工具能拿到什么

| 资料 | 内容 | 位置 |
| --- | --- | --- |
| LLMs.txt | 全站索引、全文与分册的纯文本版本 | 见 [LLMs.txt](./llms) |
| 单页 Markdown | 任一页面地址后加 `.md` | 每页正文上方的「取本页 Markdown」 |
| 公开 API 清单 | 每个包的 `PublicAPI.Shipped.txt` 与 `PublicAPI.Unshipped.txt`，逐行列出公开类型与成员 | 框架源码 `next/src/<包 ID>/` |
| 规范 | RFC、术语表、能力 ID、错误码、Baggage 键、操作 ID 文法 | [XiHan Spec](https://github.com/XiHanFun/framework-spec) |
| 应用自省命令 | `graph`、`pipeline`、`operations`、`invoke` | 应用的命令行宿主，或 [`xihan` 工具](./cli)转发 |
| 应用的 MCP 端点 | 把应用自己的操作作为 MCP tool 交给 AI 工具调用 | 见 [MCP Server](./mcp) |

5.0 目前没有离线知识库与文档 MCP 服务端，4.x 的 `XiHan.Framework.Cli` 知识库命令不适用于 5.0。

## 给 AI 工具的检查清单

AI 工具写完代码后，按这几项自查最容易出错的地方：

1. 每个新特性都在 `AddXiHan` 里 `UseFeature` 了，清单 ID 唯一，`Requires` 用 `<领域>Capabilities` 常量。
2. 每个操作的请求与响应类型都登记到了 `JsonSerializerContext`（否则编译报 `XHG002`）。
3. 操作的 `Permission` 引用的权限码在特性里用 `AddPermission` 登记过。
4. 新增的消息键在 10 个语言文件里各有一行，占位符与 `en-US.json` 一致。
5. 管道外使用仓储时自己开启了工作单元。
6. 没有引入反射、`dynamic` 或运行期程序集扫描。

## 相关

- [LLMs.txt](./llms) · [MCP Server](./mcp) · [CLI](./cli)
- [快速上手](../quickstart)
