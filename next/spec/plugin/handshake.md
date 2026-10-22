# 插件启动与握手

> 规范性。协议 `xihan.plugin.v1`（`plugin.proto`，RFC-0017）。参考实现：`XiHan.Framework.Plugins.OutOfProcess`（P4-02）。

## 1. 启动

宿主从插件目录读取 `xihan.manifest.json`（RFC-0002；`kind: plugin`、`isolation: out-of-process`），校验 Schema、原语层大版本与权限授予后启动进程：

- `entry` 是相对插件目录的可执行文件路径（Windows 上没有扩展名时补 `.exe`）；工作目录是插件目录。
- 宿主只继承白名单环境变量（`PATH`、`HOME` / `USERPROFILE`、`TMP` / `TEMP`、`LANG`、`TZ`、`DOTNET_*`、`GO*`），并追加下表；插件不得假定其他变量存在。
- 宿主把插件的 stderr 逐行转为自己的日志（级别 Information，来源 `plugins.<清单 ID>`）；stdout 只用于握手行，握手完成后的 stdout 输出按 Information 记录。

| 环境变量 | 含义 |
| --- | --- |
| `XIHAN_PLUGIN_PROTOCOL_VERSIONS` | 宿主支持的协议版本，逗号分隔、升序，如 `1` |
| `XIHAN_PLUGIN_PRIMITIVES` | 宿主的原语层大版本，如 `1` |
| `XIHAN_PLUGIN_SESSION` | 本次启动的会话令牌（随机，至少 32 字节，base64url） |
| `XIHAN_PLUGIN_HOST_ADDRESS` | 宿主回调端点：`<network> <address>`，如 `unix /run/xihan/host-1f3a.sock` |
| `XIHAN_PLUGIN_INSTANCE` | 实例 ID |
| `XIHAN_PLUGIN_LOCALE` | 宿主默认区域（BCP 47） |

## 2. 握手行

插件启动 gRPC 服务端后向 stdout 写一行（`\n` 结尾），之后不得再向 stdout 写任何东西直到握手完成：

```text
xihan-plugin/1 <network> <address>
```

- `xihan-plugin/1` 是握手行格式版本（与协议版本无关，只在行格式变化时递增）。
- `network` 为 `unix`、`tcp` 或 `npipe`；`tcp` 的地址必须是回环地址（`127.0.0.1:<port>`、`[::1]:<port>`），其他地址视为不合法。
- 文法：`^xihan-plugin/1 (unix|tcp|npipe) (\S+)$`；一致性向量 `conformance/vectors/plugin/handshake-lines.json`。

宿主在 `HandshakeTimeout`（默认 10 秒）内读到握手行并连上端点，否则终止进程并以 `PLUGINS_TIMEOUT_001` 失败；行不合法以 `PLUGINS_VALIDATION_003` 失败。

## 3. Handshake 调用

连接后宿主调用 `Plugin.Handshake`：

1. 版本协商：插件选宿主 `protocol_versions` 里最高的自己也支持的版本写入 `protocol_version`；没有共同版本时返回 gRPC `FailedPrecondition`。宿主收到不在列表里的版本同样视为失败（`PLUGINS_VALIDATION_003`）。向量：`conformance/vectors/plugin/protocol-versions.json`。
2. 原语层：`primitives` 必须与宿主相同，否则 `PLUGINS_VALIDATION_004`。
3. 清单：`manifest` 必须与目录里的文件逐字段一致（`id`、`version`、`primitives`、`kind`、`isolation`、`provides`、`requires`、`contributes`、`activation`、`permissions`），否则 `PLUGINS_VALIDATION_004`。
4. 操作：`operations[].id` 必须都出现在清单 `contributes.operations`；ID 文法与 Schema 规则同 RFC-0011；重复即 `PLUGINS_VALIDATION_004`。宿主把它们登记为代理操作（见 `invocation.md`）。
5. 订阅：`subscriptions[].topic` 必须符合主题文法；重复的（主题，类型）对即 `PLUGINS_VALIDATION_004`。
6. 目录：`catalogs` 合并进宿主的本地化器（目录名重复时宿主已有的优先并记警告）；至少应含 `en-US`。

握手成功后插件状态为 `Resolved`；宿主接着 `Configure` 与 `Start`（`lifecycle.md`）。整个握手阶段的 gRPC 调用都带 `HandshakeTimeout` 的截止时间。

## 4. 会话令牌与回调

- 插件回调 `Host` 服务时必须在元数据 `xihan-plugin-session` 里带 `XIHAN_PLUGIN_SESSION`；缺失或不匹配返回 gRPC `Unauthenticated` + `PLUGINS_DENIED_002`。
- 令牌只对本次进程有效；重启后换新令牌。
- 宿主端点与插件端点都只接受本机连接：Unix 域套接字的目录权限 `0700`、文件 `0600`；命名管道只允许当前用户；TCP 只绑定回环地址。

## 5. 失败与重试

握手阶段任何失败都终止进程、记为 `Failed`，并按 `lifecycle.md` 的重启策略处理；握手失败不会让宿主进程退出。
