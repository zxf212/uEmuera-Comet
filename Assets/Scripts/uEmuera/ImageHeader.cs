using System;
using System.IO;

namespace uEmuera
{
    /// <summary>
    /// 只读取图片文件头，拿到宽/高，不做任何解码、不分配像素内存。
    ///
    /// 背景：eraTW-KAGEROU 有 2591 张图，解码成 RGBA32 后共 14.1 亿像素
    /// （一份 5.27GB；uEmuera 还会再缓存一份 pixelCache，合计约 10.5GB）。
    /// 启动时全量解码在手机上必然被 lowmemorykiller 杀掉。
    /// 所以 LoadContents 阶段只登记尺寸，像素数据推迟到真正要绘制时再解。
    /// </summary>
    internal static class ImageHeader
    {
        /// <summary>读取文件头并解析尺寸。失败返回 false。</summary>
        internal static bool TryGetSize(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            try
            {
                var ext = Utils.GetSuffix(path).ToLower();
                byte[] head = ReadHead(path, 65536);
                if (head == null || head.Length < 32)
                    return false;
                switch (ext)
                {
                    case "webp": return WebP(head, out width, out height);
                    case "png": return Png(head, out width, out height);
                    case "jpg":
                    case "jpeg": return Jpeg(head, out width, out height);
                    case "bmp": return Bmp(head, out width, out height);
                    case "gif": return Gif(head, out width, out height);
                }
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[ImageHeader] {path} exception: {e.Message}");
            }
            return false;
        }

        static byte[] ReadHead(string path, int maxBytes)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var len = (int)Math.Min(fs.Length, maxBytes);
                var buf = new byte[len];
                int off = 0;
                while (off < len)
                {
                    var r = fs.Read(buf, off, len - off);
                    if (r <= 0)
                        break;
                    off += r;
                }
                return buf;
            }
        }

        static bool Be(byte[] b, int off, string s)
        {
            for (int i = 0; i < s.Length; ++i)
                if (b[off + i] != (byte)s[i])
                    return false;
            return true;
        }

        static ushort U16LE(byte[] b, int off)
        {
            return (ushort)(b[off] | (b[off + 1] << 8));
        }

        static uint U32LE(byte[] b, int off)
        {
            return (uint)(b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | ((uint)b[off + 3] << 24));
        }

        static int Be32(byte[] b, int off)
        {
            return (b[off] << 24) | (b[off + 1] << 16) | (b[off + 2] << 8) | b[off + 3];
        }

        // ---------- WebP ----------
        static bool WebP(byte[] b, out int w, out int h)
        {
            w = 0; h = 0;
            if (b.Length < 32 || !Be(b, 0, "RIFF") || !Be(b, 8, "WEBP"))
                return false;

            if (Be(b, 12, "VP8 "))
            {
                // 12..15 fourcc, 16..19 chunk size, 20..22 frame tag,
                // 23..25 sync code (9D 01 2A), 26..27 width(14bit), 28..29 height(14bit)
                if (b.Length < 30) return false;
                w = U16LE(b, 26) & 0x3FFF;
                h = U16LE(b, 28) & 0x3FFF;
                return w > 0 && h > 0;
            }
            if (Be(b, 12, "VP8L"))
            {
                // 20 = 0x2F signature, 21..24 = 14bit(w-1) + 14bit(h-1)
                if (b.Length < 25) return false;
                var bits = U32LE(b, 21);
                w = (int)(bits & 0x3FFF) + 1;
                h = (int)((bits >> 14) & 0x3FFF) + 1;
                return w > 0 && h > 0;
            }
            if (Be(b, 12, "VP8X"))
            {
                // 20 flags, 21..23 reserved, 24..26 canvas w-1, 27..29 canvas h-1 (24bit LE)
                if (b.Length < 30) return false;
                w = (int)(b[24] | (b[25] << 8) | (b[26] << 16)) + 1;
                h = (int)(b[27] | (b[28] << 8) | (b[29] << 16)) + 1;
                return w > 0 && h > 0;
            }
            return false;
        }

        // ---------- PNG ----------
        static bool Png(byte[] b, out int w, out int h)
        {
            w = 0; h = 0;
            if (b.Length < 24 || b[0] != 0x89 || !Be(b, 1, "PNG")) return false;
            w = Be32(b, 16);
            h = Be32(b, 20);
            return w > 0 && h > 0;
        }

        // ---------- JPEG ----------
        static bool Jpeg(byte[] b, out int w, out int h)
        {
            w = 0; h = 0;
            if (b.Length < 4 || b[0] != 0xFF || b[1] != 0xD8) return false;
            int i = 2;
            int n = b.Length;
            while (i < n - 9)
            {
                if (b[i] != 0xFF) { i++; continue; }
                var marker = b[i + 1];
                if (marker == 0xD8 || marker == 0xD9 || marker == 0x01
                    || (marker >= 0xD0 && marker <= 0xD7))
                {
                    i += 2;
                    continue;
                }
                if (i + 3 >= n) return false;
                int seglen = (b[i + 2] << 8) | b[i + 3];
                if (marker == 0xC0 || marker == 0xC1 || marker == 0xC2 || marker == 0xC3
                    || marker == 0xC5 || marker == 0xC6 || marker == 0xC7
                    || marker == 0xC9 || marker == 0xCA || marker == 0xCB
                    || marker == 0xCD || marker == 0xCE || marker == 0xCF)
                {
                    if (i + 9 >= n) return false;
                    h = (b[i + 5] << 8) | b[i + 6];
                    w = (b[i + 7] << 8) | b[i + 8];
                    return w > 0 && h > 0;
                }
                if (seglen <= 0) return false;
                i += 2 + seglen;
            }
            return false;
        }

        // ---------- BMP ----------
        static bool Bmp(byte[] b, out int w, out int h)
        {
            w = 0; h = 0;
            if (b.Length < 26 || b[0] != (byte)'B' || b[1] != (byte)'M') return false;
            w = Math.Abs((int)U32LE(b, 18));
            h = Math.Abs((int)U32LE(b, 22));
            return w > 0 && h > 0;
        }

        // ---------- GIF ----------
        static bool Gif(byte[] b, out int w, out int h)
        {
            w = 0; h = 0;
            if (b.Length < 10 || !Be(b, 0, "GIF")) return false;
            w = U16LE(b, 6);
            h = U16LE(b, 8);
            return w > 0 && h > 0;
        }
    }
}
