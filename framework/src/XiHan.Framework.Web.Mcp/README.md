# XiHan.Framework.Web.Mcp

## 概述
XiHan.Framework.Web.Mcp 把 AI 技能以 MCP（Model Context Protocol）Server 的形式经 HTTP 传输对外暴露，供外部 MCP 客户端调用。

这里暴露的是**宿主应用自己注册的 `IAiSkill` 实现**，与仓库内 `framework/tool/XiHan.Framework.Docs.Mcp`（本机 stdio、检索本仓库文档的内部工具）是两回事，两者常被混为一谈：本包不提供 `search_docs`／`read_doc`／`list_docs` 这些文档工具。

**默认暴露的是宿主注册的全部技能**：端点一旦开出去，持有那把 key 的人就能调用其中任意一个。要把暴露面收窄到其中一部分，用 `AllowedTools`（只暴露清单内的）与 `DeniedTools`（永远不暴露清单内的），见下文「[收窄暴露面](#收窄暴露面允许清单与拒绝清单)」。

## 核心能力
- MCP Server 的 HTTP 传输装配（可选无状态模式）
- 技能注册表到 MCP tools 的投影（复用 `XiHan.Framework.AI` 的 `AddXiHanMcpServerTools`）
- `/mcp` 端点映射与应用管理 key 鉴权（定长比较，401 拒绝）
- fail-closed：未开启或未配置密钥时既不注册服务也不映射端点
- 工具暴露策略：允许/拒绝清单裁剪对外可见与可调用的工具集（两者都不配时暴露全部）

## 依赖关系
- 通过 `XiHanWebMcpModule` 参与模块化生命周期
- 依赖 `XiHanWebCoreModule` 与 `XiHanAIModule`

## 配置与约定
配置节 `XiHan:AI:Mcp`（`XiHanMcpOptions`）：

| 键 | 默认值 | 说明 |
| --- | --- | --- |
| `Enabled` | `false` | 是否启用 MCP Server |
| `ApiKey` | 空 | 应用管理的访问密钥，空则不暴露端点 |
| `HeaderName` | `X-Api-Key` | 携带密钥的请求头名（也接受 `Authorization: Bearer`） |
| `Path` | `/mcp` | 端点路径 |
| `Stateless` | `true` | 是否无状态 HTTP 传输 |
| `AllowedTools` | 空数组 | 工具名允许清单；空表示不限制，非空则只暴露清单内的工具 |
| `DeniedTools` | 空数组 | 工具名拒绝清单；始终生效，同名同时出现在允许清单时以拒绝为准 |

### appsettings.json 长什么样

配置节名 `XiHan:AI:Mcp` 是冒号分隔的**配置路径**，落到 JSON 里是三层嵌套，写平了绑不上：

```json
{
  "XiHan": {
    "AI": {
      "Mcp": {
        "Enabled": true,
        "ApiKey": "",
        "Path": "/mcp",
        "Stateless": true,
        "AllowedTools": [],
        "DeniedTools": []
      }
    }
  }
}
```

上面这份就是**该进源码管理的样子**：`ApiKey` 留空（或整个键都不写）。此时 `IsExposable` 为 false，端点不会被映射，提交进仓库的配置默认什么都不暴露。

> **`ApiKey` 绝不能写进任何 appsettings 文件。** 它是能调用宿主全部 AI 技能的凭据，落进版本库就等于公开，事后删掉也仍在历史里。密钥只能从环境变量或 Secret Manager 注入。

开发期用 Secret Manager（在宿主项目目录执行）：

```bash
dotnet user-secrets init
dotnet user-secrets set "XiHan:AI:Mcp:ApiKey" "<随机生成的密钥>"
```

部署期用环境变量。ASP.NET Core 把双下划线 `__` 映射成配置路径分隔符 `:`，所以键名是 `XiHan__AI__Mcp__ApiKey`：

```bash
export XiHan__AI__Mcp__Enabled=true
export XiHan__AI__Mcp__ApiKey='<随机生成的密钥>'
```

PowerShell：

```powershell
$env:XiHan__AI__Mcp__Enabled = 'true'
$env:XiHan__AI__Mcp__ApiKey = '<随机生成的密钥>'
```

### fail-closed 门控

`XiHanMcpOptions.IsExposable` 的定义是 `Enabled && ApiKey 非空白`，**服务注册与端点映射共用这一个判定**：

- 判定为 false 时，`AddXiHanWebMcp` 只绑定选项、不注册任何 MCP 服务；`MapXiHanMcp` 直接返回，不映射任何端点
- 于是配错的部署（开了 `Enabled` 却漏了 `ApiKey`）暴露出去的是**什么都没有**，而不是一个无鉴权的 `/mcp`

这是本包最要紧的安全性质：漏配的后果只会是「用不了」，不会是「敞开了」。

### 收窄暴露面：允许清单与拒绝清单

`AllowedTools` 与 `DeniedTools` 装的都是**工具名**——技能投影出的 MCP tool 名，也就是客户端在 `tools/list` 里看到的那个：

| 允许清单 | 拒绝清单 | 结果 |
| --- | --- | --- |
| 空 | 空 | 暴露全部工具（默认；既有宿主升级到本版本后行为不变） |
| 非空 | 空 | 只暴露允许清单里列出的工具 |
| 空 | 非空 | 暴露除拒绝清单以外的全部工具 |
| 非空 | 非空 | 先按允许清单收窄，再减去拒绝清单；同一个名字两边都写了，**以拒绝为准** |

三条要记住的：

- **拒绝胜过允许。** 把名字写进拒绝清单的人期待它彻底消失，不论别处还说了什么。
- **名字区分大小写**，按序号（ordinal）比较，与 MCP 工具集自身按名索引的方式一致。`SendMail` 与 `sendmail` 是两个名字：拒绝清单里大小写写错等于**没写**，那个工具照样暴露；允许清单里大小写写错等于**没列**，那个工具反而消失。配完请照着 `tools/list` 的实际输出核一遍。
- **裁掉就是两条路一起断。** 被裁掉的工具既不出现在 `tools/list`，也不能经 `tools/call` 调用，不是只在列表里藏起来。

配置写法（环境变量按下标给数组元素）：

```json
{
  "XiHan": {
    "AI": {
      "Mcp": {
        "DeniedTools": ["delete_tenant", "reset_password"]
      }
    }
  }
}
```

```bash
export XiHan__AI__Mcp__AllowedTools__0=search_orders
export XiHan__AI__Mcp__AllowedTools__1=query_inventory
```

清单管的只是**这个 HTTP 端点**的暴露面：它是与 `ApiKey` 同层的部署级对外策略，不是技能开关，被裁掉的技能在宿主进程内照常被模型经 `AIFunction` 自动调用。

### 工具重名：启动即失败

两个技能投影出同一个工具名时，宿主**起不来**，异常里点明工具名与冲突双方（`SkillMcpToolsConfigurator` 抛 `InvalidOperationException`，`MapXiHanMcp` 在映射端点前把工具集装配出来，好让它落在启动期而不是第一个 MCP 请求上）。

这是刻意的：同名工具里注定只有一个能被列出与调用，另一个会无声消失。与其让人在「注册过的技能凭空不存在」上排查，不如启动时就说清是谁撞了谁。技能名相同的两个技能进不了同一张注册表（`DefaultAiSkillRegistry` 按名索引、同名覆盖），所以撞名基本来自 `AsFunction()` 里取了同一个工具名。

### 客户端怎么带密钥

端点用 `AllowAnonymous()` 绕开框架的全局鉴权 FallbackPolicy，改由 `McpApiKeyEndpointFilter` 守门（定长比较防时序侧信道，不匹配即 401）。客户端二选一：

- 请求头 `X-Api-Key: <密钥>`（头名由 `HeaderName` 决定，默认即此）
- `Authorization: Bearer <密钥>`

只有 `HeaderName` 指定的那个头缺失或为空时，才回退去看 `Authorization`。

冒烟验证（把地址换成宿主实际监听的地址；MCP 的 Streamable HTTP 传输要求 `Accept` 同时列出 `application/json` 与 `text/event-stream`）：

```bash
# 不带密钥：401
curl -i -X POST http://localhost:5000/mcp \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json, text/event-stream' \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"curl","version":"0"}}}'

# 带密钥：请求进入 MCP 处理器
curl -i -X POST http://localhost:5000/mcp \
  -H "X-Api-Key: <随机生成的密钥>" \
  -H 'Content-Type: application/json' \
  -H 'Accept: application/json, text/event-stream' \
  -d '{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"curl","version":"0"}}}'
```

### Stateless 的取舍

`Stateless` 默认 `true`，含义是 HTTP 传输不保持会话，**没有服务端 → 客户端的回调通道**。

- 检索、查询、计算这类「进去一次、出来一次」的技能，无状态足够，也更好水平扩缩
- 技能若需要 sampling（回头找客户端要一次模型推理）或 elicitation（回头问用户要输入），必须把 `Stateless` 设为 `false`

## 使用方式
```csharp
[DependsOn(typeof(XiHanWebMcpModule))]
public class MyModule : XiHanModule
{
}
```

## 扩展点
- 实现 `IAiSkill` 并注册到技能注册表，即自动成为 MCP tool（受允许/拒绝清单裁剪）
- 自行调用 `MapXiHanMcp` 在指定端点路由构建器上映射

## 目录结构
```text
XiHan.Framework.Web.Mcp/
  README.md
  XiHanWebMcpModule.cs
  Extensions/
    ApplicationBuilderExtensions.cs
    DependencyInjection/
      XiHanWebMcpServiceCollectionExtensions.cs
  Filters/
    McpApiKeyEndpointFilter.cs
    McpToolExposureFilter.cs
  Options/
    XiHanMcpOptions.cs
```
