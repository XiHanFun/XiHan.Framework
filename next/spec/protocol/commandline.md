# 命令行载体

> 规范性。参考实现：`XiHan.Framework.Hosting.CommandLine`。命令行参数翻译为 XGI 调用，一次进程一条命令。

## 上下文映射（RFC-0003）

| 字段 | 选项 | 说明 |
| --- | --- | --- |
| Baggage `xihan.tenant` | `--tenant <id>` | |
| Baggage `xihan.correlation` | `--correlation <id>` | |
| Baggage `xihan.locale` | `--locale <locale>` | 规范化后写入；失败文案据此翻译 |
| Deadline | `--timeout <seconds>` | 正数秒，按宿主时钟换算为绝对截止时间 |
| Credential | `--authorization '<scheme> <value>'` 或环境变量 `XIHAN_AUTHORIZATION` | 原样进入凭据，由 Authenticate 阶段解释；选项优先 |
| Trace | — | 每次调用生成新的 trace-id |

## 操作映射（RFC-0011）

- `invoke <operation-id> [...]` 调用操作；`<resource> <verb> [...]` 等价于 `invoke <resource>.<verb> [...]`（命令名不是已注册命令、且能拼成合法操作 ID 时）。
- 发布范围：`Expose` 含 `CommandLine` 且非 `Stability.Internal` 的操作；其他 → `HOSTING_NOTFOUND_001`，退出码 3。
- 载荷（JSON 对象）：`--json <object>` > `--data <file>`（`@file` 亦可）> 其余 `--<field> <value>` 选项（值为字符串，重复的选项成数组；没有字段时为 `{}`）。保留选项：`json`、`data`、`authorization`、`tenant`、`correlation`、`locale`、`timeout`、`help`。
- 成功：响应 JSON 一行写到 stdout，退出码 0。
- 失败：stderr 一行 `{"code":"<错误码>","detail":"<文案>","traceId":"<trace-id>"}`；退出码按 KIND：VALIDATION 2、NOTFOUND 3、DENIED 4、CONFLICT 5、UNAVAILABLE 6、TIMEOUT 7、CANCELLED 8、其他 1。用法错误（无命令、未知命令、选项或载荷不合法）→ 64，stderr 为同样的 JSON（`HOSTING_VALIDATION_001` / `HOSTING_NOTFOUND_002`）。

## 内置命令

| 命令 | 输出 |
| --- | --- |
| `help`（或无命令、`--help`） | 用法与命令列表；无命令时写到 stderr 并返回 64 |
| `graph` | `App.DescribeGraph()` |
| `pipeline` | `App.DescribePipeline()` |
| `operations` | 每行 `ID<TAB>稳定性<TAB>暴露<TAB>permission=…/policy=…/-` |
| `invoke` | 见上 |
| `mcp`（`Protocols.Mcp` 注册） | stdio 传输：stdin / stdout 每行一条 JSON-RPC 消息 |

## 进程约定

- 日志只写 stderr；stdout 只有命令输出。
- Ctrl+C 取消当前命令（退出码 8），宿主仍按顺序停止。
