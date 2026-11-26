// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Kernel.Tests.Fixtures;

/// <summary>
/// 一层套一层的对象
/// </summary>
/// <param name="Next">里面一层；最里层为 <see langword="null"/></param>
/// <param name="Value">最里层的值</param>
internal sealed record DepthNode(DepthNode? Next, int? Value)
{
    /// <summary>
    /// 造给定层数的对象链
    /// </summary>
    /// <remarks>
    /// 最里层带一个数值成员：值本身不加层数，System.Text.Json 却在写它之前核对深度
    /// </remarks>
    /// <param name="levels">对象的层数</param>
    public static DepthNode Make(int levels)
    {
        var node = new DepthNode(null, 1);
        for (var i = 1; i < levels; i++)
        {
            node = new DepthNode(node, null);
        }

        return node;
    }
}
