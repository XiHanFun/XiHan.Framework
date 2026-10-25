// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using Wasmtime;
using XiHan.Framework.Plugins.Wasm.Runtime;

namespace XiHan.Framework.Plugins.Wasm.Instances;

/// <summary>
/// 一个模块实例：自己的 Store（内存上限、WASI、epoch 截止）、导入函数与导出函数的委托；按 ABI v1 的内存约定做一次调用。
/// 实例不是线程安全的，调用由运行时串行化；回调引发的再入由 <see cref="WasmCallPump"/> 回到执行线程上。
/// </summary>
internal sealed class WasmInstance : IDisposable
{
    /// <summary>
    /// Store：内存上限、WASI 配置与 epoch 截止时间都挂在它上面。
    /// </summary>
    private readonly Store _store;

    /// <summary>
    /// 链接器：WASI 与 <c>xihan</c> 导入。
    /// </summary>
    private readonly Linker _linker;

    /// <summary>
    /// 线性内存。
    /// </summary>
    private readonly Memory _memory;

    /// <summary>
    /// <c>xihan_alloc</c>。
    /// </summary>
    private readonly Func<int, int> _alloc;

    /// <summary>
    /// <c>xihan_free</c>。
    /// </summary>
    private readonly Action<int, int> _free;

    /// <summary>
    /// 各调用导出。
    /// </summary>
    private readonly Dictionary<string, Func<int, int, long>> _calls = new(StringComparer.Ordinal);

    /// <summary>
    /// epoch 递增间隔，用于把时限换算成 epoch 配额。
    /// </summary>
    private readonly TimeSpan _epochInterval;

    /// <summary>
    /// 所属插件的调用泵。
    /// </summary>
    private readonly WasmCallPump _pump;

    /// <summary>
    /// 实例化：定义导入、设上限与 WASI、实例化模块、调用 <c>_initialize</c>、读 ABI 版本并解析导出。
    /// </summary>
    /// <param name="engine">引擎。</param>
    /// <param name="module">已校验的模块。</param>
    /// <param name="functions">宿主导入函数。</param>
    /// <param name="wasi">WASI 配置。</param>
    /// <param name="memoryLimit">线性内存上限（字节）。</param>
    /// <param name="epochInterval">epoch 递增间隔。</param>
    /// <param name="startupTimeout">实例化与初始化的时限。</param>
    public WasmInstance(Engine engine, WasmModule module, WasmHostFunctions functions, WasiConfiguration wasi, long memoryLimit, TimeSpan epochInterval, TimeSpan startupTimeout)
    {
        _epochInterval = epochInterval;
        _pump = functions.Pump;
        _store = new Store(engine);
        _linker = new Linker(engine);
        try
        {
            _store.SetLimits(memorySize: memoryLimit);
            _store.SetWasiConfiguration(wasi);
            _linker.DefineWasi();
            // 三个导入始终定义；模块没有导入它们时多余的定义无害
            _linker.DefineFunction(WasmModule.ImportModule, "log", (Caller caller, int level, int ptr, int len) => functions.Log(caller, level, ptr, len));
            _linker.DefineFunction(WasmModule.ImportModule, "invoke", (Caller caller, int ptr, int len) => functions.Invoke(caller, ptr, len));
            _linker.DefineFunction(WasmModule.ImportModule, "publish", (Caller caller, int ptr, int len) => functions.Publish(caller, ptr, len));

            SetDeadline(startupTimeout);
            var instance = _linker.Instantiate(_store, module.Module);
            _memory = instance.GetMemory("memory") ?? throw new InvalidOperationException("The module does not export 'memory'.");
            if (module.HasInitialize)
            {
                instance.GetAction("_initialize")?.Invoke();
            }

            AbiVersion = (uint)(instance.GetFunction<int>("xihan_abi_version")?.Invoke() ?? 0);
            _alloc = instance.GetFunction<int, int>("xihan_alloc") ?? throw new InvalidOperationException("The module does not export 'xihan_alloc'.");
            _free = instance.GetAction<int, int>("xihan_free") ?? throw new InvalidOperationException("The module does not export 'xihan_free'.");
            foreach (var name in module.Calls)
            {
                _calls[name] = instance.GetFunction<int, int, long>(name) ?? throw new InvalidOperationException($"The module does not export '{name}'.");
            }
        }
        catch
        {
            _linker.Dispose();
            _store.Dispose();
            throw;
        }
    }

    /// <summary>
    /// 模块报告的 ABI 版本。
    /// </summary>
    public uint AbiVersion { get; }

    /// <summary>
    /// 模块是否导出该调用。
    /// </summary>
    public bool Supports(string export) => _calls.ContainsKey(export);

    /// <summary>
    /// 一次外层调用：设截止时间、在当前线程上登记执行线程，把请求写进客体内存，调用导出，读回响应并释放。
    /// </summary>
    public WasmCallOutcome Call(string export, ReadOnlyMemory<byte> request, TimeSpan timeout)
        => _pump.Run(() => CallCore(export, request, timeout));

    /// <summary>
    /// 一次再入调用：外层调用的导入函数等待期间、在执行线程上调用导出；不另设截止时间，受外层调用的截止时间约束。
    /// </summary>
    public WasmCallOutcome CallNested(string export, ReadOnlyMemory<byte> request)
        => CallCore(export, request, null);

    /// <summary>
    /// 调用导出并按内存约定交换请求与响应。
    /// </summary>
    private WasmCallOutcome CallCore(string export, ReadOnlyMemory<byte> request, TimeSpan? timeout)
    {
        if (!_calls.TryGetValue(export, out var call))
        {
            return WasmCallOutcome.Failed(WasmCallStatus.Faulted, $"the module does not export '{export}'");
        }

        if (timeout is { } limit)
        {
            SetDeadline(limit);
        }

        try
        {
            var pointer = 0;
            if (request.Length > 0)
            {
                pointer = _alloc(request.Length);
                if (pointer <= 0 || pointer + request.Length > _memory.GetLength())
                {
                    return WasmCallOutcome.Failed(WasmCallStatus.Exhausted, $"xihan_alloc could not provide {request.Length} bytes for the request");
                }

                request.Span.CopyTo(_memory.GetSpan(pointer, request.Length));
            }

            var packed = call(pointer, request.Length);
            if (packed == 0)
            {
                return WasmCallOutcome.Failed(WasmCallStatus.NoResponse, $"'{export}' returned no response");
            }

            var responsePointer = (int)(packed >> 32);
            var responseLength = (int)(packed & 0xFFFF_FFFF);
            if (responsePointer <= 0 || responseLength < 0 || responsePointer + (long)responseLength > _memory.GetLength())
            {
                return WasmCallOutcome.Failed(WasmCallStatus.Exhausted, $"'{export}' returned a response outside the linear memory");
            }

            var response = _memory.GetSpan(responsePointer, responseLength).ToArray();
            _free(responsePointer, responseLength);
            return WasmCallOutcome.Succeeded(response);
        }
        catch (TrapException exception)
        {
            return exception.Type == TrapCode.Interrupt
                ? WasmCallOutcome.Failed(WasmCallStatus.Timeout, exception.Message)
                : WasmCallOutcome.Failed(WasmCallStatus.Trapped, $"{exception.Type}: {exception.Message}");
        }
        catch (WasmtimeException exception)
        {
            return WasmCallOutcome.Failed(WasmCallStatus.Faulted, exception.Message);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _linker.Dispose();
        _store.Dispose();
    }

    /// <summary>
    /// 把时限换算成 epoch 配额：至少 1 个刻度。
    /// </summary>
    private void SetDeadline(TimeSpan timeout)
        => _store.SetEpochDeadline((ulong)Math.Max(1, Math.Ceiling(timeout / _epochInterval)));
}
