// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// profile 请求里的嵌套对象
/// </summary>
internal sealed class VectorProfileInner
{
    /// <summary>
    /// 必填的 64 位整数
    /// </summary>
    public required long N { get; set; }
}
