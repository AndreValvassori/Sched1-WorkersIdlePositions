using GameHintDisplay = Il2CppScheduleOne.UI.HintDisplay;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WorkersPlaces;

internal sealed class PlacementPrompt
{
    private static Texture2D? _background;
    private readonly GameObject _root;
    private readonly TextMeshProUGUI _label;

    private PlacementPrompt(TMP_FontAsset font, string message)
    {
        _root = new GameObject("WorkersPlaces Placement Prompt");
        RectTransform canvasRect = _root.AddComponent<RectTransform>();
        Canvas canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 30000;

        CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject shadeObject = new("WorkersPlaces Prompt Shade");
        RectTransform shadeRect = shadeObject.AddComponent<RectTransform>();
        shadeRect.SetParent(canvasRect, false);
        shadeRect.anchorMin = new Vector2(0.5f, 0.86f);
        shadeRect.anchorMax = shadeRect.anchorMin;
        shadeRect.pivot = new Vector2(0.5f, 0.5f);
        shadeRect.sizeDelta = new Vector2(700f, 56f);

        RawImage shade = shadeObject.AddComponent<RawImage>();
        shade.texture = GetBackground();
        shade.raycastTarget = false;

        GameObject labelObject = new("WorkersPlaces Prompt Text");
        RectTransform labelRect = labelObject.AddComponent<RectTransform>();
        labelRect.SetParent(shadeRect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(20f, 4f);
        labelRect.offsetMax = new Vector2(-20f, -4f);

        _label = labelObject.AddComponent<TextMeshProUGUI>();
        _label.font = font;
        _label.fontSize = 20f;
        _label.fontSizeMin = 16f;
        _label.fontSizeMax = 20f;
        _label.enableAutoSizing = true;
        _label.fontStyle = FontStyles.Bold;
        _label.color = Color.white;
        _label.alignment = TextAlignmentOptions.Center;
        _label.enableWordWrapping = false;
        _label.overflowMode = TextOverflowModes.Ellipsis;
        _label.raycastTarget = false;
        _label.text = message;
    }

    public static PlacementPrompt? TryCreate(TextMeshProUGUI? fallbackText, string message)
    {
        GameHintDisplay? hint = UnityEngine.Object.FindObjectOfType<GameHintDisplay>();
        TMP_FontAsset? font = hint?.Label?.font ?? fallbackText?.font;
        return font == null ? null : new PlacementPrompt(font, message);
    }

    public void SetText(string message)
    {
        _label.text = message;
    }

    public void Destroy()
    {
        if (_root != null)
        {
            UnityEngine.Object.Destroy(_root);
        }
    }

    private static Texture2D GetBackground()
    {
        if (_background != null)
        {
            return _background;
        }

        const int width = 700;
        const int height = 56;
        const int radius = 8;
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                float horizontal = column < radius ? radius - column : column > width - radius ? column - (width - radius) : 0f;
                float vertical = row < radius ? radius - row : row > height - radius ? row - (height - radius) : 0f;
                float alpha = horizontal * horizontal + vertical * vertical > radius * radius ? 0f : 0.82f;
                texture.SetPixel(column, row, new Color(0.13f, 0.14f, 0.15f, alpha));
            }
        }

        texture.Apply();
        _background = texture;
        return texture;
    }
}
