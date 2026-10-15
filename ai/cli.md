来源：https://framework.docs.xihanfun.com/ai/cli

# CLI

`XiHan.Framework.Cli` 是 XiHan.Framework 的命令行工具：在终端里查包、模块类与依赖、配置节、文档与更新日志，也是 [MCP 服务端](./mcp) 的载体。命令名 `xihan-framework`。

## 特点

- **离线**：整站文档、各包的项目文件、模块类与选项类在打包时嵌进工具，查询不联网。
- **与版本一致**：工具与框架各包同号发布。在项目里运行时，工具扫描项目文件与 props 里的 `XiHan.Framework.*` 包引用，版本与自身不一致时在 stderr 提示改用同号的工具。
- **配置键从源码读**：配置节、属性、类型与缺省值取自选项类的 `SectionName` 常量与属性声明，说明取自 XML 文档注释，不靠手抄。
- **给机器读**：所有查询命令都可输出 JSON；查不到时给出候选。

## 安装

不安装，用 .NET 10 SDK 自带的 `dnx` 按版本直接运行：

```bash
dnx XiHan.Framework.Cli@<版本> --yes -- info Caching
```

装成全局工具：

```bash
dotnet tool install --global XiHan.Framework.Cli --version <版本>
xihan-framework info Caching
```

或者装成仓库的本地工具，版本记在 `.config/dotnet-tools.json` 里随项目一起升级：

```bash
dotnet new tool-manifest
dotnet tool install XiHan.Framework.Cli --version <版本>
dotnet xihan-framework info Caching
```

需要 .NET 10 SDK。

## 快速开始

```bash
xihan-framework list --layer Web                # Web 层的包
xihan-framework info XiHanWebApiModule          # 按模块类名查包
xihan-framework config Authentication           # 认证包的全部配置节
xihan-framework config XiHan:Authentication:Jwt # 一个配置节的属性与缺省值
xihan-framework search 工作单元 事务            # 同时提到两个词的页面
xihan-framework doc guide/uow                   # 指南全文
```

## 命令

### 查询

| 命令 | 参数 | 说明 |
| --- | --- | --- |
| `list` | `--layer <层>` `--query <关键字>` | 按所在层列出包与模块类 |
| `info` | `<包\|模块>` | 安装命令、模块类与 `[DependsOn]`、引用的框架包与第三方包、绑定的配置节 |
| `doc` | `[<页面\|包>]` | 文档页全文；写包名时取它的参考页；不带参数列出全部页面 |
| `config` | `[<配置节\|包\|选项类>]` | 不带参数列出全部配置节；带参数时逐项列出属性、类型、缺省值与说明，并给出按缺省值写出的 appsettings 片段 |
| `search` | `<关键字…>` | 在全部文档里检索，多个词需同时出现，按相关度排序 |
| `changelog` | `[<版本> [<版本>]]` `--keyword <词>` | 不带版本取最近一版；一个版本取这一版；两个版本取两者之间（不含较早的那一版）；`--keyword` 只留提到它的条目，可写包名 |

### 集成

| 命令 | 参数 | 说明 |
| --- | --- | --- |
| `mcp` | — | 以 stdio 启动 MCP 服务端，见 [MCP Server](./mcp) |
| `setup` | `--client <claude\|cursor\|vscode\|codex>` `--mode <mcp\|skill\|both>` `--pin <版本>` `--dry-run` `--check` | 把钉住版本的 MCP 配置与使用技能写进项目 |

### 包名

包名可以写全名（`XiHan.Framework.Caching`）、短名（`Caching`、`Web.Api`、`web-api`）或模块类名（`XiHanCachingModule`）。文档页写站内路径，如 `guide/caching`、`quickstart`、`packages/web-api`。

## 全局选项

| 选项 | 说明 | 缺省 |
| --- | --- | --- |
| `--format <markdown\|json>` | 输出格式 | `markdown` |
| `-v, --version` | 打印版本 | — |
| `-h, --help` | 打印帮助 | — |

## 退出码

| 退出码 | 含义 |
| --- | --- |
| `0` | 成功 |
| `1` | 查不到（stderr 给出候选）或配置核对未通过 |
| `2` | 用法错误：未知命令、缺参数、选项取值不合法 |

## 相关

- [For Agents](./agents) · [MCP Server](./mcp) · [LLMs.txt](./llms)
