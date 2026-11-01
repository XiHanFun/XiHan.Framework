// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Collections.Concurrent;
using XiHan.Framework.Primitives;

namespace XiHan.Framework.Persistence.Conformance.Fixtures;

/// <summary>
/// 用例交给套件操作执行的脚本。
/// </summary>
/// <remarks>
/// 单例。用例登记一段脚本得到键，以键为请求调用 <see cref="ReadScriptOperation"/> 或 <see cref="WriteScriptOperation"/>，脚本就在真实操作的处理器里、经完整管道执行；只读与否由操作契约决定，与真实应用一致。脚本执行时取出即删除。
/// </remarks>
internal sealed class ConformanceScripts
{
    /// <summary>
    /// 待执行的脚本，按键索引。
    /// </summary>
    private readonly ConcurrentDictionary<string, Func<InvocationContext, Task<Result>>> _scripts = new(StringComparer.Ordinal);

    /// <summary>
    /// 登记脚本。
    /// </summary>
    /// <returns>作为请求交给操作的键。</returns>
    public string Add(Func<InvocationContext, Task<Result>> script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var key = Guid.NewGuid().ToString("N");
        _scripts[key] = script;
        return key;
    }

    /// <summary>
    /// 在操作的处理器里执行脚本。
    /// </summary>
    /// <remarks>
    /// 脚本的结果成为操作的结果：成功时响应带回键，失败时原样返回错误。
    /// </remarks>
    public async ValueTask<Result<ConformanceScriptResponse>> RunAsync(ConformanceScriptRequest request, InvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(context);
        if (!_scripts.TryRemove(request.Script, out var script))
        {
            throw new InvalidOperationException($"Conformance script '{request.Script}' is not registered.");
        }

        var result = await script(context).ConfigureAwait(false);
        return result.IsSuccess ? new ConformanceScriptResponse(request.Script) : result.Error!;
    }
}
