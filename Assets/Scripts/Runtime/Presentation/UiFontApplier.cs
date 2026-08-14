using UnityEngine;
using UnityEngine.UI;

namespace FarmCreature.Presentation
{
    /// <summary>
    /// 하위의 모든 <see cref="Text"/> 에 OS 폰트를 런타임에 꽂아준다.
    /// <see cref="Font.CreateDynamicFontFromOSFont"/> 가 만드는 Font 는 에셋이 아니라
    /// 씬에 직렬화되지 않는다 — 그래서 에디터에서 지정하지 않고 여기서 붙인다.
    /// 한글 글리프가 있는 OS 폰트를 써야 상태 패널과 버튼 라벨이 보인다.
    /// </summary>
    public class UiFontApplier : MonoBehaviour
    {
        public string[] fontNames = { "맑은 고딕", "Malgun Gothic", "Gulim", "Arial" };
        public int baseSize = 32;

        private void Awake()
        {
            Font font = null;

            for (int i = 0; i < fontNames.Length && font == null; i++)
            {
                font = Font.CreateDynamicFontFromOSFont(fontNames[i], baseSize);
            }

            if (font == null)
            {
                Debug.LogWarning("[FarmCreature] OS 폰트를 찾지 못했습니다. UI 텍스트가 보이지 않을 수 있습니다.");
                return;
            }

            var texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                texts[i].font = font;
            }
        }
    }
}
