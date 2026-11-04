# EchoWasmPlugin

Rust 写的 Wasm 插件示例（RFC-0018，`spec/plugin/wasm.md`）：WASI preview 1 reactor，导出 `xihan_*` 函数，消息按 proto3 JSON 经线性内存传递；只依赖 `serde_json` 与 `base64`，不依赖任何 XiHan 包。

| 内容 | 说明 |
| --- | --- |
| `echoes.create` | 回显 `text`，附带配置里的 `Greeting`、Baggage 里的租户与关联 ID |
| `echoes.relay` | 经宿主导入 `xihan.invoke` 调用宿主操作（需要 `capability:kernel.pipeline`） |
| `echoes.trap` / `echoes.spin` | 故意陷阱 / 忙等，演示崩溃重建与截止时间中断（`stability: internal`，不对外发布） |
| `events.list` | 收到的事件 ID 与中间件计数 |
| 订阅 `echo.events` | `xihan_handle_event` 记录事件 ID |
| 中间件 `echo-guard`（`observe`） | before：租户 `blocked` 短路为 `ECHOWASM_DENIED_001`，否则补 `xihan.correlation`；after：计数 |

## 构建

```powershell
rustup target add wasm32-wasip1
./build.ps1            # cargo build --release --target wasm32-wasip1，产物复制到 plugin/echo.wasm
```

构建脚本（build.rs）要为宿主编译，MSVC 工具链需要 Visual Studio 的 C++ 生成工具；没有时可用自带链接器的 GNU 工具链：`rustup toolchain install stable-x86_64-pc-windows-gnu --profile minimal` 后 `./build.ps1 -Toolchain stable-x86_64-pc-windows-gnu`。

`plugin/echo.wasm` 随仓库提交：`Plugins.Wasm.Tests` 与一致性套件直接加载它，仓库 CI 不要求 Rust 工具链；改了 `src/lib.rs` 或 `plugin/xihan.manifest.json`（它以 `include_str!` 编进模块，握手时与文件核对）后重新构建并提交产物。
