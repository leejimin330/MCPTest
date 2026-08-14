using UnityEngine;

namespace FarmCreature.Presentation
{
    /// <summary>에셋 파일 없이 코드로 만드는 플레이스홀더 스프라이트. 색은 SpriteRenderer.color 로 준다.</summary>
    public static class PlaceholderSprites
    {
        private static Sprite square;
        private static Sprite circle;

        public static Sprite Square()
        {
            if (square == null)
            {
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                texture.hideFlags = HideFlags.HideAndDontSave;

                square = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                square.hideFlags = HideFlags.HideAndDontSave;
            }

            return square;
        }

        public static Sprite Circle()
        {
            if (circle == null)
            {
                const int size = 64;
                const float radius = size * 0.5f;

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.hideFlags = HideFlags.HideAndDontSave;

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - radius;
                        float dy = y + 0.5f - radius;
                        bool inside = dx * dx + dy * dy <= radius * radius;
                        texture.SetPixel(x, y, inside ? Color.white : Color.clear);
                    }
                }

                texture.Apply();

                circle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
                circle.hideFlags = HideFlags.HideAndDontSave;
            }

            return circle;
        }
    }
}
