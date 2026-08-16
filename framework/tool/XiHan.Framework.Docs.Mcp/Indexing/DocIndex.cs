// Copyright (c) 2021-Present XiHanFun and contributors.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System.Text;
using Microsoft.Extensions.Logging;
using XiHan.Framework.Docs.Mcp.Options;
using XiHan.Framework.Docs.Mcp.Sources;

namespace XiHan.Framework.Docs.Mcp.Indexing;

/// <summary>
/// 索引门面：负责建立章节集合与倒排索引，并按文件修改时间做热更新
/// </summary>
/// <param name="locator">文档来源定位器</param>
/// <param name="options">可调参数</param>
/// <param name="timeProvider">时钟，便于测试注入</param>
/// <param name="logger">日志记录器，全部写入 stderr</param>
/// <remarks>
/// 热更新采用 mtime 轮询而非 FileSystemWatcher：后者在网络磁盘、WSL 挂载
/// 以及编辑器「写临时文件再改名」的保存流程下会静默漏事件，
/// 而这里全量重建只需几百毫秒，不值得为省这点成本引入一个会失效的机制。
/// </remarks>
public sealed class DocIndex(
    DocSourceLocator locator,
    DocsMcpOptions options,
    TimeProvider timeProvider,
    ILogger<DocIndex> logger)
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _contentCache = new(StringComparer.Ordinal);

    private DateTimeOffset _lastCheck = DateTimeOffset.MinValue;
    private string _signature = string.Empty;

    /// <summary>
    /// 当前索引快照
    /// </summary>
    public IndexSnapshot Current { get; private set; } = new([], new BigramIndex(), []);

    /// <summary>
    /// 确保索引是最新的，必要时重建
    /// </summary>
    /// <returns>本次调用看到的索引快照</returns>
    /// <remarks>
    /// 调用方必须只用这个返回值，不要再去读 <see cref="Current"/>：
    /// 重建可以插在任意两次读取之间，理由见 <see cref="IndexSnapshot"/> 的说明。
    /// </remarks>
    public IndexSnapshot EnsureFresh()
    {
        lock (_gate)
        {
            var now = timeProvider.GetUtcNow();
            if (Current.Sections.Count > 0 && now - _lastCheck < options.RefreshThrottle)
            {
                return Current;
            }

            _lastCheck = now;

            var files = locator.Enumerate();
            var signature = ComputeSignature(files);
            if (signature == _signature)
            {
                return Current;
            }

            _signature = signature;
            Rebuild(files);

            return Current;
        }
    }

    /// <summary>
    /// 由文件路径与修改时间组成签名，用于判断是否需要重建
    /// </summary>
    private static string ComputeSignature(IReadOnlyList<DocFile> files)
    {
        var builder = new StringBuilder();
        foreach (var file in files)
        {
            builder.Append(file.RelativePath).Append('#').Append(file.LastWriteUtc.Ticks).Append(';');
        }

        return builder.ToString();
    }

    /// <summary>
    /// 全量重建章节集合与倒排索引
    /// </summary>
    private void Rebuild(IReadOnlyList<DocFile> files)
    {
        var sections = new List<DocSection>();
        var index = new BigramIndex();

        foreach (var file in files)
        {
            var content = ReadContent(file);
            if (content is null)
            {
                continue;
            }

            sections.AddRange(MarkdownSectionSplitter.Split(file.RelativePath, file.Source, content));
        }

        for (var i = 0; i < sections.Count; i++)
        {
            index.Add(i, sections[i].TitlePath, sections[i].Content);
        }

        // 三者一次性整体换掉：中途被读到的只会是上一份完全自洽的快照，不会是半新半旧的组合
        Current = new IndexSnapshot(sections, index, files);

        logger.LogInformation("文档索引已重建：{FileCount} 个文件，{SectionCount} 个章节。", files.Count, sections.Count);
    }

    /// <summary>
    /// 读取文件内容，读取失败时沿用缓存中的旧内容，不让整次重建失败
    /// </summary>
    private string? ReadContent(DocFile file)
    {
        try
        {
            var content = File.ReadAllText(file.AbsolutePath);
            _contentCache[file.RelativePath] = content;
            return content;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (_contentCache.TryGetValue(file.RelativePath, out var cached))
            {
                logger.LogWarning(ex, "读取 {Path} 失败，沿用上一次的内容。", file.RelativePath);
                return cached;
            }

            logger.LogWarning(ex, "读取 {Path} 失败且无缓存，本次跳过该文件。", file.RelativePath);
            return null;
        }
    }
}
