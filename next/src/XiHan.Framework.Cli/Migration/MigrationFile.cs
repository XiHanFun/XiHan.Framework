// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

namespace XiHan.Framework.Cli.Migration;

/// <summary>
/// 正在迁移的一个文件：绝对路径、相对根目录的显示路径、所属项目目录与当前文本。
/// </summary>
/// <param name="Path">绝对路径。</param>
/// <param name="RelativePath">相对 <c>--path</c> 的路径，报告里用。</param>
/// <param name="ProjectDirectory">最近的含 <c>.csproj</c> 的祖先目录；没有为 <see langword="null"/>。</param>
/// <param name="NewLine">文件使用的换行符；生成的标记与成员沿用。</param>
internal sealed record MigrationFile(string Path, string RelativePath, string? ProjectDirectory, string NewLine)
{
    /// <summary>
    /// 同一次迁移里另一个文件（如生成文件）的描述：相对路径按本文件推出的根目录计算。
    /// </summary>
    public MigrationFile Sibling(string absolutePath)
    {
        // 绝对路径末尾去掉相对路径的长度就是根（分隔符一一对应）
        var root = Path[..^RelativePath.Length];
        return new MigrationFile(absolutePath, System.IO.Path.GetRelativePath(root, absolutePath).Replace('\\', '/'), ProjectDirectory, NewLine);
    }
}
