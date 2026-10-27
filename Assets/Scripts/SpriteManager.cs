using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MinorShift.Emuera.Content;
using uEmuera.Drawing;
using WebP;

internal static class SpriteManager
{
    static float kPastTime = 300.0f;
    static int mainThreadId;
    static Queue<Action> mainThreadActions = new Queue<Action>();
    static object mainThreadLock = new object();

    // 逐图解码日志在 eraTW 这种万级资源下会灌满 logcat，既占内存又拖慢加载。
    // 只在需要排查时手动改成 true。
    static bool debug = false;

    static void DebugLog(string msg)
    {
        if(!debug)
            return;
        int tid = System.Threading.Thread.CurrentThread.ManagedThreadId;
        string marker = (tid == mainThreadId) ? "MAIN" : "WORKER";
        Debug.Log($"[SpriteMgr][{marker}:{tid}] {msg}");
    }

    internal class SpriteInfo : IDisposable
    {
        internal SpriteInfo(TextureInfo p, Sprite s)
        {
            parent = p;
            sprite = s;
        }
        public void Dispose()
        {
            DebugLog($"SpriteInfo.Dispose: name={parent.imagename}");
            UnityEngine.Object.Destroy(sprite);
            sprite = null;
        }
        internal Sprite sprite;
        internal TextureInfo parent;
    }
    internal class TextureInfo : IDisposable
    {
        // 纹理空闲多久就丢掉 CPU 侧的像素副本（秒）。纹理本身留着还能继续画。
        const float kPixelCacheIdle = 20.0f;

        internal TextureInfo(string b, Texture2D tex)
        {
            imagename = b;
            texture = tex;
            _w = tex.width;
            _h = tex.height;
            pasttime = Time.unscaledTime + kPastTime;
            // 这里不再做 GetPixels32()。
            // v24 的 ConstImage 只持有一份 Bitmap，绘制时直接从这一份取像素；
            // 本实现原先额外常驻一份 Color32[]，等于把整包资源复制两份，
            // 万级资源（eraTW）直接翻倍。改为按需构建，见 EnsurePixelCache()。
        }
        internal SpriteInfo GetSprite(ASprite src)
        {
            SpriteInfo sprite = null;
            if(!sprites.TryGetValue(src.Name, out sprite))
            {
                DebugLog($"GetSprite: Sprite.Create name={src.Name} rect={src.Rectangle}");
                sprite = new SpriteInfo(this, 
                    Sprite.Create(texture,
                        GenericUtils.ToUnityRect(src.Rectangle, _w, _h),
                        Vector2.zero)
                    );
                sprites[src.Name] = sprite;
            }
            if(sprite != null)
                refcount += 1;
            return sprite;
        }
        internal void Release()
        {
            refcount -= 1;
            pasttime = Time.unscaledTime + kPastTime;
        }
        public void Dispose()
        {
            DebugLog($"TextureInfo.Dispose: name={imagename}");
            var iter = sprites.Values.GetEnumerator();
            while(iter.MoveNext())
            {
                iter.Current.Dispose();
            }
            sprites.Clear();
            sprites = null;

            DebugLog($"TextureInfo.Dispose: Object.Destroy texture");
            UnityEngine.Object.Destroy(texture);
            texture = null;

            pixelCache = null;
            pixelCacheDirty = false;
        }
        internal string imagename = null;
        internal int refcount = 0;
        internal float pasttime = 0;
        internal int _w;
        internal int _h;
        internal int width { get { return _w; } }
        internal int height { get { return _h; } }
        internal Texture2D texture = null;
        Dictionary<string, SpriteInfo> sprites = new Dictionary<string, SpriteInfo>();

        internal UnityEngine.Color32[] pixelCache = null;
        internal bool pixelCacheDirty = false;
        float pixelCachePasttime = 0f;

        internal bool HasPixelCache { get { return pixelCache != null; } }
        internal bool PixelCacheDirty { get { return pixelCacheDirty; } }
        internal bool PixelCacheExpired(float now)
        {
            return pixelCache != null && now > pixelCachePasttime;
        }

        /// <summary>
        /// 按需构建 CPU 侧像素副本。首次访问时才 GetPixels32()。
        /// Emuera 的绘图指令（GDRAWCIMG 等）跑在后台线程，而 GetPixels32 只能主线程调用，
        /// 所以非主线程时切到主线程执行（与 GetTextureInfo 同一套处理方式）。
        /// </summary>
        internal UnityEngine.Color32[] EnsurePixelCache()
        {
            if (pixelCache != null)
                return pixelCache;
            if (texture == null)
                return null;
            if (!SpriteManager.IsMainThread())
            {
                UnityEngine.Color32[] result = null;
                var done = new System.Threading.ManualResetEvent(false);
                SpriteManager.RunOnMainThread(() =>
                {
                    result = EnsurePixelCache();
                    done.Set();
                });
                done.WaitOne();
                done.Close();
                return result;
            }
            pixelCache = texture.GetPixels32();
            pixelCachePasttime = Time.unscaledTime + kPixelCacheIdle;
            return pixelCache;
        }

        internal UnityEngine.Color32 GetPixelCached(int x, int y)
        {
            var cache = EnsurePixelCache();
            if (cache == null || x < 0 || y < 0 || x >= _w || y >= _h)
                return default(UnityEngine.Color32);
            return cache[y * _w + x];
        }

        internal UnityEngine.Color32[] GetPixelsCached()
        {
            return EnsurePixelCache();
        }

        internal void SetPixelCached(int x, int y, UnityEngine.Color32 c)
        {
            var cache = EnsurePixelCache();
            if (cache == null || x < 0 || y < 0 || x >= _w || y >= _h)
                return;
            cache[y * _w + x] = c;
            pixelCacheDirty = true;
        }

        internal void ApplyPixelCache()
        {
            if (pixelCacheDirty && pixelCache != null)
            {
                texture.SetPixels32(pixelCache);
                texture.Apply();
                pixelCacheDirty = false;
            }
        }

        internal void ReleasePixelCache()
        {
            if (pixelCacheDirty)
                return;
            pixelCache = null;
        }
    }
    class CallbackInfo
    {
        public CallbackInfo(ASprite src, object obj, 
                            Action<object, SpriteInfo> callback)
        {
            this.src = src;
            this.obj = obj;
            this.callback = callback;
        }
        public void DoCallback(SpriteInfo info)
        {
            callback(obj, info);
        }
        public ASprite src;
        object obj;
        Action<object, SpriteInfo> callback;
    }

    public static void Init()
    {
        mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
        DebugLog($"Init: mainThreadId={mainThreadId}");
        GenericUtils.StartCoroutine(ProcessMainThreadActions());
#if UNITY_EDITOR
        kPastTime = 120.0f;
        GenericUtils.StartCoroutine(Update());
        GenericUtils.StartCoroutine(UpdateRenderOP());
#else
        // 纹理驻留时间：设备内存越小，回收越激进。
        // 原先 <=4096/<=8192/>8192 三档给的是 120/300/600 秒，等于加载后
        // 好几分钟一张都不回收，峰值必须扛住全量资源。手机上按 60/120/300 秒走。
        var memorysize = SystemInfo.systemMemorySize;
        if(memorysize <= 6144)
            kPastTime = 60.0f;
        else if(memorysize <= 8192)
            kPastTime = 120.0f;
        else
            kPastTime = 300.0f;
        GenericUtils.StartCoroutine(Update());
        GenericUtils.StartCoroutine(UpdateRenderOP());
#endif
    }
    static IEnumerator ProcessMainThreadActions()
    {
        while(true)
        {
            yield return null;
            lock(mainThreadLock)
            {
                if(mainThreadActions.Count > 0)
                {
                    DebugLog($"ProcessMainThreadActions: processing {mainThreadActions.Count} actions");
                }
                while(mainThreadActions.Count > 0)
                {
                    var action = mainThreadActions.Dequeue();
                    DebugLog($"ProcessMainThreadActions: executing queued action");
                    action();
                }
            }
        }
    }

    public static void RunOnMainThread(Action action)
    {
        lock(mainThreadLock)
        {
            mainThreadActions.Enqueue(action);
            DebugLog($"RunOnMainThread: queued action, queue size={mainThreadActions.Count}");
        }
    }

    public static bool IsMainThread()
    {
        return System.Threading.Thread.CurrentThread.ManagedThreadId == mainThreadId;
    }

    public static void GetSprite(ASprite src, 
                                object obj, Action<object, SpriteInfo> callback)
    {
        if(src == null || src.Bitmap == null)
        {
            if(callback != null)
                callback(null, null);
            return;
        }

        var basename = src.Bitmap.filename;
        TextureInfo ti = null;
        texture_dict.TryGetValue(basename, out ti);
        if(ti == null)
        {
            var pixelBmp = src.Bitmap as BitmapPixelBuffer;
            if (pixelBmp != null && pixelBmp.tex != null)
            {
                DebugLog($"GetSprite: BitmapPixelBuffer direct register name={basename}");
                RegisterTexture(basename, pixelBmp.tex);
                texture_dict.TryGetValue(basename, out ti);
            }

            if (ti == null)
            {
                DebugLog($"GetSprite: MISS dict, starting Loading for {basename}");
                var item = new CallbackInfo(src, obj, callback);
                List<CallbackInfo> list = null;
                if(loading_set.TryGetValue(basename, out list))
                    list.Add(item);
                else
                {
                    list = new List<CallbackInfo> { item };
                    loading_set.Add(basename, list);
                    GenericUtils.StartCoroutine(Loading(src.Bitmap));
                }
                return;
            }
        }

        DebugLog($"GetSprite: HIT dict for {basename}");
        callback(obj, GetSpriteInfo(ti, src));
    }

    static Texture2D MakeReadable(Texture2D source)
    {
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        UnityEngine.Graphics.Blit(source, rt);
        var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        readable.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        UnityEngine.Object.Destroy(source);
        return readable;
    }

    public static Color32[] DecodeWebPToPixels(byte[] webpData, out int width, out int height)
    {
        width = 0;
        height = 0;
        if(webpData == null || webpData.Length == 0)
            return null;
        try
        {
            Texture2DExt.GetWebPDimensions(webpData, out int w, out int h);
            if(w <= 0 || h <= 0)
            {
                Debug.LogWarning($"[SpriteMgr] DecodeWebPToPixels: invalid dimensions w={w} h={h}");
                return null;
            }
            byte[] rawData = Texture2DExt.LoadRGBAFromWebP(webpData, ref w, ref h, false, out Error err);
            if(err != Error.Success || rawData == null)
            {
                Debug.LogWarning($"[SpriteMgr] DecodeWebPToPixels: LoadRGBA failed err={err}");
                return null;
            }
            width = w;
            height = h;
            Color32[] pixels = new Color32[w * h];
            for(int i = 0; i < pixels.Length; ++i)
            {
                int off = i * 4;
                pixels[i] = new Color32(rawData[off], rawData[off + 1], rawData[off + 2], rawData[off + 3]);
            }
            return pixels;
        }
        catch(Exception e)
        {
            Debug.LogError($"[SpriteMgr] DecodeWebPToPixels exception: {e.Message}");
            return null;
        }
    }

    public static TextureInfo GetTextureInfo(string name, string filename)
    {
        var key = GenericUtils.GetFilename(filename);
        if (string.IsNullOrEmpty(key))
            key = name;
        TextureInfo ti = null;
        if(texture_dict.TryGetValue(key, out ti))
        {
            DebugLog($"GetTextureInfo: hit cache key={key}");
            return ti;
        }
        if(string.IsNullOrEmpty(filename))
            return null;

        DebugLog($"GetTextureInfo: MISS key={key} file={filename}");

        if(!IsMainThread())
        {
            DebugLog($"GetTextureInfo: on WORKER thread, dispatching to main thread...");
            TextureInfo result = null;
            var readyEvent = new System.Threading.ManualResetEvent(false);
            RunOnMainThread(() =>
            {
                DebugLog($"GetTextureInfo: callback on main thread, calling GetTextureInfoInternal");
                result = GetTextureInfoInternal(key, filename);
                readyEvent.Set();
            });
            readyEvent.WaitOne();
            readyEvent.Close();
            DebugLog($"GetTextureInfo: worker resumed, result={result != null}");
            return result;
        }

        DebugLog($"GetTextureInfo: on MAIN thread, calling GetTextureInfoInternal directly");
        return GetTextureInfoInternal(key, filename);
    }

    static TextureInfo GetTextureInfoInternal(string name, string filename)
    {
        TextureInfo ti = null;
        if(texture_dict.TryGetValue(name, out ti))
            return ti;

        FileInfo fi = new FileInfo(filename);
        if(!fi.Exists)
            return null;

        byte[] content;
        using (FileStream fs = fi.OpenRead())
        {
            var filesize = fs.Length;
            content = new byte[filesize];
            fs.Read(content, 0, (int)filesize);
        }

        // LoadImage 会把纹理重建为图片的实际尺寸；若沿用 DXT 压缩格式，
        // 宽高不是 4 的倍数时 Unity 会报 "requires a texture size that is a multiple of 4"。
        // 这里固定 RGBA32（Android 真机原本也走 RGBA32，设备行为不变）。
        const TextureFormat format = TextureFormat.RGBA32;

        var extname = uEmuera.Utils.GetSuffix(filename).ToLower();
        DebugLog($"GetTextureInfoInternal: name={name} ext={extname}");

        if (extname == "webp")
        {
            DebugLog($"GetTextureInfoInternal: webp file={filename} bytes={content.Length}");
            var webpTex = Texture2DExt.CreateTexture2DFromWebP(content, false, false, out Error lError);
            if(webpTex == null || lError != Error.Success)
            {
                Debug.LogError($"[SpriteMgr] {filename} webp decode FAILED err={lError}");
                return null;
            }
            DebugLog($"GetTextureInfoInternal: webp OK {webpTex.width}x{webpTex.height} fmt={webpTex.format}");
            var tex = MakeReadable(webpTex);
            DebugLog($"GetTextureInfoInternal: readable copy {tex.width}x{tex.height}, creating TextureInfo");
            ti = new TextureInfo(name, tex);
            texture_dict.Add(name, ti);
        }
        else
        {
            DebugLog($"GetTextureInfoInternal: other format={format} file={filename}");
            var tex = new Texture2D(4, 4, format, false);
            if (tex.LoadImage(content))
            {
                DebugLog($"GetTextureInfoInternal: LoadImage ok, creating TextureInfo");
                ti = new TextureInfo(name, tex);
                texture_dict.Add(name, ti);
            }
            else
            {
                Debug.LogWarning($"GetTextureInfoInternal: LoadImage FAILED file={filename}");
            }
        }
        return ti;
    }

    public static TextureInfoOtherThread GetTextureInfoOtherThread(
        string name, string path, Action<TextureInfo> callback)
    {
        var ti = new TextureInfoOtherThread
        {
            name = name,
            path = path,
            callback = callback,
            readyEvent = new System.Threading.ManualResetEvent(false),
        };
        texture_other_threads.Add(ti);
        return ti;
    }
    public class TextureInfoOtherThread
    {
        public string name;
        public string path;
        public Action<TextureInfo> callback;
        public System.Threading.ManualResetEvent readyEvent;
    }
    static List<TextureInfoOtherThread> texture_other_threads = new List<TextureInfoOtherThread>();

    public static RenderTextureOtherThread GetRenderTextureOtherThread(int x, int y, Action<RenderTexture> callback)
    {
        var ti = new RenderTextureOtherThread
        {
            x = x,
            y = y,
            callback = callback,
            mutex = null,
        };
        render_texture_other_threads.Add(ti);
        return ti;
    }
    public class RenderTextureOtherThread
    {
        public int x;
        public int y;
        public Action<RenderTexture> callback;
        public System.Threading.Mutex mutex;
    }
    static List<RenderTextureOtherThread> render_texture_other_threads = new List<RenderTextureOtherThread>();

    ///public static RenderTextureDoSomething RenderTexture
    ///

    public class RenderTextureDoSomething
    {
        public enum Code
        {
            kClear,
            kDrawRectangle,
            kFillRectangle,
            kDrawCImg,
            kDrawG,
            kDrawGWithMask,
            kSetColor,
            kGetColor,
        }
        //Todo: 实现对于方法
    }

    static IEnumerator Loading(Bitmap baseimage)
    {
        DebugLog($"Loading: start file={baseimage.path}");
        TextureInfo ti = null;
        FileInfo fi = new FileInfo(baseimage.path);
        if(fi.Exists)
        {
            byte[] content;
            using (FileStream fs = fi.OpenRead())
            {
                var filesize = fs.Length;
                content = new byte[filesize];

                var async = fs.BeginRead(content, 0, (int)filesize, null, null);
                while(!async.IsCompleted)
                    yield return null;
            }

            const TextureFormat format = TextureFormat.RGBA32;

            var extname = uEmuera.Utils.GetSuffix(baseimage.path).ToLower();
            DebugLog($"Loading: name={baseimage.filename} ext={extname}");

            if (extname == "webp")
            {
                DebugLog($"Loading: webp file={baseimage.path} bytes={content.Length}");
                var webpTex = Texture2DExt.CreateTexture2DFromWebP(content, false, false, out Error lError);
                if(webpTex == null || lError != Error.Success)
                {
                    Debug.LogError($"[SpriteMgr] {baseimage.path} webp decode FAILED err={lError}");
                    yield break;
                }
                DebugLog($"Loading: webp OK {webpTex.width}x{webpTex.height} fmt={webpTex.format}");
                var tex = MakeReadable(webpTex);
                DebugLog($"Loading: readable copy {tex.width}x{tex.height}, creating TextureInfo");
                ti = new TextureInfo(baseimage.filename, tex);
                texture_dict.Add(baseimage.filename, ti);

                baseimage.size.Width = tex.width;
                baseimage.size.Height = tex.height;
            }
            else
            {
                DebugLog($"Loading: new Texture2D format={format} file={baseimage.path}");
                var tex = new Texture2D(4, 4, format, false);
                if (tex.LoadImage(content))
                {
                    DebugLog($"Loading: LoadImage ok, creating TextureInfo");
                    ti = new TextureInfo(baseimage.filename, tex);
                    texture_dict.Add(baseimage.filename, ti);

                    baseimage.size.Width = tex.width;
                    baseimage.size.Height = tex.height;
                }
                else
                {
                    Debug.LogWarning($"Loading: LoadImage FAILED file={baseimage.path}");
                }
            }
        }
        else
        {
            Debug.LogWarning($"Loading: file not found {baseimage.path}");
        }
        List<CallbackInfo> list = null;
        if(loading_set.TryGetValue(baseimage.filename, out list))
        {
            DebugLog($"Loading: invoking {list.Count} callbacks");
            var count = list.Count;
            CallbackInfo item = null;
            for(int i=0; i<count; ++i)
            {
                  item = list[i];
                if (ti != null)
                    item.DoCallback(GetSpriteInfo(ti, item.src));
                else
                    item.DoCallback(null);
            }
            list.Clear();
            loading_set.Remove(baseimage.filename);
        }
    }
    static SpriteInfo GetSpriteInfo(TextureInfo textinfo, ASprite src)
    {
        return textinfo.GetSprite(src);
    }
    internal static void GivebackSpriteInfo(SpriteInfo info)
    {
        if(info == null)
            return;
        info.parent.Release();
    }
    static IEnumerator Update()
    {
        float collectInterval = 60.0f;
        float lastCollectTime = 0f;

        while(true)
        {
            do
            {
                yield return new WaitForSeconds(5.0f);
            } while(texture_dict.Count == 0);

            var now = Time.unscaledTime;
            List<TextureInfo> evictList = null;

            var iter = texture_dict.Values.GetEnumerator();
            while(iter.MoveNext())
            {
                var ti = iter.Current;
                // CPU 像素副本比纹理更贵（同样大小再复制一份），空闲一会儿就先丢掉，
                // 纹理留着还能继续画，下次要读像素时再重建。
                if(ti.PixelCacheExpired(now))
                    ti.ReleasePixelCache();
                if(ti.refcount == 0 && now > ti.pasttime)
                {
                    if(evictList == null)
                        evictList = new List<TextureInfo>();
                    evictList.Add(ti);
                }
            }

            if(evictList != null)
            {
                var count = evictList.Count;
                for(int i = 0; i < count; ++i)
                {
                    var tinfo = evictList[i];
                    texture_dict.Remove(tinfo.imagename);
                    tinfo.Dispose();
                }
                evictList.Clear();
            }

            if(evictList != null || (now - lastCollectTime > collectInterval))
            {
                GC.Collect();
                lastCollectTime = now;
            }
        }
    }
    static IEnumerator UpdateRenderOP()
    {
        while(true)
        {
            do
            {
                yield return new WaitForSeconds(15);
            } while(texture_other_threads.Count == 0
                && render_texture_other_threads.Count == 0);

            TextureInfo ti = null;
            if(texture_other_threads.Count > 0)
            {
                TextureInfoOtherThread tiot = null;
                var tiotiter = texture_other_threads.GetEnumerator();
                while(tiotiter.MoveNext())
                {
                    tiot = tiotiter.Current;
                    ti = GetTextureInfo(tiot.name, tiot.path);
                    tiot.callback(ti);
                    tiot.readyEvent.Set();
                }
                texture_other_threads.Clear();
            }
            if(render_texture_other_threads.Count > 0)
            {
                RenderTextureOtherThread rtot = null;
                var rtotiter = render_texture_other_threads.GetEnumerator();
                while(rtotiter.MoveNext())
                {
                    rtot = rtotiter.Current;
                    rtot.mutex = new System.Threading.Mutex(true);
                    //tiot.mutex.WaitOne();
                    var rt = new RenderTexture(rtot.x, rtot.y, 24, RenderTextureFormat.ARGB32);
                    rtot.callback(rt);
                    rtot.mutex.ReleaseMutex();
                }
                render_texture_other_threads.Clear();
            }
        }
    }
    internal static void ForceClear()
    {
        DebugLog($"ForceClear: clearing {texture_dict.Count} textures");
        var iter = texture_dict.Values.GetEnumerator();
        while(iter.MoveNext())
        {
            iter.Current.Dispose();
        }
        texture_dict.Clear();
        GC.Collect();
    }
    internal static void RegisterTexture(string name, Texture2D tex)
    {
        DebugLog($"RegisterTexture: name={name} w={tex.width}h={tex.height}");
        TextureInfo ti = null;
        if (texture_dict.TryGetValue(name, out ti))
        {
            ti.Dispose();
            texture_dict.Remove(name);
        }
        ti = new TextureInfo(name, tex);
        texture_dict.Add(name, ti);
    }
    internal static void SetResourceCSVLine(string filename, string[] lines)
    {
        var cache = string.Join("\n", lines);
        UnityEngine.PlayerPrefs.SetInt(filename + "_fixed", 1);
        UnityEngine.PlayerPrefs.SetString(filename + "_time",
                        File.GetLastWriteTime(filename).ToString());
        UnityEngine.PlayerPrefs.SetString(filename, cache);
    }
    internal static string[] GetResourceCSVLines(string filename)
    {
        if(PlayerPrefs.GetInt(filename + "_fixed", 0) == 0)
            return null;
        var oldwritetime = PlayerPrefs.GetString(filename + "_time", null);
        if(string.IsNullOrEmpty(oldwritetime))
            return null;
        var writetime = File.GetLastWriteTime(filename).ToString();
        if(oldwritetime != writetime)
            return null;
        var cache = UnityEngine.PlayerPrefs.GetString(filename, null);
        if(string.IsNullOrEmpty(cache))
            return null;
        return cache.Split('\n');
    }
    internal static void ClearResourceCSVLines(string filename)
    {
        UnityEngine.PlayerPrefs.SetInt(filename + "_fixed", 0);
        UnityEngine.PlayerPrefs.SetString(filename + "_time", null);
        UnityEngine.PlayerPrefs.SetString(filename, null);
    }
    static Dictionary<string, List<CallbackInfo>> loading_set =
        new Dictionary<string, List<CallbackInfo>>();
    static Dictionary<string, TextureInfo> texture_dict =
        new Dictionary<string, TextureInfo>();
}
