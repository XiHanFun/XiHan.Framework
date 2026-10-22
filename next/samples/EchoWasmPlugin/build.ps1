# 构建 Wasm 插件示例并把产物复制到 plugin/echo.wasm（随仓库提交；测试与一致性套件直接加载它）。
# 需要：rustup 与 wasm32-wasip1 目标（rustup target add wasm32-wasip1）。
# -Toolchain 指定 cargo 的工具链；没有 Visual Studio C++ 生成工具时用 stable-x86_64-pc-windows-gnu。
param([string] $Toolchain = '')
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    if ($Toolchain) {
        cargo "+$Toolchain" build --release --target wasm32-wasip1
    }
    else {
        cargo build --release --target wasm32-wasip1
    }

    if ($LASTEXITCODE -ne 0) {
        throw "cargo build failed with exit code $LASTEXITCODE"
    }

    Copy-Item 'target/wasm32-wasip1/release/echo_wasm_plugin.wasm' 'plugin/echo.wasm' -Force
    Get-Item 'plugin/echo.wasm' | Select-Object Name, Length
}
finally {
    Pop-Location
}
