//! XiHan Framework Wasm 插件示例（RFC-0018）：以导出函数 + 线性内存实现 xihan.plugin.v1，
//! 消息按 proto3 JSON 编码；只依赖 serde_json 与 base64，不依赖任何 XiHan 包。

use base64::engine::general_purpose::STANDARD as BASE64;
use base64::Engine;
use serde_json::{json, Map, Value};
use std::alloc::{alloc, dealloc, Layout};
use std::cell::RefCell;
use std::time::Instant;

/// 清单 ID。
const PLUGIN_ID: &str = "xihan.samples.echowasm";

/// 清单文本：与插件目录里的文件逐字节相同，握手时原样交给宿主核对。
const MANIFEST: &str = include_str!("../plugin/xihan.manifest.json");

/// 宿主导入（wasm.md §4）。
#[link(wasm_import_module = "xihan")]
extern "C" {
    fn log(level: i32, ptr: i32, len: i32);
    fn invoke(ptr: i32, len: i32) -> i64;
}

/// 插件状态：配置、收到的事件、中间件计数与自报的生命周期状态。
#[derive(Default)]
struct State {
    greeting: Option<String>,
    granted: Vec<String>,
    events: Vec<String>,
    middleware_before: u64,
    middleware_after: u64,
    correlations: u64,
    lifecycle: String,
}

thread_local! {
    static STATE: RefCell<State> = RefCell::new(State { lifecycle: "LIFECYCLE_STATE_RESOLVED".to_string(), ..Default::default() });
}

// ---------------------------------------------------------------------------
// ABI：版本、内存
// ---------------------------------------------------------------------------

#[no_mangle]
pub extern "C" fn xihan_abi_version() -> i32 {
    1
}

#[no_mangle]
pub extern "C" fn xihan_alloc(size: i32) -> i32 {
    if size <= 0 {
        return 0;
    }
    // 对齐 1：宿主只写字节
    unsafe { alloc(Layout::from_size_align_unchecked(size as usize, 1)) as i32 }
}

#[no_mangle]
pub extern "C" fn xihan_free(ptr: i32, size: i32) {
    if ptr == 0 || size <= 0 {
        return;
    }
    unsafe { dealloc(ptr as *mut u8, Layout::from_size_align_unchecked(size as usize, 1)) }
}

/// 读宿主写入的请求并回收这块内存（wasm.md §2：传入的内存由插件回收）。
fn input(ptr: i32, len: i32) -> Value {
    if ptr == 0 || len <= 0 {
        return Value::Object(Map::new());
    }
    let bytes = unsafe { std::slice::from_raw_parts(ptr as *const u8, len as usize) };
    let value = serde_json::from_slice(bytes).unwrap_or_else(|_| Value::Object(Map::new()));
    xihan_free(ptr, len);
    value
}

/// 把响应写进客体内存并以 (ptr << 32) | len 返回。
fn output(value: &Value) -> i64 {
    let bytes = value.to_string().into_bytes();
    let len = bytes.len() as i32;
    let ptr = xihan_alloc(len);
    if ptr == 0 {
        return 0;
    }
    unsafe { std::ptr::copy_nonoverlapping(bytes.as_ptr(), ptr as *mut u8, len as usize) }
    ((ptr as i64) << 32) | (len as i64 & 0xFFFF_FFFF)
}

/// 读宿主导入函数的响应（宿主经 xihan_alloc 写入，由插件回收）。
fn import_result(packed: i64) -> Value {
    if packed == 0 {
        return Value::Null;
    }
    input((packed >> 32) as i32, (packed & 0xFFFF_FFFF) as i32)
}

/// 写一行日志。
fn log_line(level: i32, text: &str) {
    unsafe { log(level, text.as_ptr() as i32, text.len() as i32) }
}

// ---------------------------------------------------------------------------
// 结果与载荷
// ---------------------------------------------------------------------------

fn ok() -> Value {
    json!({ "ok": true })
}

fn failure(code: &str, message_key: &str, details: Option<Value>) -> Value {
    let mut error = json!({ "code": code, "messageKey": message_key });
    if let Some(details) = details {
        error["details"] = Value::String(details.to_string());
    }
    json!({ "ok": false, "error": error })
}

/// 请求载荷：proto3 JSON 里 bytes 是 base64。
fn payload_of(request: &Value) -> Value {
    request["payload"]
        .as_str()
        .and_then(|text| BASE64.decode(text).ok())
        .and_then(|bytes| serde_json::from_slice(&bytes).ok())
        .unwrap_or_else(|| Value::Object(Map::new()))
}

fn success(payload: &Value) -> Value {
    json!({ "status": ok(), "payload": BASE64.encode(payload.to_string()) })
}

fn baggage(request: &Value, key: &str) -> Option<String> {
    request["metadata"]["baggage"][key].as_str().map(str::to_string)
}

// ---------------------------------------------------------------------------
// 生命周期
// ---------------------------------------------------------------------------

#[no_mangle]
pub extern "C" fn xihan_handshake(ptr: i32, len: i32) -> i64 {
    let request = input(ptr, len);
    let versions = request["protocolVersions"].as_array().cloned().unwrap_or_default();
    if !versions.iter().any(|v| v.as_u64() == Some(1)) {
        // 没有共同版本：按协议以失败状态表达（Wasm 没有 gRPC 状态）
        return output(&json!({ "status": failure("PLUGINS_VALIDATION_003", "plugins.handshake.invalid", None) }));
    }
    log_line(2, "handshake");
    let operation = |id: &str, expose: Vec<&str>| json!({ "id": id, "stability": "experimental", "expose": expose });
    let response = json!({
        "protocolVersion": 1,
        "primitives": request["primitives"].as_u64().unwrap_or(1),
        "manifest": MANIFEST,
        "sdk": { "name": "EchoWasmPlugin", "version": "5.0.0-preview.1", "language": "rust" },
        "operations": [
            {
                "id": "echoes.create", "stability": "experimental", "expose": ["http", "mcp", "commandline"],
                "requestSchema": "{\"type\":\"object\",\"properties\":{\"text\":{\"type\":\"string\"}},\"required\":[\"text\"]}",
                "responseSchema": "{\"type\":\"object\",\"properties\":{\"text\":{\"type\":\"string\"},\"plugin\":{\"type\":\"string\"},\"greeting\":{\"type\":\"string\"},\"tenant\":{\"type\":\"string\"}}}"
            },
            operation("echoes.relay", vec!["http"]),
            { "id": "echoes.trap", "stability": "internal" },
            { "id": "echoes.spin", "stability": "internal" },
            operation("events.list", vec!["http"])
        ],
        "subscriptions": [ { "topic": "echo.events" } ],
        "catalogs": [
            {
                "name": "echowasm",
                "locales": [
                    { "locale": "en-US", "templates": { "echowasm.text.empty": "The text must not be empty.", "echowasm.tenant.blocked": "Tenant '{tenant}' is blocked by the echo guard." } },
                    { "locale": "zh-CN", "templates": { "echowasm.text.empty": "文本不能为空。", "echowasm.tenant.blocked": "租户 '{tenant}' 被回显守卫拦截。" } }
                ]
            }
        ]
    });
    output(&response)
}

#[no_mangle]
pub extern "C" fn xihan_configure(ptr: i32, len: i32) -> i64 {
    let request = input(ptr, len);
    let configuration: Value = request["configuration"].as_str().and_then(|text| serde_json::from_str(text).ok()).unwrap_or(Value::Null);
    STATE.with(|state| {
        let mut state = state.borrow_mut();
        state.greeting = configuration["Greeting"].as_str().map(str::to_string);
        state.granted = request["grantedPermissions"].as_array().map(|items| items.iter().filter_map(|v| v.as_str().map(str::to_string)).collect()).unwrap_or_default();
        state.lifecycle = "LIFECYCLE_STATE_CONFIGURED".to_string();
    });
    output(&json!({ "status": ok() }))
}

#[no_mangle]
pub extern "C" fn xihan_start(ptr: i32, len: i32) -> i64 {
    let _ = input(ptr, len);
    STATE.with(|state| state.borrow_mut().lifecycle = "LIFECYCLE_STATE_READY".to_string());
    log_line(2, "started");
    output(&json!({ "status": ok() }))
}

#[no_mangle]
pub extern "C" fn xihan_stop(ptr: i32, len: i32) -> i64 {
    let request = input(ptr, len);
    log_line(2, &format!("stop requested ({})", request["reason"].as_str().unwrap_or("")));
    STATE.with(|state| state.borrow_mut().lifecycle = "LIFECYCLE_STATE_STOPPED".to_string());
    output(&json!({ "status": ok() }))
}

#[no_mangle]
pub extern "C" fn xihan_health(ptr: i32, len: i32) -> i64 {
    let _ = input(ptr, len);
    let lifecycle = STATE.with(|state| state.borrow().lifecycle.clone());
    output(&json!({ "state": lifecycle, "items": [ { "name": "echo", "status": "HEALTH_STATUS_HEALTHY" } ] }))
}

// ---------------------------------------------------------------------------
// 调用、事件、中间件
// ---------------------------------------------------------------------------

#[no_mangle]
pub extern "C" fn xihan_invoke(ptr: i32, len: i32) -> i64 {
    let request = input(ptr, len);
    let operation = request["operation"].as_str().unwrap_or("").to_string();
    let payload = payload_of(&request);
    let response = match operation.as_str() {
        "echoes.create" => {
            let text = payload["text"].as_str().unwrap_or("");
            if text.trim().is_empty() {
                json!({ "status": failure("ECHOWASM_VALIDATION_001", "echowasm.text.empty", None) })
            } else {
                let mut echoed = json!({ "text": text, "plugin": PLUGIN_ID });
                if let Some(greeting) = STATE.with(|state| state.borrow().greeting.clone()) {
                    echoed["greeting"] = Value::String(greeting);
                }
                if let Some(tenant) = baggage(&request, "xihan.tenant") {
                    echoed["tenant"] = Value::String(tenant);
                }
                if let Some(correlation) = baggage(&request, "xihan.correlation") {
                    echoed["correlation"] = Value::String(correlation);
                }
                success(&echoed)
            }
        }
        "echoes.relay" => {
            // 经宿主回调调用 payload.operation，把宿主的应答原样返回
            let inner = payload["payload"].clone();
            let relay = json!({
                "operation": payload["operation"].as_str().unwrap_or(""),
                "payload": BASE64.encode(inner.to_string()),
                "metadata": request["metadata"].clone()
            });
            let text = relay.to_string();
            let packed = unsafe { invoke(text.as_ptr() as i32, text.len() as i32) };
            match import_result(packed) {
                Value::Null => json!({ "status": failure("ECHOWASM_UNAVAILABLE_001", "echowasm.host.unavailable", None) }),
                relayed => relayed,
            }
        }
        "echoes.trap" => {
            log_line(3, "trapping on request");
            // 故意陷阱：宿主应记为崩溃并重建实例
            unreachable!("echoes.trap");
        }
        "echoes.spin" => {
            // 忙等指定毫秒：宿主应按截止时间中断
            let millis = payload["millis"].as_u64().unwrap_or(0);
            let started = Instant::now();
            let mut spins: u64 = 0;
            while started.elapsed().as_millis() < millis as u128 {
                spins = spins.wrapping_add(1);
            }
            success(&json!({ "spins": spins }))
        }
        "events.list" => STATE.with(|state| {
            let state = state.borrow();
            success(&json!({ "ids": state.events, "middlewareBefore": state.middleware_before, "middlewareAfter": state.middleware_after }))
        }),
        _ => json!({ "status": failure("KERNEL_NOTFOUND_001", "kernel.operation.unknown", Some(json!({ "id": operation }))) }),
    };
    output(&response)
}

#[no_mangle]
pub extern "C" fn xihan_handle_event(ptr: i32, len: i32) -> i64 {
    let request = input(ptr, len);
    if let Some(id) = request["envelope"]["id"].as_str() {
        STATE.with(|state| state.borrow_mut().events.push(id.to_string()));
    }
    output(&json!({ "status": ok() }))
}

#[no_mangle]
pub extern "C" fn xihan_middleware(ptr: i32, len: i32) -> i64 {
    let request = input(ptr, len);
    let response = match request["phase"].as_str().unwrap_or("") {
        "before" => STATE.with(|state| {
            let mut state = state.borrow_mut();
            state.middleware_before += 1;
            // 守卫：被封禁的租户直接短路；其余补一个关联 ID
            if baggage(&request, "xihan.tenant").as_deref() == Some("blocked") {
                let tenant = "blocked";
                json!({
                    "action": "MIDDLEWARE_ACTION_SHORT_CIRCUIT",
                    "outcome": failure("ECHOWASM_DENIED_001", "echowasm.tenant.blocked", Some(json!({ "tenant": tenant })))
                })
            } else if baggage(&request, "xihan.correlation").is_none() {
                state.correlations += 1;
                json!({ "action": "MIDDLEWARE_ACTION_CONTINUE", "baggage": { "xihan.correlation": format!("wasm-{}", state.correlations) } })
            } else {
                json!({ "action": "MIDDLEWARE_ACTION_CONTINUE" })
            }
        }),
        _ => STATE.with(|state| {
            state.borrow_mut().middleware_after += 1;
            json!({ "action": "MIDDLEWARE_ACTION_CONTINUE" })
        }),
    };
    output(&response)
}
