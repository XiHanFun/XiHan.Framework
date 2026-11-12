// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using System.Text;

namespace XiHan.Framework.Plugins.Protocol;

/// <summary>
/// 插件工作目录的位置与权限
/// </summary>
/// <remarks>
/// 两种宿主共用（ADR-0207）。缺省的根在临时目录下、按用户分开；Unix 上根必须是本进程能确认私有的真实目录（权限 0700、不是链接），每个插件的子目录同样 0700。临时目录是所有用户共享的，名字可预测的固定目录能被别的用户抢先建成指向别处的链接或放宽权限的目录
/// </remarks>
internal static class PluginWorkDirectories
{
    /// <summary>
    /// 只有本用户能读写进入的权限
    /// </summary>
    private const UnixFileMode Private = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    /// <summary>
    /// 缺省的根：临时目录下按用户分开的目录
    /// </summary>
    /// <param name="name">宿主的目录名前缀，如 <c>xihan-plugins</c></param>
    public static string DefaultRoot(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return Path.GetFullPath(Path.Combine(Path.GetTempPath(), $"{name}-{UserSegment()}"));
    }

    /// <summary>
    /// 准备一个插件的工作目录并返回它
    /// </summary>
    /// <remarks>
    /// <paramref name="defaultRoot"/> 为真（没有配置工作目录根）时先核对根是私有的，不是就抛出 <see cref="InvalidOperationException"/>：可能是别的用户抢先建的；配置了根时由运营方负责它的权限。子目录在 Unix 上以 0700 新建，已存在的保留
    /// </remarks>
    /// <param name="root">工作目录根</param>
    /// <param name="manifestId">插件的清单 ID</param>
    /// <param name="defaultRoot">根是不是缺省的那个</param>
    public static string Prepare(string root, string manifestId, bool defaultRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestId);
        var directory = Path.Combine(root, manifestId);
        if (OperatingSystem.IsWindows())
        {
            // Windows 的临时目录本来就按用户分开、只给本用户
            Directory.CreateDirectory(directory);
            return directory;
        }

        if (defaultRoot)
        {
            EnsurePrivate(root);
        }
        else
        {
            Directory.CreateDirectory(root);
        }

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory, Private);
        }

        return directory;
    }

    /// <summary>
    /// 确保根是本用户私有的目录
    /// </summary>
    /// <remarks>
    /// 不存在就以 0700 新建；存在就核对它是不是真实目录、权限是不是 0700
    /// </remarks>
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    private static void EnsurePrivate(string root)
    {
        if (!Directory.Exists(root) && !File.Exists(root))
        {
            Directory.CreateDirectory(root, Private);
        }

        // 链接与放宽了权限的目录都不用：临时目录里名字可预测，别的用户可以抢先建好（sticky 位之后再也替换不了）
        var info = new DirectoryInfo(root);
        if (!info.Exists || info.LinkTarget is not null || File.GetUnixFileMode(root) != Private)
        {
            throw new InvalidOperationException($"The plugin work directory '{root}' is not a private directory of this user (mode 0700, not a link); remove it or configure WorkDirectory.");
        }
    }

    /// <summary>
    /// 用户名里能放进路径的部分
    /// </summary>
    /// <remarks>
    /// 字母、数字、点、下划线、连字符原样，其余换成下划线；取不到用户名时为 <c>user</c>
    /// </remarks>
    private static string UserSegment()
    {
        var name = Environment.UserName;
        if (string.IsNullOrWhiteSpace(name))
        {
            return "user";
        }

        var builder = new StringBuilder(name.Length);
        foreach (var c in name)
        {
            builder.Append(char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '_');
        }

        return builder.ToString();
    }
}
