// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.VirtualFileSystem.Events;

namespace XiHan.Framework.VirtualFileSystem.Tests.Events;

/// <summary>
/// 文件变化类型枚举测试
/// </summary>
/// <remarks>
/// 该枚举会随变更事件跨进程/跨版本传递，底层数值一旦漂移，订阅方会把「新增」当成「修改」处理，
/// 因此这里把数值锁死，新增成员只能往后追加。
/// </remarks>
public class FileChangeTypeTests
{
    /// <summary>
    /// 底层数值稳定不漂移
    /// </summary>
    [Theory]
    [InlineData(FileChangeType.Created, 0)]
    [InlineData(FileChangeType.Modified, 1)]
    [InlineData(FileChangeType.Deleted, 2)]
    public void UnderlyingValue_IsStable(FileChangeType changeType, int expected)
    {
        Assert.Equal(expected, (int)changeType);
    }

    /// <summary>
    /// 成员集合恰好是三个，新增成员必须显式修改本用例
    /// </summary>
    [Fact]
    public void Members_AreExactlyThree()
    {
        var values = Enum.GetValues<FileChangeType>();

        Assert.Equal(3, values.Length);
        Assert.Contains(FileChangeType.Created, values);
        Assert.Contains(FileChangeType.Modified, values);
        Assert.Contains(FileChangeType.Deleted, values);
    }
}
