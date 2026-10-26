using UnityEngine;
using UnityEngine.UI;
using uEmuera.Drawing;

namespace MinorShift.Emuera.Content
{
    public class UnityDrawContext : MonoBehaviour, IDrawContext
    {
        public static UnityDrawContext Create(string name, Transform parent, int width, int height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ctx = go.AddComponent<UnityDrawContext>();
            ctx.Init(width, height);
            return ctx;
        }

        RawImage rawImage;

        void Init(int width, int height)
        {
            _rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            _rt.Create();

            rawImage = gameObject.AddComponent<RawImage>();
            rawImage.texture = _rt;
            rawImage.raycastTarget = false;

            var rt = rawImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        RenderTexture _rt;
        Material _drawMat;
        Material drawMat
        {
            get
            {
                if (_drawMat == null)
                    _drawMat = new Material(Shader.Find("Sprites/Default"));
                return _drawMat;
            }
        }

        public void Clear(Color32 c)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            GL.Clear(true, true, c);
            RenderTexture.active = prev;
        }

        public void DrawTexture(Texture2D tex, Rect srcRect, Rect destRect)
        {
            if (tex == null) return;
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            UnityEngine.Graphics.DrawTexture(destRect, tex, srcRect, 0, 0, 0, 0, drawMat);
            RenderTexture.active = prev;
        }

        public void DrawSprite(Sprite sprite, Rect destRect)
        {
            if (sprite == null) return;
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var rect = new Rect(0, 0, sprite.rect.width, sprite.rect.height);
            var scaleX = destRect.width / sprite.rect.width;
            var scaleY = destRect.height / sprite.rect.height;
            var m = Matrix4x4.TRS(
                new Vector3(destRect.x, destRect.y, 0),
                Quaternion.identity,
                new Vector3(scaleX, scaleY, 1));
            UnityEngine.Graphics.DrawTexture(new Rect(0, 0, 1, 1), sprite.texture, rect,
                0, 0, 0, 0, drawMat);
            RenderTexture.active = prev;
        }

        public void DrawRect(Rect rect, Color32 color, bool fill)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            if (fill)
            {
                var tex = Texture2D.whiteTexture;
                UnityEngine.Graphics.DrawTexture(rect, tex, new Rect(0, 0, 1, 1), 0, 0, 0, 0, drawMat);
            }
            RenderTexture.active = prev;
        }

        public void Resize(int width, int height)
        {
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
            }
            _rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            _rt.Create();
            rawImage.texture = _rt;
        }

        void OnDestroy()
        {
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
            }
            if (_drawMat != null)
                Destroy(_drawMat);
        }

        public RenderTexture renderTexture { get { return _rt; } }
    }
}
