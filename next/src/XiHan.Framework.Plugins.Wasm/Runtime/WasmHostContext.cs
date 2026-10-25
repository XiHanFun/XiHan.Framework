// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Microsoft.Extensions.Configuration;
using Wasmtime;

namespace XiHan.Framework.Plugins.Wasm.Runtime;

/// <summary>
/// 宿主侧所有 Wasm 插件共享的环境：引擎、选项、配置、时钟与附着后的宿主容器。
/// </summary>
internal sealed class WasmHostContext(Engine engine, WasmPluginOptions options, IConfiguration configuration, TimeProvider clock, int primitives, string defaultLocale, string application, string workRoot)
{
    /// <summary>
    /// wasmtime 引擎（已启用 epoch 中断，由宿主的计时器递增）。
    /// </summary>
    public Engine Engine { get; } = engine;

    /// <summary>
    /// 选项。
    /// </summary>
    public WasmPluginOptions Options { get; } = options;

    /// <summary>
    /// 宿主配置（插件配置节与绑定从中读取）。
    /// </summary>
    public IConfiguration Configuration { get; } = configuration;

    /// <summary>
    /// 时钟。
    /// </summary>
    public TimeProvider Clock { get; } = clock;

    /// <summary>
    /// 宿主原语层大版本。
    /// </summary>
    public int Primitives { get; } = primitives;

    /// <summary>
    /// 宿主默认区域。
    /// </summary>
    public string DefaultLocale { get; } = defaultLocale;

    /// <summary>
    /// 应用名；可空。
    /// </summary>
    public string Application { get; } = application;

    /// <summary>
    /// 插件工作目录的根。
    /// </summary>
    public string WorkRoot { get; } = workRoot;

    /// <summary>
    /// 宿主应用的根容器；附着后才有，回调经它开作用域。
    /// </summary>
    public IServiceProvider? Services { get; set; }
}
