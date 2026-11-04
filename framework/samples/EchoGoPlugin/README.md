# EchoGoPlugin

用 [framework-sdk-go](https://github.com/XiHanFun/framework-sdk-go) 写的进程外回声插件，由 .NET 宿主（`Plugins.OutOfProcess`）加载，是"Go 插件在 .NET 宿主中服务一个操作"的端到端示例（P4-06）。操作、订阅、健康与配置的实现来自 SDK 的 `examples/echo`（清单 ID `xihan.samples.echo-go`），本目录只有可执行入口 `main.go`、同一份 `xihan.manifest.json` 与 Go 模块文件。

| 操作 | 行为 |
| --- | --- |
| `echoes.create` | 回声 `text`，附配置节 `XiHan:Echo` 的 `greeting` 与上下文里的租户、主体、关联 ID；`text` 为空返回 `ECHOGO_VALIDATION_002`（消息键 `echogo.text.missing`，宿主用握手时拿到的目录翻译） |
| `echoes.relay` | 经宿主回调 `Host.Invoke` 调用 `operation`，需要权限 `capability:kernel.pipeline` |
| `echoes.crash` | 不应答直接退出，宿主记为崩溃并按退避重启 |
| `events.list` | 列出经订阅 `echo.events` 收到的事件 ID |

## 构建

```powershell
./build.ps1          # 产物在 bin/：echo-go(.exe) + xihan.manifest.json
```

需要 Go 1.26+。`go.mod` 以伪版本锁定 SDK 的提交；升级 SDK 用 `go get github.com/xihanfun/framework-sdk-go@<提交或标签>`。

## 测试

`XiHan.Framework.Plugins.OutOfProcess.Tests` 的 `EchoGoPluginConformanceTests`（协议黑盒套件）与 `EchoGoPluginHostTests`（发现、经管道调用、本地化、回调身份、事件、崩溃重启、停止）在测试运行时用本机的 `go` 构建本目录，没有 `go` 时跳过；CI 用 `actions/setup-go` 安装。也可以直接用规范仓库的运行器：

```bash
xihan-conformance plugin --dir bin --exclude echoes.crash --sample 'echoes.create={"text":"hi"}'
```
