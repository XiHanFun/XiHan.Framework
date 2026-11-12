// Copyright (c) XiHanFun Contributors. Licensed under the MIT License.
// See the LICENSE file in the project root for full license text.

using XiHan.Framework.Plugins.Protocol;

namespace XiHan.Framework.Plugins.OutOfProcess.Tests;

/// <summary>
/// 插件工作目录缺省按用户分开、只给本用户（ADR-0207）
/// </summary>
/// <remarks>
/// 安全审查 S-37：原先缺省是临时目录下名字固定的 <c>xihan-plugins</c>，别的用户能抢先把它建成指向别处的链接或放宽权限的目录
/// </remarks>
public sealed class PluginWorkDirectoriesTests
{
    [Fact]
    public void DefaultRoots_ArePerUser_NotAFixedNameInTheSharedTemp()
    {
        var temp = Path.GetFullPath(Path.GetTempPath());

        // 修之前缺省根是临时目录下的固定名字（Wasm 宿主同理，见它的测试）
        Assert.NotEqual(Path.Combine(temp, "xihan-plugins"), new OutOfProcessPluginOptions().ResolveWorkDirectory());
        Assert.StartsWith(Path.Combine(temp, "xihan-plugins-"), new OutOfProcessPluginOptions().ResolveWorkDirectory(), StringComparison.Ordinal);
        Assert.Equal(Path.GetFullPath("/srv/plugins"), new OutOfProcessPluginOptions { WorkDirectory = "/srv/plugins" }.ResolveWorkDirectory());
    }

    [Fact]
    public void DefaultRoot_IsCreatedPrivate_AndAChildIsPrivateToo()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("The Windows temp directory is already per user.");
            return;
        }

        var root = Path.Combine(Path.GetTempPath(), "xihan-workdir-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var child = PluginWorkDirectories.Prepare(root, "acme.echo", defaultRoot: true);

            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(root));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(child));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void DefaultRoot_ThatIsALinkOrNotPrivate_IsRefused()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("The Windows temp directory is already per user.");
            return;
        }

        var target = Path.Combine(Path.GetTempPath(), "xihan-workdir-target-" + Guid.NewGuid().ToString("N"));
        var link = Path.Combine(Path.GetTempPath(), "xihan-workdir-link-" + Guid.NewGuid().ToString("N"));
        var open = Path.Combine(Path.GetTempPath(), "xihan-workdir-open-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(target, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Directory.CreateSymbolicLink(link, target);
        Directory.CreateDirectory(open, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        try
        {
            // 别的用户抢先建的链接与放宽了权限的目录都不用；配置的根由运营方负责，照常使用
            Assert.Throws<InvalidOperationException>(() => PluginWorkDirectories.Prepare(link, "acme.echo", defaultRoot: true));
            Assert.Throws<InvalidOperationException>(() => PluginWorkDirectories.Prepare(open, "acme.echo", defaultRoot: true));
            Assert.True(Directory.Exists(PluginWorkDirectories.Prepare(open, "acme.echo", defaultRoot: false)));
        }
        finally
        {
            File.Delete(link);
            Directory.Delete(target, recursive: true);
            Directory.Delete(open, recursive: true);
        }
    }
}
