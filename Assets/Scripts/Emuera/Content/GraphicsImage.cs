using MinorShift._Library;
using System;
using System.Collections.Generic;
//using System.Drawing;
//using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using uEmuera.Drawing;
//using System.Threading.Tasks;

namespace MinorShift.Emuera.Content
{
	internal sealed class GraphicsImage : AbstractImage
	{
		public GraphicsImage(int id)
		{
			ID = id;
			texName = string.Concat(":GRAPHICS:", ID);
			lock(allGraphicsLock) { allGraphics.Add(this); }
		}
		public readonly int ID;
		string texName;

		static List<GraphicsImage> allGraphics = new List<GraphicsImage>();
		static object allGraphicsLock = new object();
        //Size size;
        //Brush brush = null;
        //Pen pen = null;
        //Font font = null;
        //Bitmap b;
        //Graphics g;


        ////bool created;
        ////bool locked;
        //public void LockGraphics()
        //{
        //	//if (locked)
        //	//	return;
        //	//g = Graphics.FromImage(b);
        //	//locked = true;
        //}
        //public void UnlockGraphics()
        //{
        //	//if (!locked)
        //	//	return;
        //	//g.Dispose();
        //	//g = null;
        //	//locked = false;
        //}

        #region Bitmap書き込み・作成

        /// <summary>
        /// GCREATE(int ID, int width, int height)
        /// Graphicsの基礎となるBitmapを作成する。エラーチェックは呼び出し元でのみ行う
        /// </summary>
        public void GCreate(int x, int y, bool useGDI)
        {
            is_created = false;
            width = 0;
            height = 0;
            pixelBuffer = null;
            pixelDirty = false;

            is_created = true;
            width = x;
            height = y;
            pixelBuffer = new Color32[x * y];
            // 置脏：v24 里 GCREATE 之后画布就是一张有效的空图；本实现若初始不脏，
            // ApplyPixelBuffer 不会执行 → Bitmap 为 null → SPRITECREATE 解析失败。
            pixelDirty = true;
        }

        public void ApplyPixelBuffer()
        {
            if (pixelBuffer == null || !pixelDirty)
                return;

            if (pixelTexture == null)
            {
                pixelTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            }

            pixelTexture.SetPixels32(pixelBuffer);
            pixelTexture.Apply();

            if (Bitmap == null)
            {
                Bitmap = new BitmapPixelBuffer(texName, pixelBuffer, pixelTexture, width, height);
                SpriteManager.RegisterTexture(texName, pixelTexture);
            }

            pixelDirty = false;
        }

        public static void FlushDirtyGraphics()
        {
            lock(allGraphicsLock)
            {
                for (int i = 0; i < allGraphics.Count; ++i)
                {
                    var g = allGraphics[i];
                    if (g.pixelDirty)
                        g.ApplyPixelBuffer();
                }
            }
        }

        internal void GCreateFromF(Bitmap bmp, bool useGDI)
        {
            this.GDispose();
            this.Bitmap = bmp;
            is_created = true;
            width = bmp.Width;
            height = bmp.Height;
        }

        /// <summary>
        /// GCLEAR(int ID, int cARGB)
        /// エラーチェックは呼び出し元でのみ行う
        /// </summary>
        public void GClear(uEmuera.Drawing.Color c)
        {
            if (pixelBuffer == null)
                return;
            var uc = new Color32((byte)c.R, (byte)c.G, (byte)c.B, (byte)c.A);
            var count = pixelBuffer.Length;
            for (int i = 0; i < count; ++i)
                pixelBuffer[i] = uc;
            pixelDirty = true;
        }

        /// <summary>
        /// GDRAWTEXTGDRAWTEXT int ID, str text, int x, int y
        /// エラーチェックは呼び出し元でのみ行う
        /// </summary>
        //public void GDrawString(string text, int x, int y)
        //{
        //	if (g == null)
        //		throw new NullReferenceException();
        //	Font usingFont = font;
        //	if (usingFont == null)
        //		usingFont = Config.Font;
        //	if (brush != null)
        //	{
        //		g.DrawString(text, usingFont, brush, x, y);
        //	}
        //	else
        //	{
        //		using (SolidBrush b = new SolidBrush(Config.ForeColor))
        //			g.DrawString(text, usingFont, b, x, y);
        //	}
        //}
        /// <summary>
        /// GDRAWTEXTGDRAWTEXT int ID, str text, int x, int y, int width, int height
        /// エラーチェックは呼び出し元でのみ行う
        /// </summary>
        //public void GDrawString(string text, int x, int y, int width, int height)
        //{
        //	if (g == null)
        //		throw new NullReferenceException();
        //	Font usingFont = font;
        //	if (usingFont == null)
        //		usingFont = Config.Font;
        //	if (brush != null)
        //	{
        //		g.DrawString(text, usingFont, brush, new RectangleF(x,y,width,height));
        //	}
        //	else
        //	{
        //		using (SolidBrush b = new SolidBrush(Config.ForeColor))
        //			g.DrawString(text, usingFont, b, new RectangleF(x, y, width, height));
        //	}
        //}

        /// <summary>
        /// GDRAWRECTANGLE(int ID, int x, int y, int width, int height)
        /// エラーチェックは呼び出し元でのみ行う
        /// </summary>
        //public void GDrawRectangle(Rectangle rect)
        //{
        //	if (g == null)
        //		throw new NullReferenceException();
        //	if (pen != null)
        //	{
        //		g.DrawRectangle(pen, rect);
        //	}
        //	else
        //	{
        //		using (Pen p = new Pen(Config.ForeColor))
        //			g.DrawRectangle(p, rect);
        //	}
        //}

        /// <summary>
        /// GFILLRECTANGLE(int ID, int x, int y, int width, int height)
        /// エラーチェックは呼び出し元でのみ行う
        /// </summary>
        public void GFillRectangle(Rectangle rect)
        {
            if (pixelBuffer == null)
                return;
            int rw = width;
            int clipX = Math.Max(0, rect.X);
            int clipY = Math.Max(0, rect.Y);
            int clipR = Math.Min(rw, rect.X + rect.Width);
            int clipB = Math.Min(height, rect.Y + rect.Height);
            if (clipX >= clipR || clipY >= clipB)
                return;
            var uc = new Color32(0, 0, 0, 255);
            for (int y = clipY; y < clipB; ++y)
            {
                int rowStart = y * rw;
                for (int x = clipX; x < clipR; ++x)
                    pixelBuffer[rowStart + x] = uc;
            }
            pixelDirty = true;
        }

		/// <summary>
		/// GDRAWCIMG(int ID, str imgName, int destX, int destY, int destWidth, int destHeight)
		/// エラーチェックは呼び出し元でのみ行う
		/// </summary>
		public void GDrawCImg(ASprite img, Rectangle destRect)
		{
            if (pixelBuffer == null || img == null || !img.IsCreated)
                return;
            var srcBmp = img.Bitmap;
            if (srcBmp == null)
                return;
            var ti = SpriteManager.GetTextureInfo(srcBmp.name, srcBmp.path);
            if (ti == null)
                return;
            var srcColors = ti.GetPixelsCached();
            int srcW = ti.width;
            int srcH = ti.height;
            var srcRect = img.Rectangle;

            int dstW = destRect.Width;
            int dstH = destRect.Height;
            if (dstW <= 0 || dstH <= 0)
                return;

            for (int dy = 0; dy < dstH; ++dy)
            {
                int dstY = destRect.Y + dy;
                if (dstY < 0 || dstY >= height)
                    continue;
                int srcY = srcRect.Y + dy * srcRect.Height / dstH;
                if (srcY < 0 || srcY >= srcH)
                    continue;
                int dstRowStart = dstY * width;
                int srcRowStart = srcY * srcW;

                for (int dx = 0; dx < dstW; ++dx)
                {
                    int dstX = destRect.X + dx;
                    if (dstX < 0 || dstX >= width)
                        continue;
                    int srcX = srcRect.X + dx * srcRect.Width / dstW;
                    if (srcX < 0 || srcX >= srcW)
                        continue;

                    var sc = srcColors[srcRowStart + srcX];
                    if (sc.a == 0)
                        continue;
                    if (sc.a == 255)
                    {
                        pixelBuffer[dstRowStart + dstX] = sc;
                    }
                    else
                    {
                        var dc = pixelBuffer[dstRowStart + dstX];
                        int alpha = sc.a + 1;
                        dc.r = (byte)((sc.r * alpha + dc.r * (256 - alpha)) >> 8);
                        dc.g = (byte)((sc.g * alpha + dc.g * (256 - alpha)) >> 8);
                        dc.b = (byte)((sc.b * alpha + dc.b * (256 - alpha)) >> 8);
                        dc.a = (byte)Math.Min(255, sc.a + (dc.a * (256 - alpha) >> 8));
                        pixelBuffer[dstRowStart + dstX] = dc;
                    }
                }
            }
            pixelDirty = true;
		}

		/// <summary>
		/// GDRAWCIMG(int ID, str imgName, int destX, int destY, int destWidth, int destHeight, float[][] cm)
		/// エラーチェックは呼び出し元でのみ行う
		/// </summary>
		public void GDrawCImg(ASprite img, Rectangle destRect, float[][] cm)
		{
            GDrawCImg(img, destRect);
		}

        /// <summary>
        /// GDRAWG(int ID, int srcID, int destX, int destY, int destWidth, int destHeight, int srcX, int srcY, int srcWidth, int srcHeight)
        /// エラーチェックは呼び出し元でのみ行う
        /// </summary>
        public void GDrawG(GraphicsImage srcGra, Rectangle destRect, Rectangle srcRect)
        {
            if (pixelBuffer == null || srcGra == null || srcGra.pixelBuffer == null)
                return;

            int dstW = destRect.Width;
            int dstH = destRect.Height;
            int srcW = srcGra.width;
            int srcH = srcGra.height;
            var srcBuf = srcGra.pixelBuffer;

            for (int dy = 0; dy < dstH; ++dy)
            {
                int dstY = destRect.Y + dy;
                if (dstY < 0 || dstY >= height)
                    continue;
                int srcY = srcRect.Y + dy * srcRect.Height / dstH;
                if (srcY < 0 || srcY >= srcH)
                    continue;
                int dstRowStart = dstY * width;
                int srcRowStart = srcY * srcW;

                for (int dx = 0; dx < dstW; ++dx)
                {
                    int dstX = destRect.X + dx;
                    if (dstX < 0 || dstX >= width)
                        continue;
                    int srcX = srcRect.X + dx * srcRect.Width / dstW;
                    if (srcX < 0 || srcX >= srcW)
                        continue;

                    var sc = srcBuf[srcRowStart + srcX];
                    if (sc.a == 0)
                        continue;
                    if (sc.a == 255)
                    {
                        pixelBuffer[dstRowStart + dstX] = sc;
                    }
                    else
                    {
                        var dc = pixelBuffer[dstRowStart + dstX];
                        int alpha = sc.a + 1;
                        dc.r = (byte)((sc.r * alpha + dc.r * (256 - alpha)) >> 8);
                        dc.g = (byte)((sc.g * alpha + dc.g * (256 - alpha)) >> 8);
                        dc.b = (byte)((sc.b * alpha + dc.b * (256 - alpha)) >> 8);
                        dc.a = (byte)Math.Min(255, sc.a + (dc.a * (256 - alpha) >> 8));
                        pixelBuffer[dstRowStart + dstX] = dc;
                    }
                }
            }
            pixelDirty = true;
        }


        /// <summary>
        /// GDRAWG(int ID, int srcID, int destX, int destY, int destWidth, int destHeight, int srcX, int srcY, int srcWidth, int srcHeight, float[][] cm)
        /// エラーチェックは呼び出し元でのみ行う
        /// </summary>
        public void GDrawG(GraphicsImage srcGra, Rectangle destRect, Rectangle srcRect, float[][] cm)
        {
            GDrawG(srcGra, destRect, srcRect);
        }


        /// <summary>
        /// GDRAWGWITHMASK(int ID, int srcID, int maskID, int destX, int destY)
        /// エラーチェックは呼び出し元でのみ行う
        /// </summary>
        public void GDrawGWithMask(GraphicsImage srcGra, GraphicsImage maskGra, Point destPoint)
        {
            if (pixelBuffer == null || srcGra == null || srcGra.pixelBuffer == null || maskGra == null || maskGra.pixelBuffer == null)
                return;

            int srcW = srcGra.width;
            int srcH = srcGra.height;
            var srcBuf = srcGra.pixelBuffer;
            var maskBuf = maskGra.pixelBuffer;
            int maskW = maskGra.width;
            int maskH = maskGra.height;

            for (int y = 0; y < srcH; ++y)
            {
                int dstY = destPoint.Y + y;
                if (dstY < 0 || dstY >= height)
                    continue;
                int dstRowStart = dstY * width;
                int srcRowStart = y * srcW;

                int maskY = y * maskH / srcH;
                if (maskY < 0 || maskY >= maskH)
                    continue;
                int maskRowStart = maskY * maskW;

                for (int x = 0; x < srcW; ++x)
                {
                    int dstX = destPoint.X + x;
                    if (dstX < 0 || dstX >= width)
                        continue;

                    int maskX = x * maskW / srcW;
                    if (maskX < 0 || maskX >= maskW)
                        continue;

                    int maskAlpha = maskBuf[maskRowStart + maskX].a;
                    if (maskAlpha == 0)
                        continue;

                    var sc = srcBuf[srcRowStart + x];
                    if (maskAlpha == 255)
                    {
                        pixelBuffer[dstRowStart + dstX] = sc;
                    }
                    else
                    {
                        var dc = pixelBuffer[dstRowStart + dstX];
                        int alpha = maskAlpha + 1;
                        dc.r = (byte)((sc.r * alpha + dc.r * (256 - alpha)) >> 8);
                        dc.g = (byte)((sc.g * alpha + dc.g * (256 - alpha)) >> 8);
                        dc.b = (byte)((sc.b * alpha + dc.b * (256 - alpha)) >> 8);
                        dc.a = (byte)Math.Min(255, sc.a + (dc.a * (256 - alpha) >> 8));
                        pixelBuffer[dstRowStart + dstX] = dc;
                    }
                }
            }
            pixelDirty = true;
        }

        public void GSetFont(uEmuera.Drawing.Font r)
        {
        //	if (font != null)
        //		font.Dispose();
        //	font = r;
        }
        public void GSetBrush(Brush r)
        {
        //	if (brush != null)
        //		brush.Dispose();
        //	brush = r;
        }
        public void GSetPen(Pen r)
        {
        //	if (pen != null)
        //		pen.Dispose();
        //	pen = r;
        }
        //private static byte[] BytesFromBitmap(Bitmap bmp)
        //{
        //	BitmapData bmpData = bmp.LockBits(
        //	  new Rectangle(0, 0, bmp.Width, bmp.Height),
        //	  ImageLockMode.ReadOnly,  // 書き込むときはReadAndWriteで
        //	  PixelFormat.Format32bppArgb
        //	);
        //	if (bmpData.Stride < 0)
        //		throw new Exception();//変な形式のが送られてくることはありえないはずだが一応
        //	byte[] pixels = new byte[bmpData.Stride * bmp.Height];
        //	try
        //	{ 
        //		IntPtr ptr = bmpData.Scan0;
        //		System.Runtime.InteropServices.Marshal.Copy(ptr, pixels, 0, pixels.Length);
        //	}
        //	finally
        //	{
        //		bmp.UnlockBits(bmpData);

        //	}
        //	return pixels;
        //}

        /// <summary>
        /// GTOARRAY int ID, var array
        /// エラーチェックは呼び出し元でのみ行う
        /// <returns></returns>
        //public bool GBitmapToInt64Array(Int64[,] array, int xstart, int ystart)
        //{
        //	if (g == null || Bitmap == null)
        //		throw new NullReferenceException();
        //	int w = Bitmap.Width;
        //	int h = Bitmap.Height;
        //	if (xstart + w > array.GetLength(0) || ystart + h > array.GetLength(1))
        //		return false;
        //	Rectangle rect = new Rectangle(0, 0, w, h);
        //	System.Drawing.Imaging.BitmapData bmpData =
        //		Bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly,
        //		PixelFormat.Format32bppArgb);
        //	IntPtr ptr = bmpData.Scan0;
        //	byte[] rgbValues = new byte[w * h * 4];
        //	Marshal.Copy(ptr, rgbValues, 0, rgbValues.Length);
        //	Bitmap.UnlockBits(bmpData);
        //	int i = 0;
        //	for (int y = 0; y < h; y++)
        //	{
        //		for (int x = 0; x < w; x++)
        //		{
        //			array[x + xstart, y + ystart] =
        //			rgbValues[i++] + //B
        //			(((Int64)rgbValues[i++]) << 8) + //G
        //			(((Int64)rgbValues[i++]) << 16) + //R
        //			(((Int64)rgbValues[i++]) << 24);  //A
        //		}
        //	}
        //	return true;
        //}


        /// <summary>
        /// GFROMARRAY int ID, var array
        /// エラーチェックは呼び出し元でのみ行う
        /// <returns></returns>
        //public bool GByteArrayToBitmap(Int64[,] array, int xstart, int ystart)
        //{
        //	if (g == null || Bitmap == null)
        //		throw new NullReferenceException();
        //	int w = Bitmap.Width;
        //	int h = Bitmap.Height;
        //	if (xstart + w > array.GetLength(0) || ystart + h > array.GetLength(1))
        //		return false;

        //	byte[] rgbValues = new byte[w * h * 4];
        //	int i = 0;
        //	for (int y = 0; y < h; y++)
        //	{
        //		for (int x = 0; x < w; x++)
        //		{
        //			Int64 c = array[x + xstart, y + ystart];
        //			rgbValues[i++] = (byte)(c & 0xFF);//B
        //			rgbValues[i++] = (byte)((c >> 8) & 0xFF);//G
        //			rgbValues[i++] = (byte)((c >> 16) & 0xFF);//R
        //			rgbValues[i++] = (byte)((c >> 24) & 0xFF);//A
        //		}
        //	}
        //	Rectangle rect = new Rectangle(0, 0, w, h);
        //	System.Drawing.Imaging.BitmapData bmpData =
        //		Bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly,
        //		PixelFormat.Format32bppArgb);
        //	IntPtr ptr = bmpData.Scan0;
        //	Marshal.Copy(rgbValues, 0, ptr, rgbValues.Length);
        //	Bitmap.UnlockBits(bmpData);
        //	return true;
        //}
        #endregion
        #region Bitmap読み込み・削除
        /// <summary>
        /// 未作成ならエラー
        /// </summary>
        //public Bitmap GetBitmap()
        //{
        //	if (Bitmap == null)
        //		throw new NullReferenceException();
        //	//UnlockGraphics();
        //	return Bitmap;
        //}
        /// <summary>
        /// GSETCOLOR(int ID, int cARGB, int x, int y)
        /// エラーチェックは呼び出し元でのみ行う
        /// </summary>
        public void GSetColor(uEmuera.Drawing.Color c, int x, int y)
        {
            if (pixelBuffer == null)
            {
                if (Bitmap == null)
                    throw new NullReferenceException();
                Bitmap.SetPixel(c, x, y);
                return;
            }
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;
            pixelBuffer[y * width + x] = new Color32((byte)c.R, (byte)c.G, (byte)c.B, (byte)c.A);
            pixelDirty = true;
        }

        /// <summary>
        /// GGETCOLOR(int ID, int x, int y)
        /// エラーチェックは呼び出し元でのみ行う。特に画像範囲内であるかどうかチェックすること
        /// </summary>
        public uEmuera.Drawing.Color GGetColor(int x, int y)
        {
            if (pixelBuffer == null)
            {
                if (Bitmap == null)
                    throw new NullReferenceException();
                return Bitmap.GetPixel(x, y);
            }
            if (x < 0 || y < 0 || x >= width || y >= height)
                return uEmuera.Drawing.Color.Transparent;
            var uc = pixelBuffer[y * width + x];
            return new uEmuera.Drawing.Color(uc.r, uc.g, uc.b, uc.a);
        }


        /// <summary>
        /// GDISPOSE(int ID)
        /// </summary>
        public void GDispose()
        {
            Debug.Log($"[GraphicsImg] GDispose ID={ID}");

            is_created = false;
            width = 0;
            height = 0;

            Texture2D oldTex = pixelTexture;
            pixelTexture = null;
            pixelBuffer = null;
            buttonMapCache = null;
            pixelDirty = false;
            lock(allGraphicsLock) { allGraphics.Remove(this); }

            if (oldTex != null)
            {
                if (SpriteManager.IsMainThread())
                    UnityEngine.Object.Destroy(oldTex);
                else
                    SpriteManager.RunOnMainThread(() => UnityEngine.Object.Destroy(oldTex));
            }
        }

        public override void Dispose()
        {
            this.GDispose();
            lock(allGraphicsLock) { allGraphics.Remove(this); }
        }

        ~GraphicsImage()
        {
            Dispose();
        }
        #endregion

//#region 状態判定（Bitmap読み書きを伴わない）
        //public override bool IsCreated { get { return g != null; } }
        public override bool IsCreated { get { return is_created; } }
        bool is_created = false;

        /// <summary>
        /// int GWIDTH(int ID)
        /// </summary>
        public int Width { get { return width; } }
        int width = 0;
		/// <summary>
		/// int GHEIGHT(int ID)
		/// </summary>
		public int Height { get { return height; } }
        int height = 0;

        internal Color32[] pixelBuffer = null;
        internal Texture2D pixelTexture = null;
        internal bool pixelDirty = false;

        internal UnityEngine.Color32[] buttonMapCache = null;

        internal void CacheButtonMapPixels()
        {
            if (buttonMapCache != null)
                return;
            if (Bitmap == null)
                return;
            var ti = SpriteManager.GetTextureInfo(Bitmap.name, Bitmap.path);
            if (ti != null)
            {
                buttonMapCache = ti.GetPixelsCached();
            }
        }

        internal UnityEngine.Color32 GetButtonMapPixel(int x, int y)
        {
            if (buttonMapCache == null)
                CacheButtonMapPixels();
            if (buttonMapCache == null)
                return new UnityEngine.Color32(0, 0, 0, 0);
            if (x < 0 || y < 0 || x >= width || y >= height)
                return new UnityEngine.Color32(0, 0, 0, 0);
            return buttonMapCache[y * width + x];
        }
        //#endregion
	}
}
