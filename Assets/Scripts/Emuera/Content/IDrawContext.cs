// IDrawContext.cs
// ---------------------------------------------------------------------------
// 【补全说明】本文件在 Trae History 中不存在（从未被编辑器保存过），
// 以下签名是根据唯一实现方 UnityDrawContext.cs（快照 History\486e865\dvN9.cs，2026-05-04 12:04:32）
// 中 5 个 public 方法反推补全的，用于让工程可编译。
// 若日后找到原始接口定义，请以原始定义为准。
// ---------------------------------------------------------------------------
using UnityEngine;

namespace MinorShift.Emuera.Content
{
    public interface IDrawContext
    {
        void Clear(Color32 c);
        void DrawTexture(Texture2D tex, Rect srcRect, Rect destRect);
        void DrawSprite(Sprite sprite, Rect destRect);
        void DrawRect(Rect rect, Color32 color, bool fill);
        void Resize(int width, int height);
    }
}
