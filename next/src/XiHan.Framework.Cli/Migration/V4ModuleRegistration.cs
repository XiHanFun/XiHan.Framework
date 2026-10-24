// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 一个 4.x 框架模块在 <c>ConfigureServices</c> 里做的一组注册：承接该模块时应用可能要补回的服务（<c>spec/migration/v4.md</c> §4.1）。
/// </summary>
/// <param name="Method">4.x 的扩展方法名，如 <c>AddXiHanWebCore</c>；模块直接 <c>services.Add…</c> 时为 <see langword="null"/>。</param>
/// <param name="Configuration">扩展方法是否接受 <c>IConfiguration</c>。</param>
/// <param name="Services">注册的服务、选项或组件的标识符，按原文列出。</param>
internal sealed record V4ModuleRegistration(string? Method, bool Configuration, IReadOnlyList<string> Services)
{
    /// <summary>
    /// 标记里的写法：<c>AddXiHanWebCore(configuration): IHttpContextAccessor, …</c>；没有扩展方法时只列服务。
    /// </summary>
    public override string ToString()
    {
        var services = string.Join(", ", Services);
        return Method is null ? services : $"{Method}({(Configuration ? "configuration" : string.Empty)}): {services}";
    }
}
