// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Kernel.Hosting;
using XiHan.Framework.Primitives;
using XiHan.Framework.Primitives.Manifests;

namespace XiHan.Framework.Kernel.Plugins;

/// <summary>
/// 清单声明的绑定与解析结果的核对
/// </summary>
/// <remarks>
/// 规则出自 RFC-0016「资源绑定与配置节」第 8 条与 <c>spec/plugin/lifecycle.md</c> §2「<c>bindings</c> 的核对」（ADR-0292、ADR-0301）：内核装配特性时（不合即 <see cref="KernelErrors.MissingBinding"/>，启动失败）、进程外与 Wasm 宿主组装 <c>Configure</c> 请求时、进程内宿主加载插件代码之前，按清单的顺序对每个声明的绑定核对一次，先核对有没有、再核对类型，报第一处不合的。
/// 按有没有能用的内容判断：只给了类型的绑定插件收不到，特性拿到的也只是一个类型；可选的绑定也核对类型，因为它一旦给出，插件与特性都会拿它去连。名字里的 Plugin 是最初的使用方，内核装配特性用的是同一处
/// </remarks>
[ApiLevel(Stability.Preview, "5.0")]
public static class PluginBindingRules
{
    /// <summary>
    /// 解析出的绑定有没有能交给插件或特性的内容
    /// </summary>
    /// <remarks>
    /// 主值或至少一个属性；只给了类型（或一个空的绑定目录）的没有
    /// </remarks>
    public static bool HasContent(BindingValue binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        return binding.Value is not null || binding.Properties.Count > 0;
    }

    /// <summary>
    /// 找出一个声明的绑定与解析结果对不上的地方
    /// </summary>
    /// <remarks>
    /// 有值时是写进错误 <c>reason</c>（内核装配特性时是异常消息）的固定英文文本：必需的绑定没有能用的内容为 <c>binding '&lt;绑定名&gt;' is required but not provided</c>；来源声明了类型、而与清单声明的不同（按序数比较）为 <c>binding '&lt;绑定名&gt;' is of type '&lt;来源的类型&gt;' but the manifest declares '&lt;清单的类型&gt;'</c>。
    /// 绑定名是清单里的名字；类型不是凭据，原样写出，主值与属性都不出现。可选的绑定没有内容、或来源没有声明类型时为无值
    /// </remarks>
    /// <param name="declared">清单里的声明</param>
    /// <param name="resolved">按 RFC-0016 解析出的同名绑定；两种来源都没给时为 <see langword="null"/></param>
    public static Maybe<string> FindViolation(Binding declared, BindingValue? resolved)
    {
        ArgumentNullException.ThrowIfNull(declared);

        // 先有没有、后类型：没有内容的绑定不会下发、特性也用不上，谈不上类型对不对
        if (resolved is null || !HasContent(resolved))
        {
            return declared.Required ? Maybe.Some($"binding '{declared.Name}' is required but not provided") : default;
        }

        // 来源没声明类型（空串）时下发的是清单的类型，不算不同
        if (resolved.Type.Length > 0 && !string.Equals(resolved.Type, declared.Type, StringComparison.Ordinal))
        {
            return Maybe.Some($"binding '{declared.Name}' is of type '{resolved.Type}' but the manifest declares '{declared.Type}'");
        }

        return default;
    }
}
