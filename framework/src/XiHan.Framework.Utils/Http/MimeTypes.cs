#region <<版权版本注释>>

// ----------------------------------------------------------------
// Copyright ©2024 ZhaiFanhua All Rights Reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.
// FileName:MimeTypes
// Guid:b9c6842b-fd5e-42a8-afa9-cfd35c1a82a8
// Author:zhaifanhua
// Email:me@zhaifanhua.com
// CreateTime:2024/4/19 0:55:49
// ----------------------------------------------------------------

#endregion <<版权版本注释>>

namespace XiHan.Framework.Utils.Http;

/// <summary>
/// 定义了一组常见的 MIME 类型，用于表示不同类型的文件和数据流。
/// MIME 类型用于在 HTTP 协议中描述文件的格式，以便接收端正确处理。
/// </summary>
public static class MimeTypes
{
    /// <summary>
    /// 应用程序类型的MIME类，包含用于标识不同格式应用程序数据文件的MIME类型。
    /// 这些类型通常用于标识可通过应用程序处理的数据文件，如文档、脚本、档案等。
    /// </summary>
    public static class Application
    {
        /// <summary>
        /// 表示Atom聚合格式的XML MIME类型，Atom是用于Web feeds的一种格式。
        /// </summary>
        public const string AtomXml = "application/atom+xml";

        /// <summary>
        /// 表示Atom Publishing Protocol的XML MIME类型，用于描述Atom服务的集合。
        /// </summary>
        public const string AtomcatXml = "application/atomcat+xml";

        /// <summary>
        /// 表示ECMAScript的MIME类型，通常用于JavaScript脚本文件。
        /// </summary>
        public const string Ecmascript = "application/ecmascript";

        /// <summary>
        /// 表示Java档案（JAR）文件的MIME类型，JAR文件是一个打包的Java应用程序或库。
        /// </summary>
        public const string JavaArchive = "application/java-archive";

        /// <summary>
        /// 表示JavaScript的MIME类型，通常用于网页中的脚本文件。
        /// </summary>
        public const string Javascript = "application/javascript";

        /// <summary>
        /// 表示JSON数据格式的MIME类型，JSON是一种轻量级的数据交换格式。
        /// </summary>
        public const string Json = "application/json";

        /// <summary>
        /// 表示MPEG-4多媒体文件的MIME类型，MP4是一种容器格式，可以包含音频、视频或两者。
        /// </summary>
        public const string Mp4 = "application/mp4";

        /// <summary>
        /// 表示任意二进制数据流的MIME类型，常用于二进制文件传输。
        /// </summary>
        public const string OctetStream = "application/octet-stream";

        /// <summary>
        /// 表示PDF文档的MIME类型，PDF是一种广泛使用的文档格式。
        /// </summary>
        public const string Pdf = "application/pdf";

        /// <summary>
        /// 表示PKCS#10证书签名请求的MIME类型，PKCS#10是一种证书请求格式。
        /// </summary>
        public const string Pkcs10 = "application/pkcs10";

        /// <summary>
        /// 表示PKCS#7 MIME类型的MIME类型，PKCS#7用于数据签名和加密。
        /// </summary>
        public const string Pkcs7Mime = "application/pkcs7-mime";

        /// <summary>
        /// 表示PKCS#7签名的MIME类型，PKCS#7是一种加密标准，用于创建数字签名。
        /// </summary>
        public const string Pkcs7Signature = "application/pkcs7-signature";

        /// <summary>
        /// 表示PKCS#8私钥信息的MIME类型，PKCS#8是一种存储私钥的格式。
        /// </summary>
        public const string Pkcs8 = "application/pkcs8";

        /// <summary>
        /// 表示PostScript文档的MIME类型，PostScript是一种页面描述语言，用于打印机和成像设备。
        /// </summary>
        public const string Postscript = "application/postscript";

        /// <summary>
        /// 表示资源描述（RDF）数据的MIME类型，RDF是一种用于描述网络资源的模型。
        /// </summary>
        public const string RdfXml = "application/rdf+xml";

        /// <summary>
        /// 表示RSS Web feed的MIME类型，RSS是一种常用的Web feed格式。
        /// </summary>
        public const string RssXml = "application/rss+xml";

        /// <summary>
        /// 表示富文本格式（RTF）文档的MIME类型，RTF是一种文档格式，支持基本文档格式化。
        /// </summary>
        public const string Rtf = "application/rtf";

        /// <summary>
        /// 表示同步多媒体集成语言（SMIL）文档的MIME类型，SMIL用于描述多媒体演示。
        /// </summary>
        public const string SmilXml = "application/smil+xml";

        /// <summary>
        /// 表示OpenType字体的MIME类型，OpenType是一种字体格式，广泛用于现代操作系统和Web。
        /// </summary>
        public const string XFontOtf = "application/x-font-otf";

        /// <summary>
        /// 表示TrueType字体的MIME类型，TrueType是一种常用的字体格式，支持大多数操作系统和Web浏览器。
        /// </summary>
        public const string XFontTtf = "application/x-font-ttf";

        /// <summary>
        /// 表示Web Open Font Format文档的MIME类型，WOFF是一种专为Web设计的字体文件格式。
        /// </summary>
        public const string XFontWoff = "application/x-font-woff";

        /// <summary>
        /// 表示PKCS#12个人证书包的MIME类型，PKCS#12是一种证书和私钥的打包格式。
        /// </summary>
        public const string XPkcs12 = "application/x-pkcs12";

        /// <summary>
        /// 表示Shockwave Flash文件的MIME类型，Flash是一种多媒体和Rich Internet Application格式。
        /// </summary>
        public const string XShockwaveFlash = "application/x-shockwave-flash";

        /// <summary>
        /// 表示Microsoft Silverlight应用程序的MIME类型，Silverlight是一种Web应用程序。
        /// </summary>
        public const string XSilverlightApp = "application/x-silverlight-app";

        /// <summary>
        /// 表示XHTML文档的MIME类型，XHTML是一种用于标记网页的结构的标记语言。
        /// </summary>
        public const string XhtmlXml = "application/xhtml+xml";

        /// <summary>
        /// 表示可扩展方法标记语言（XML）文档的MIME类型，XML是一种用于存储和传输数据的格式。
        /// </summary>
        public const string Xml = "application/xml";

        /// <summary>
        /// 表示XML数据类型定义（DTD）的MIME类型，DTD是定义XML文档结构的文档类型。
        /// </summary>
        public const string XmlDtd = "application/xml-dtd";

        /// <summary>
        /// 表示可扩展方法样式表语言转换（XSLT）文档的MIME类型，XSLT用于将XML文档转换成HTML、文本或其他XML文档。
        /// </summary>
        public const string XsltXml = "application/xslt+xml";

        /// <summary>
        /// 表示ZIP压缩文件的MIME类型，ZIP是一种流行的压缩文件格式。
        /// </summary>
        public const string Zip = "application/zip";
    }

    /// <summary>
    /// 音频类型的MIME类，包含用于标识不同格式音频文件的MIME类型。
    /// 这些类型用于在网络传输和文件交换中标识音频文件的格式，以确保音频能够被正确地识别和播放。
    /// </summary>
    public static class Audio
    {
        /// <summary>
        /// 表示音乐器数字接口（MIDI）文件的MIME类型，MIDI是一种广泛使用的乐器和音乐软件的数字协议。
        /// </summary>
        public const string Midi = "audio/midi";

        /// <summary>
        /// 表示MPEG-4音频文件的MIME类型，MP4是一种容器格式，可以包含音频和视频数据。
        /// </summary>
        public const string Mp4 = "audio/mp4";

        /// <summary>
        /// 表示MPEG音频文件的MIME类型，MPEG是一种音频压缩标准，常用于音乐和视频文件。
        /// </summary>
        public const string Mpeg = "audio/mpeg";

        /// <summary>
        /// 表示Ogg音频文件的MIME类型，Ogg是一种开源的多媒体容器格式，常用于流媒体和开源项目。
        /// </summary>
        public const string Ogg = "audio/ogg";

        /// <summary>
        /// 表示WebM音频文件的MIME类型，WebM是一种开源的多媒体容器格式，支持音频和视频。
        /// </summary>
        public const string Webm = "audio/webm";

        /// <summary>
        /// 表示高级音频编码（AAC）文件的MIME类型，AAC是一种广泛使用的音频压缩标准，提供比MP3更好的音质。
        /// </summary>
        public const string XAac = "audio/x-aac";

        /// <summary>
        /// 表示音频交换文件格式（AIFF）文件的MIME类型，AIFF是一种无损音频文件格式，常用于专业音频制作。
        /// </summary>
        public const string XAiff = "audio/x-aiff";

        /// <summary>
        /// 表示MPEG URL文件的MIME类型，通常用于指向MPEG音频流的URL。
        /// </summary>
        public const string XMpegurl = "audio/x-mpegurl";

        /// <summary>
        /// 表示Windows Media Audio（WMA）文件的MIME类型，WMA是微软开发的一种音频压缩格式。
        /// </summary>
        public const string XMsWma = "audio/x-ms-wma";

        /// <summary>
        /// 表示波形音频文件（WAV）的MIME类型，WAV是一种无损音频文件格式，广泛用于保存未压缩的音频。
        /// </summary>
        public const string XWav = "audio/x-wav";
    }

    /// <summary>
    /// 图像类型的MIME类，包含用于标识不同格式图像文件的MIME类型。
    /// 这些类型用于在网络传输和文件交换中标识图像文件的格式，以确保图像能够被正确地识别和展示。
    /// </summary>
    public static class Image
    {
        /// <summary>
        /// 表示Windows位图（BMP）图像文件的MIME类型，BMP是一种未压缩的位图图像文件格式。
        /// </summary>
        public const string Bmp = "image/bmp";

        /// <summary>
        /// 表示图形交换格式（GIF）图像文件的MIME类型，GIF支持动画和透明背景。
        /// </summary>
        public const string Gif = "image/gif";

        /// <summary>
        /// 表示联合图像专家组（JPEG）图像文件的MIME类型，JPEG是一种常用的有损压缩图像格式，适合用于照片。
        /// </summary>
        public const string Jpeg = "image/jpeg";

        /// <summary>
        /// 表示便携式网络图形（PNG）图像文件的MIME类型，PNG支持无损压缩和透明背景。
        /// </summary>
        public const string Png = "image/png";

        /// <summary>
        /// 表示可缩放矢量图形（SVG）图像文件的MIME类型，SVG是一种基于XML的矢量图像格式。
        /// </summary>
        public const string SvgXml = "image/svg+xml";

        /// <summary>
        /// 表示标记图像文件格式（TIFF）图像文件的MIME类型，TIFF是一种灵活的、适用于打印的位图图像格式。
        /// </summary>
        public const string Tiff = "image/tiff";

        /// <summary>
        /// 表示WebP图像文件的MIME类型，WebP是由Google开发的一种支持无损和有损压缩的现代图像格式。
        /// </summary>
        public const string Webp = "image/webp";
    }

    /// <summary>
    /// 文本类型的MIME类，包含用于标识文本文件的MIME类型。
    /// 这些类型通常用于标识纯文本数据，它们可以被大多数文本编辑器和Web浏览器正确地渲染和处理。
    /// </summary>
    public static class Text
    {
        /// <summary>
        /// 表示层叠样式表（CSS）文件的MIME类型，CSS用于定义Web页面的布局和视觉样式。
        /// </summary>
        public const string Css = "text/css";

        /// <summary>
        /// 表示逗号分隔值（CSV）文件的MIME类型，CSV是一种常见的表格数据表示格式，广泛应用于电子表格和数据分析。
        /// </summary>
        public const string Csv = "text/csv";

        /// <summary>
        /// 表示HTML文档的MIME类型，HTML是构建Web页面的标准标记语言。
        /// </summary>
        public const string Html = "text/html";

        /// <summary>
        /// 表示无格式的纯文本文件的MIME类型，通常用于通用文本数据的交换。
        /// </summary>
        public const string Plain = "text/plain";

        /// <summary>
        /// 表示富文本（Rich Text）格式的MIME类型，富文本是一种包含格式化信息的文本格式，但不包含HTML标签。
        /// </summary>
        public const string RichText = "text/richtext";

        /// <summary>
        /// 表示标准通用标记语言（SGML）的MIME类型，SGML是一种文档结构的国际标准，HTML是SGML的一个子集。
        /// </summary>
        public const string Sgml = "text/sgml";

        /// <summary>
        /// 表示YAML数据序列化格式的MIME类型，YAML是一种用于数据序列化的格式，常用于配置文件和数据交换。
        /// </summary>
        public const string Yaml = "text/yaml";
    }

    /// <summary>
    /// 视频类型的MIME类，包含用于标识视频文件的MIME类型。
    /// 这些类型用于在网络传输中标识视频文件的格式，以便接收端能够正确地处理和显示视频内容。
    /// </summary>
    public static class Video
    {
        /// <summary>
        /// 表示3GPP视频文件的MIME类型，3GPP是一种广泛使用的视频文件格式，特别适用于移动设备。
        /// 通常用于通过手机网络传输的视频内容。
        /// </summary>
        public const string Threegpp = "video/3gpp";

        /// <summary>
        /// 表示H.264视频编码格式的MIME类型，H.264是一种高效的视频压缩标准，广泛应用于视频流和视频文件中，包括高清视频和蓝光光盘。
        /// </summary>
        public const string H264 = "video/h264";

        /// <summary>
        /// 表示MPEG-4 Part 14视频文件的MIME类型，通常以.mp4扩展方法名出现，是一种非常流行的视频文件格式，支持流媒体和视频播放设备。
        /// </summary>
        public const string Mp4 = "video/mp4";

        /// <summary>
        /// 表示MPEG视频文件的MIME类型，MPEG是一种早期的视频压缩标准，用于DVD视频和旧式的在线视频，以.mpeg或.mpg扩展方法名常见。
        /// </summary>
        public const string Mpeg = "video/mpeg";

        /// <summary>
        /// 表示Ogg容器格式的视频文件的MIME类型，Ogg是一个开源的多媒体容器格式，常用于流媒体和开源项目，支持多声道音频和多种编码格式。
        /// </summary>
        public const string Ogg = "video/ogg";

        /// <summary>
        /// 表示QuickTime文件格式的MIME类型，QuickTime是Apple公司开发的一种视频和音频文件格式，支持多种视频编码和高质量的音频，广泛用于视频编辑和播放。
        /// </summary>
        public const string Quicktime = "video/quicktime";

        /// <summary>
        /// 表示WebM视频文件的MIME类型，WebM是一个开源的视频文件格式，由WebM项目开发，旨在提供高质量的视频压缩，并且完全免费使用。
        /// </summary>
        public const string Webm = "video/webm";
    }
}
