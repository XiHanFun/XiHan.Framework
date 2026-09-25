# XiHan.Framework.Web.Mcp

## 概述
XiHan.Framework.Web.Mcp 把 AI 技能以 MCP（Model Context Protocol）Server 的形式经 HTTP 传输对外暴露，供外部 MCP 客户端调用。

端点一旦开出去，持有那把 key 的人默认可调用宿主注册的全部技能。要把暴露面收窄到其中一部分，用 `AllowedTools` 与 `DeniedTools`，见下文「[收窄暴露面](#收窄暴露面允许清单与拒绝清单)」。

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

### 收窄暴露面：允许清单与拒绝清单

两个清单装的都是**工具名**——技能投影出的 MCP tool 名，也就是客户端在 `tools/list` 里看到的那个：

| 允许清单 | 拒绝清单 | 结果 |
| --- | --- | --- |
| 空 | 空 | 暴露全部工具（默认；既有宿主升级到本版本后暴露面不变） |
| 非空 | 空 | 只暴露允许清单里列出的工具 |
| 空 | 非空 | 暴露除拒绝清单以外的全部工具 |
| 非空 | 非空 | 先按允许清单收窄，再减去拒绝清单；同一个名字两边都写了，**以拒绝为准** |

- 名字按序号（ordinal）比较，**区分大小写**，与 MCP 工具集自身按名索引的方式一致。大小写写错的名字匹配不上任何工具：写进拒绝清单拦不住它，写进允许清单也放行不了它。配完请照着 `tools/list` 的实际输出核一遍。
- 被裁掉的工具既不出现在 `tools/list`，也不能经 `tools/call` 调用。
- 裁剪的对象是 `McpServerOptions.ToolCollection`。宿主若另行设置 `Handlers.ListToolsHandler` / `CallToolHandler`，这两个 handler 提供的工具不在裁剪范围内；且 `CallToolHandler` 是「`ToolCollection` 里找不到才调用」的回退，被拒绝的名字有可能落到它身上。启用清单的宿主不应同时使用这两个 handler。
- 清单管的只是**这个 HTTP 端点**的暴露面：它是与 `ApiKey` 同层的部署级对外策略，不是技能开关，被裁掉的技能在宿主进程内照常被模型经 `AIFunction` 自动调用。

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

### 工具重名：启动即失败

两个技能投影出同一个工具名时宿主起不来：`SkillMcpToolsConfigurator` 抛 `InvalidOperationException`，异常里点明工具名与冲突双方；`MapXiHanMcp` 在映射端点前先把工具集装配出来，让这个失败落在启动期而不是第一个 MCP 请求上。

同名工具里只有一个能被列出与调用，另一个会无声消失，而清单是按工具名放行的，一条清单项会指向两个不同的能力。技能名相同的两个技能进不了同一张注册表（`DefaultAiSkillRegistry` 按名索引、同名覆盖），所以撞名通常来自 `AsFunction()` 里取了同一个工具名。

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
