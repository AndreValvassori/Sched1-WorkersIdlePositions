using Il2CppScheduleOne.Employees;
using Il2CppScheduleOne.Management.UI;
using Il2CppScheduleOne.UI.Management;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WorkersPlaces;

internal sealed class ClipboardIdleControls
{
    private static Texture2D? _woodTexture;
    private readonly GameObject _root;
    private readonly TextMeshProUGUI _status;
    private readonly TextMeshProUGUI _setLabel;
    private readonly Button _setButton;
    private readonly Button _clearButton;
    private readonly Action _setAction;
    private readonly Action _clearAction;
    private readonly Color _defaultStatusColor;
    private bool? _lastHasPosition;
    private bool? _lastCanEdit;

    private ClipboardIdleControls(ConfigPanel panel, TextMeshProUGUI template, Action set, Action clear)
    {
        _setAction = set;
        _clearAction = clear;
        _defaultStatusColor = template.color;
        _root = new GameObject("WorkersPlaces Idle Position");
        RectTransform rootRect = _root.AddComponent<RectTransform>();
        rootRect.SetParent(panel.transform, false);
        rootRect.anchorMin = new Vector2(1.13f, 0.70f);
        rootRect.anchorMax = new Vector2(1.66f, 0.98f);
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        RawImage frame = _root.AddComponent<RawImage>();
        frame.texture = GetWoodTexture();
        frame.raycastTarget = false;
        frame.maskable = false;

        RectTransform paper = CreateSurface(rootRect, "Paper", new Vector2(0.045f, 0.06f), new Vector2(0.955f, 0.94f), new Color(0.99f, 0.99f, 0.975f, 1f));
        CreateText(paper, template, "Title", "Idle position", new Vector2(0.08f, 0.73f), new Vector2(0.92f, 0.96f));
        CreateSurface(paper, "Rule", new Vector2(0.08f, 0.69f), new Vector2(0.92f, 0.705f), new Color(0.25f, 0.23f, 0.21f, 1f));
        _status = CreateText(paper, template, "Status", "Default", new Vector2(0.08f, 0.45f), new Vector2(0.92f, 0.67f));
        _setButton = CreateButton(paper, template, "Set", new Vector2(0.08f, 0.10f), new Vector2(0.65f, 0.36f), out _setLabel);
        _clearButton = CreateButton(paper, template, "Clear", new Vector2(0.68f, 0.10f), new Vector2(0.95f, 0.36f), out _);
    }

    public static ClipboardIdleControls? TryCreate(EEmployeeType workerType, Action set, Action clear)
    {
        if (workerType == EEmployeeType.Chemist)
        {
            foreach (ChemistConfigPanel panel in UnityEngine.Object.FindObjectsOfType<ChemistConfigPanel>())
            {
                if (panel.gameObject.activeInHierarchy && panel.BedUI?.FieldLabel != null)
                {
                    return new ClipboardIdleControls(panel, panel.BedUI.FieldLabel, set, clear);
                }
            }
        }

        if (workerType == EEmployeeType.Botanist)
        {
            foreach (BotanistConfigPanel panel in UnityEngine.Object.FindObjectsOfType<BotanistConfigPanel>())
            {
                if (panel.gameObject.activeInHierarchy && panel.BedUI?.FieldLabel != null)
                {
                    return new ClipboardIdleControls(panel, panel.BedUI.FieldLabel, set, clear);
                }
            }
        }

        if (workerType == EEmployeeType.Cleaner)
        {
            foreach (CleanerConfigPanel panel in UnityEngine.Object.FindObjectsOfType<CleanerConfigPanel>())
            {
                if (panel.gameObject.activeInHierarchy && panel.BedUI?.FieldLabel != null)
                {
                    return new ClipboardIdleControls(panel, panel.BedUI.FieldLabel, set, clear);
                }
            }
        }

        if (workerType == EEmployeeType.Handler)
        {
            foreach (PackagerConfigPanel panel in UnityEngine.Object.FindObjectsOfType<PackagerConfigPanel>())
            {
                if (panel.gameObject.activeInHierarchy && panel.BedUI?.FieldLabel != null)
                {
                    return new ClipboardIdleControls(panel, panel.BedUI.FieldLabel, set, clear);
                }
            }
        }

        return null;
    }

    public void Refresh(bool hasPosition, bool canEdit)
    {
        if (_lastHasPosition == hasPosition && _lastCanEdit == canEdit)
        {
            return;
        }

        _lastHasPosition = hasPosition;
        _lastCanEdit = canEdit;
        _status.text = hasPosition ? "Custom" : "Default";
        _status.color = hasPosition ? new Color(0.17f, 0.43f, 0.31f, 1f) : _defaultStatusColor;
        _setLabel.text = hasPosition ? "Change" : "Set";
        _setButton.interactable = canEdit;
        _clearButton.gameObject.SetActive(hasPosition && canEdit);
    }

    public void HandlePointerClick(Camera? fallbackCamera)
    {
        if (!_lastCanEdit.GetValueOrDefault() || !Input.GetMouseButtonDown(0))
        {
            return;
        }

        Canvas? canvas = _root.GetComponentInParent<Canvas>();
        Camera? eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera ?? fallbackCamera
            : null;
        Vector3 mousePosition = Input.mousePosition;
        Vector2 pointerPosition = new(mousePosition.x, mousePosition.y);

        if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_setButton.transform, pointerPosition, eventCamera))
        {
            _setAction();
        }
        else if (_lastHasPosition.GetValueOrDefault() && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)_clearButton.transform, pointerPosition, eventCamera))
        {
            _clearAction();
        }
    }

    public void Destroy()
    {
        if (_root != null)
        {
            UnityEngine.Object.Destroy(_root);
        }
    }

    private static RectTransform CreateSurface(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        GameObject surface = new($"WorkersPlaces {name}");
        RectTransform rect = surface.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        RawImage image = surface.AddComponent<RawImage>();
        image.texture = Texture2D.whiteTexture;
        image.color = color;
        image.raycastTarget = false;
        image.maskable = false;
        return rect;
    }

    private static Texture2D GetWoodTexture()
    {
        if (_woodTexture != null)
        {
            return _woodTexture;
        }

        const int width = 96;
        const int height = 72;
        Texture2D texture = new(width, height, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;

        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                float grain = Mathf.Sin(column * 0.31f + Mathf.Sin(row * 0.27f) * 0.9f) * 0.035f;
                grain += Mathf.Sin(column * 0.92f + row * 0.08f) * 0.012f;
                float alpha = IsOutsideRoundedCorner(column, row, width, height) ? 0f : 1f;
                texture.SetPixel(column, row, new Color(0.24f + grain, 0.17f + grain * 0.75f, 0.13f + grain * 0.55f, alpha));
            }
        }

        texture.Apply();
        _woodTexture = texture;
        return texture;
    }

    private static bool IsOutsideRoundedCorner(int column, int row, int width, int height)
    {
        const float radius = 8f;
        float horizontal = column < radius ? radius - column : column > width - radius ? column - (width - radius) : 0f;
        float vertical = row < radius ? radius - row : row > height - radius ? row - (height - radius) : 0f;
        return horizontal * horizontal + vertical * vertical > radius * radius;
    }

    private static TextMeshProUGUI CreateText(RectTransform parent, TextMeshProUGUI template, string name, string content, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject objectWithText = new($"WorkersPlaces {name}");
        RectTransform rect = objectWithText.AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = objectWithText.AddComponent<TextMeshProUGUI>();
        label.font = template.font;
        label.fontSize = Mathf.Min(template.fontSize * 0.72f, 18f);
        label.color = template.color;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableAutoSizing = false;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.text = content;
        label.raycastTarget = false;
        label.maskable = false;
        return label;
    }

    private static Button CreateButton(RectTransform parent, TextMeshProUGUI template, string name, Vector2 anchorMin, Vector2 anchorMax, out TextMeshProUGUI label)
    {
        label = CreateText(parent, template, name, name, anchorMin, anchorMax);
        label.raycastTarget = true;
        label.fontStyle = FontStyles.Underline;
        Button button = label.gameObject.AddComponent<Button>();
        button.targetGraphic = label;
        return button;
    }
}
