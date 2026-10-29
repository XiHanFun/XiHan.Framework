// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Primitives;

namespace XiHan.Framework.Kernel.Operations;

/// <summary>
/// 程序集级的操作缺省值。
/// </summary>
/// <remarks>
/// 本程序集里没有显式写 <see cref="OperationAttribute.Expose"/> 的操作取 <see cref="Expose"/>，例如 <c>[assembly: OperationDefaults(Expose = Expose.Http)]</c>让管理操作缺省只走 HTTP，需要更多协议的操作再显式写。与 <see cref="OperationAttribute"/> 一样只是源生成器（<c>Kernel.Generators</c>）的输入，运行期不读：生成的 <see cref="OperationContract"/> 里已经是落定的值。没有本特性时缺省为 <see cref="Expose.All"/>；想强制每个操作都写明暴露协议时打开诊断 <c>XHG004</c>（<c>dotnet_diagnostic.XHG004.severity = error</c>），它在没有本特性且操作没写 <c>Expose</c> 时报告。
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
[AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = false)]
public sealed class OperationDefaultsAttribute : Attribute
{
    /// <summary>
    /// 本程序集操作的缺省暴露协议。
    /// </summary>
    /// <remarks>
    /// 默认 <see cref="Expose.All"/>。
    /// </remarks>
    public Expose Expose { get; init; } = Expose.All;
}
