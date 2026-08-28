// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using XiHan.Framework.VirtualFileSystem.Events;

namespace XiHan.Framework.VirtualFileSystem.Tests.Events;

/// <summary>
/// 文件变化事件参数测试
/// </summary>
public class FileChangedEventArgsTests
{
    /// <summary>
    /// 构造后两个属性原样保留
    /// </summary>
    [Theory]
    [InlineData("/config/app.json", FileChangeType.Created)]
    [InlineData("/config/app.json", FileChangeType.Modified)]
    [InlineData("embedded://Asm/res.txt", FileChangeType.Deleted)]
    public void Constructor_KeepsFilePathAndChangeType(string filePath, FileChangeType changeType)
    {
        var args = new FileChangedEventArgs(filePath, changeType);

        Assert.Equal(filePath, args.FilePath);
        Assert.Equal(changeType, args.ChangeType);
    }

    /// <summary>
    /// 继承自 EventArgs，才能被标准事件签名承载
    /// </summary>
    [Fact]
    public void Type_DerivesFromEventArgs()
    {
        var args = new FileChangedEventArgs("/a.txt", FileChangeType.Created);

        Assert.IsAssignableFrom<EventArgs>(args);
    }

    /// <summary>
    /// 两个字段值相同的实例不相等，事件参数按引用语义处理
    /// </summary>
    /// <remarks>
    /// 锁住「不是 record」这一点：变更事件是逐条推送的，若改成值相等语义，
    /// 去重逻辑会把同一路径的连续两次变化误合并。
    /// </remarks>
    [Fact]
    public void Equality_IsReferenceBased()
    {
        var first = new FileChangedEventArgs("/a.txt", FileChangeType.Created);
        var second = new FileChangedEventArgs("/a.txt", FileChangeType.Created);

        Assert.NotEqual(first, second);
        Assert.Equal(first, first);
    }
}
