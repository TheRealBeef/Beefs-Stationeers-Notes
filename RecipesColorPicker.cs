using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using BepInEx;
using Newtonsoft.Json;

namespace BeefsRecipes
{
    public class RecipesColorPicker : MonoBehaviour
    {
        private static RecipesColorPicker _instance;
        private static Canvas _cachedCanvas;
        private GameObject _menuObject;
        private Action<Color> _onColorSelected;

        private float _hue = 0f;
        private float _saturation = 1f;
        private float _value = 1f;
        private Color _previewColor = Color.white;
        private Color _originalColor = Color.white;

        private Image _previewImage;
        private Image _originalImage;
        private InputField _hexInput;
        private RawImage _svImage;
        private RawImage _hueImage;
        private Texture2D _svTexture;
        private Texture2D _hueTexture;
        private RectTransform _svCursor;
        private RectTransform _hueCursor;
        private bool _updatingHex = false;

        private static List<string> _customPalette = null;
        private const int MaxPaletteSlots = 6;
        private const string PaletteFileName = "palette.json";
        private static string PalettePath =>
            Path.Combine(Paths.ConfigPath, "BeefsRecipes", "client", PaletteFileName);
        private readonly List<Image> _paletteSlotImages = new List<Image>();

        private static readonly Color[] PresetColors = new Color[]
        {
            Color.white,
            new Color(1f, 0.35f, 0.35f),
            new Color(1f, 0.6f, 0.2f),
            new Color(1f, 0.9f, 0.2f),
            new Color(0.35f, 1f, 0.35f),
            new Color(0.2f, 1f, 0.8f),
            new Color(0.35f, 0.6f, 1f),
            new Color(0.6f, 0.4f, 1f),
            new Color(1f, 0.4f, 0.8f),
            new Color(1f, 0.7f, 0.3f)
        };

        private static float MenuWidth => 240f * RecipesUIManager.ScaleFactor;
        private static float Padding => 8f * RecipesUIManager.ScaleFactor;
        private static float SVSize => 150f * RecipesUIManager.ScaleFactor;
        private static float HueBarHeight => 18f * RecipesUIManager.ScaleFactor;
        private static float SwatchSize => 24f * RecipesUIManager.ScaleFactor;
        private static float SwatchSpacing => 4f * RecipesUIManager.ScaleFactor;
        private static float RowHeight => 28f * RecipesUIManager.ScaleFactor;
        private static float SectionSpacing => 6f * RecipesUIManager.ScaleFactor;
        private static float ButtonHeight => 24f * RecipesUIManager.ScaleFactor;

        public static void Show(Vector2 screenPosition, Color currentColor, Action<Color> onColorSelected)
        {
            if (_instance != null)
            {
                Destroy(_instance._menuObject);
                Destroy(_instance.gameObject);
                _instance = null;
            }

            LoadPalette();

            GameObject menuObj = new GameObject("RecipesColorPicker");
            _instance = menuObj.AddComponent<RecipesColorPicker>();
            _instance._onColorSelected = onColorSelected;
            _instance._originalColor = currentColor;
            _instance._previewColor = currentColor == Color.clear ? Color.white : currentColor;

            Color.RGBToHSV(
                currentColor == Color.clear ? Color.white : currentColor,
                out _instance._hue,
                out _instance._saturation,
                out _instance._value);

            _instance.CreateMenu(screenPosition, currentColor);
        }

        private void CreateMenu(Vector2 screenPosition, Color currentColor)
        {
            _cachedCanvas = BeefsRecipesPlugin.Instance?.UICanvas;

            Canvas canvas = _cachedCanvas;
            if (canvas == null) return;

            float contentHeight =
                Padding +
                SVSize +
                SectionSpacing +
                HueBarHeight +
                SectionSpacing +
                RowHeight +
                SectionSpacing +
                SwatchSize +
                SectionSpacing +
                SwatchSize +
                SectionSpacing +
                ButtonHeight +
                SectionSpacing +
                ButtonHeight +
                Padding;

            _menuObject = new GameObject("ColorMenu");
            _menuObject.transform.SetParent(canvas.transform, false);

            RectTransform menuRect = _menuObject.AddComponent<RectTransform>();
            menuRect.sizeDelta = new Vector2(MenuWidth, contentHeight);
            menuRect.anchorMin = new Vector2(0, 0);
            menuRect.anchorMax = new Vector2(0, 0);
            menuRect.pivot = new Vector2(0.5f, 0.5f);

            float halfW = MenuWidth * 0.5f;
            float halfH = contentHeight * 0.5f;
            float x = Mathf.Clamp(screenPosition.x, halfW + 4f, Screen.width - halfW - 4f);
            float y = Mathf.Clamp(screenPosition.y, halfH + 4f, Screen.height - halfH - 4f);
            menuRect.anchoredPosition = new Vector2(x, y);

            Image bg = _menuObject.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            float yPos = -Padding;

            yPos = CreateSVSquare(_menuObject.transform, yPos);
            yPos -= SectionSpacing;
            yPos = CreateHueBar(_menuObject.transform, yPos);
            yPos -= SectionSpacing;
            yPos = CreatePreviewAndHexRow(_menuObject.transform, yPos);
            yPos -= SectionSpacing;
            yPos = CreatePresetRow(_menuObject.transform, yPos, currentColor);
            yPos -= SectionSpacing;
            yPos = CreatePaletteRow(_menuObject.transform, yPos);
            yPos -= SectionSpacing;
            CreateButtonRow(_menuObject.transform, yPos);

            RegenerateHueTexture();
            RegenerateSVTexture();
            UpdateCursors();
            UpdatePreview();
        }

        private float CreateSVSquare(Transform parent, float yPos)
        {
            float squareLeft = (MenuWidth - SVSize) * 0.5f;

            GameObject svObj = new GameObject("SVSquare");
            svObj.transform.SetParent(parent, false);

            RectTransform svRect = svObj.AddComponent<RectTransform>();
            svRect.anchorMin = new Vector2(0, 1);
            svRect.anchorMax = new Vector2(0, 1);
            svRect.pivot = new Vector2(0, 1);
            svRect.anchoredPosition = new Vector2(squareLeft, yPos);
            svRect.sizeDelta = new Vector2(SVSize, SVSize);

            _svTexture = new Texture2D(64, 64, TextureFormat.RGB24, false);
            _svTexture.filterMode = FilterMode.Bilinear;
            _svTexture.wrapMode = TextureWrapMode.Clamp;

            _svImage = svObj.AddComponent<RawImage>();
            _svImage.texture = _svTexture;
            _svImage.raycastTarget = true;

            GameObject cursorObj = new GameObject("SVCursor");
            cursorObj.transform.SetParent(svObj.transform, false);
            _svCursor = cursorObj.AddComponent<RectTransform>();
            _svCursor.sizeDelta = new Vector2(10, 10);
            _svCursor.anchorMin = new Vector2(0, 0);
            _svCursor.anchorMax = new Vector2(0, 0);
            _svCursor.pivot = new Vector2(0.5f, 0.5f);

            Image cursorImage = cursorObj.AddComponent<Image>();
            cursorImage.color = Color.white;
            cursorImage.raycastTarget = false;

            GameObject cursorInner = new GameObject("Inner");
            cursorInner.transform.SetParent(cursorObj.transform, false);
            RectTransform innerRect = cursorInner.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(2, 2);
            innerRect.offsetMax = new Vector2(-2, -2);
            Image innerImage = cursorInner.AddComponent<Image>();
            innerImage.color = Color.black;
            innerImage.raycastTarget = false;

            var handler = svObj.AddComponent<PickerDragHandler>();
            handler.onInteract = OnSVInteract;

            return yPos - SVSize;
        }

        private void OnSVInteract(Vector2 normalizedPos)
        {
            _saturation = Mathf.Clamp01(normalizedPos.x);
            _value = Mathf.Clamp01(normalizedPos.y);
            _previewColor = Color.HSVToRGB(_hue, _saturation, _value);
            UpdateCursors();
            UpdatePreview();
        }

        private float CreateHueBar(Transform parent, float yPos)
        {
            GameObject hueObj = new GameObject("HueBar");
            hueObj.transform.SetParent(parent, false);

            RectTransform hueRect = hueObj.AddComponent<RectTransform>();
            hueRect.anchorMin = new Vector2(0, 1);
            hueRect.anchorMax = new Vector2(0, 1);
            hueRect.pivot = new Vector2(0, 1);
            hueRect.anchoredPosition = new Vector2(Padding, yPos);
            hueRect.sizeDelta = new Vector2(MenuWidth - Padding * 2f, HueBarHeight);

            _hueTexture = new Texture2D(256, 1, TextureFormat.RGB24, false);
            _hueTexture.filterMode = FilterMode.Bilinear;
            _hueTexture.wrapMode = TextureWrapMode.Clamp;

            _hueImage = hueObj.AddComponent<RawImage>();
            _hueImage.texture = _hueTexture;
            _hueImage.raycastTarget = true;

            GameObject cursorObj = new GameObject("HueCursor");
            cursorObj.transform.SetParent(hueObj.transform, false);
            _hueCursor = cursorObj.AddComponent<RectTransform>();
            _hueCursor.sizeDelta = new Vector2(4, HueBarHeight + 4);
            _hueCursor.anchorMin = new Vector2(0, 0.5f);
            _hueCursor.anchorMax = new Vector2(0, 0.5f);
            _hueCursor.pivot = new Vector2(0.5f, 0.5f);

            Image hueCursorImage = cursorObj.AddComponent<Image>();
            hueCursorImage.color = Color.white;
            hueCursorImage.raycastTarget = false;

            var handler = hueObj.AddComponent<PickerDragHandler>();
            handler.onInteract = OnHueInteract;

            return yPos - HueBarHeight;
        }

        private void OnHueInteract(Vector2 normalizedPos)
        {
            _hue = Mathf.Clamp01(normalizedPos.x);
            _previewColor = Color.HSVToRGB(_hue, _saturation, _value);
            RegenerateSVTexture();
            UpdateCursors();
            UpdatePreview();
        }

        private float CreatePreviewAndHexRow(Transform parent, float yPos)
        {
            float rowWidth = MenuWidth - Padding * 2f;

            _originalImage = CreateSmallSwatch(parent, Padding, yPos, RowHeight,
                _originalColor == Color.clear ? new Color(0.2f, 0.2f, 0.2f) : _originalColor);

            CreateLabel(parent, Padding + RowHeight + 2f, yPos, 16f, RowHeight, "\u2192");

            _previewImage = CreateSmallSwatch(parent, Padding + RowHeight + 18f, yPos,
                RowHeight, _previewColor);

            float hexLeft = Padding + RowHeight * 2f + 24f;
            float hexWidth = rowWidth - RowHeight * 2f - 24f;

            GameObject hexContainer = new GameObject("HexContainer");
            hexContainer.transform.SetParent(parent, false);
            RectTransform hexContainerRect = hexContainer.AddComponent<RectTransform>();
            hexContainerRect.anchorMin = new Vector2(0, 1);
            hexContainerRect.anchorMax = new Vector2(0, 1);
            hexContainerRect.pivot = new Vector2(0, 1);
            hexContainerRect.anchoredPosition = new Vector2(hexLeft, yPos);
            hexContainerRect.sizeDelta = new Vector2(hexWidth, RowHeight);

            Image hexBg = hexContainer.AddComponent<Image>();
            hexBg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            CreateLabel(hexContainer.transform, 4f, 0f, 12f, RowHeight, "#",
                TextAnchor.MiddleLeft, anchorFromTop: false);

            GameObject inputObj = new GameObject("HexInput");
            inputObj.transform.SetParent(hexContainer.transform, false);

            RectTransform inputRect = inputObj.AddComponent<RectTransform>();
            inputRect.anchorMin = Vector2.zero;
            inputRect.anchorMax = Vector2.one;
            inputRect.offsetMin = new Vector2(14f, 2f);
            inputRect.offsetMax = new Vector2(-2f, -2f);

            Image inputBg = inputObj.AddComponent<Image>();
            inputBg.color = new Color(0, 0, 0, 0);

            _hexInput = inputObj.AddComponent<InputField>();
            _hexInput.characterLimit = 6;

            GameObject inputTextObj = new GameObject("Text");
            inputTextObj.transform.SetParent(inputObj.transform, false);
            RectTransform inputTextRect = inputTextObj.AddComponent<RectTransform>();
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = Vector2.zero;
            inputTextRect.offsetMax = Vector2.zero;

            Text inputText = inputTextObj.AddComponent<Text>();
            inputText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            inputText.fontSize = Mathf.RoundToInt(13 * RecipesUIManager.ScaleFactor);
            inputText.color = Color.white;
            inputText.alignment = TextAnchor.MiddleLeft;
            inputText.supportRichText = false;

            _hexInput.textComponent = inputText;
            _hexInput.text = ColorUtility.ToHtmlStringRGB(_previewColor);
            _hexInput.onValueChanged.AddListener(OnHexInputChanged);

            return yPos - RowHeight;
        }

        private void OnHexInputChanged(string hex)
        {
            if (_updatingHex) return;
            if (hex.Length != 6) return;

            Color parsed;
            if (ColorUtility.TryParseHtmlString("#" + hex, out parsed))
            {
                Color.RGBToHSV(parsed, out _hue, out _saturation, out _value);
                _previewColor = parsed;
                RegenerateSVTexture();
                UpdateCursors();
                UpdatePreviewVisuals();
            }
        }

        private float CreatePresetRow(Transform parent, float yPos, Color currentColor)
        {
            float totalWidth = PresetColors.Length * SwatchSize +
                               (PresetColors.Length - 1) * SwatchSpacing;
            float startX = (MenuWidth - totalWidth) * 0.5f;

            for (int i = 0; i < PresetColors.Length; i++)
            {
                float xOff = startX + i * (SwatchSize + SwatchSpacing);
                Color c = PresetColors[i];

                GameObject swObj = CreateSwatchButton(parent, xOff, yPos, SwatchSize, c,
                    Approximately(c, currentColor));
                int idx = i;
                swObj.GetComponent<Button>().onClick.AddListener(
                    () => OnPresetClicked(PresetColors[idx]));
            }

            return yPos - SwatchSize;
        }

        private void OnPresetClicked(Color color)
        {
            _onColorSelected?.Invoke(color);
            Hide();
        }

        private float CreatePaletteRow(Transform parent, float yPos)
        {
            float totalSlotWidth = MaxPaletteSlots * SwatchSize +
                                   (MaxPaletteSlots - 1) * SwatchSpacing;
            float saveButtonWidth = SwatchSize;
            float totalWidth = totalSlotWidth + SwatchSpacing + saveButtonWidth;
            float startX = (MenuWidth - totalWidth) * 0.5f;

            _paletteSlotImages.Clear();

            for (int i = 0; i < MaxPaletteSlots; i++)
            {
                float xOff = startX + i * (SwatchSize + SwatchSpacing);
                Color slotColor = GetPaletteColor(i);
                bool isEmpty = slotColor == Color.clear;

                GameObject swObj = CreateSwatchButton(parent, xOff, yPos, SwatchSize,
                    isEmpty ? new Color(0.2f, 0.2f, 0.2f, 0.5f) : slotColor, false);

                _paletteSlotImages.Add(swObj.GetComponent<Image>());

                int idx = i;
                swObj.GetComponent<Button>().onClick.AddListener(
                    () => OnPaletteSlotClicked(idx));
            }

            float saveBtnX = startX + MaxPaletteSlots * (SwatchSize + SwatchSpacing);
            GameObject saveObj = CreateSwatchButton(parent, saveBtnX, yPos, SwatchSize,
                new Color(0.3f, 0.5f, 0.3f, 0.8f), false);

            GameObject saveLabelObj = new GameObject("Label");
            saveLabelObj.transform.SetParent(saveObj.transform, false);
            RectTransform saveLabelRect = saveLabelObj.AddComponent<RectTransform>();
            saveLabelRect.anchorMin = Vector2.zero;
            saveLabelRect.anchorMax = Vector2.one;
            saveLabelRect.offsetMin = Vector2.zero;
            saveLabelRect.offsetMax = Vector2.zero;
            Text saveLabel = saveLabelObj.AddComponent<Text>();
            saveLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            saveLabel.text = "+";
            saveLabel.fontSize = Mathf.RoundToInt(16 * RecipesUIManager.ScaleFactor);
            saveLabel.color = Color.white;
            saveLabel.alignment = TextAnchor.MiddleCenter;
            saveLabel.fontStyle = FontStyle.Bold;
            saveLabel.raycastTarget = false;

            saveObj.GetComponent<Button>().onClick.AddListener(OnSaveToPalette);

            return yPos - SwatchSize;
        }

        private void OnPaletteSlotClicked(int index)
        {
            Color c = GetPaletteColor(index);
            if (c == Color.clear) return;

            _onColorSelected?.Invoke(c);
            Hide();
        }

        private void OnSaveToPalette()
        {
            string hex = "#" + ColorUtility.ToHtmlStringRGB(_previewColor);

            if (_customPalette.Count < MaxPaletteSlots)
                _customPalette.Add(hex);
            else
                _customPalette[MaxPaletteSlots - 1] = hex;

            SavePalette();
            RefreshPaletteSlots();
        }

        private void RefreshPaletteSlots()
        {
            for (int i = 0; i < _paletteSlotImages.Count && i < MaxPaletteSlots; i++)
            {
                Color c = GetPaletteColor(i);
                bool isEmpty = c == Color.clear;
                _paletteSlotImages[i].color = isEmpty
                    ? new Color(0.2f, 0.2f, 0.2f, 0.5f) : c;
            }
        }

        private void CreateButtonRow(Transform parent, float yPos)
        {
            float halfWidth = (MenuWidth - Padding * 2f - SwatchSpacing) * 0.5f;
            float fullWidth = MenuWidth - Padding * 2f;

            GameObject clearObj = CreateTextButton(parent, Padding, yPos,
                halfWidth, ButtonHeight, "Clear", new Color(0.45f, 0.2f, 0.2f, 1f));
            clearObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                _onColorSelected?.Invoke(Color.clear);
                Hide();
            });

            GameObject cancelObj = CreateTextButton(parent,
                Padding + halfWidth + SwatchSpacing, yPos,
                halfWidth, ButtonHeight, "Cancel", new Color(0.35f, 0.35f, 0.35f, 1f));
            cancelObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                Hide();
            });

            yPos -= ButtonHeight + SectionSpacing;

            GameObject applyObj = CreateTextButton(parent, Padding, yPos,
                fullWidth, ButtonHeight, "Apply", new Color(0.2f, 0.45f, 0.2f, 1f));
            applyObj.GetComponent<Button>().onClick.AddListener(() =>
            {
                _onColorSelected?.Invoke(_previewColor);
                Hide();
            });
        }

        private void RegenerateHueTexture()
        {
            for (int x = 0; x < 256; x++)
            {
                _hueTexture.SetPixel(x, 0, Color.HSVToRGB(x / 255f, 1f, 1f));
            }
            _hueTexture.Apply();
        }

        private void RegenerateSVTexture()
        {
            int size = _svTexture.width;
            for (int y = 0; y < size; y++)
            {
                float v = y / (float)(size - 1);
                for (int x = 0; x < size; x++)
                {
                    float s = x / (float)(size - 1);
                    _svTexture.SetPixel(x, y, Color.HSVToRGB(_hue, s, v));
                }
            }
            _svTexture.Apply();
        }

        private void UpdateCursors()
        {
            if (_svCursor != null)
            {
                _svCursor.anchoredPosition = new Vector2(
                    _saturation * SVSize, _value * SVSize);

                Image cursorImg = _svCursor.GetComponent<Image>();
                if (cursorImg != null)
                    cursorImg.color = _value > 0.5f ? Color.black : Color.white;
            }

            if (_hueCursor != null)
            {
                float barWidth = MenuWidth - Padding * 2f;
                _hueCursor.anchoredPosition = new Vector2(_hue * barWidth, 0f);
            }
        }

        private void UpdatePreview()
        {
            UpdatePreviewVisuals();

            _updatingHex = true;
            if (_hexInput != null)
                _hexInput.text = ColorUtility.ToHtmlStringRGB(_previewColor);
            _updatingHex = false;
        }

        private void UpdatePreviewVisuals()
        {
            if (_previewImage != null)
                _previewImage.color = _previewColor;
        }

        private Image CreateSmallSwatch(Transform parent, float x, float yPos,
            float size, Color color)
        {
            GameObject obj = new GameObject("Swatch");
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, yPos);
            rect.sizeDelta = new Vector2(size, size);
            Image img = obj.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private void CreateLabel(Transform parent, float x, float yPos,
            float width, float height, string text,
            TextAnchor align = TextAnchor.MiddleCenter, bool anchorFromTop = true)
        {
            GameObject obj = new GameObject("Label");
            obj.transform.SetParent(parent, false);
            RectTransform rect = obj.AddComponent<RectTransform>();

            if (anchorFromTop)
            {
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
            }
            else
            {
                rect.anchorMin = new Vector2(0, 0);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 0.5f);
            }
            rect.anchoredPosition = new Vector2(x, yPos);
            rect.sizeDelta = new Vector2(width, height);

            Text t = obj.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = text;
            t.fontSize = Mathf.RoundToInt(13 * RecipesUIManager.ScaleFactor);
            t.color = Color.white;
            t.alignment = align;
            t.raycastTarget = false;
        }

        private GameObject CreateSwatchButton(Transform parent, float x, float yPos,
            float size, Color color, bool showOutline)
        {
            GameObject obj = new GameObject("SwatchBtn");
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, yPos);
            rect.sizeDelta = new Vector2(size, size);

            Image img = obj.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = true;

            if (showOutline)
            {
                GameObject outlineObj = new GameObject("Outline");
                outlineObj.transform.SetParent(obj.transform, false);
                RectTransform outlineRect = outlineObj.AddComponent<RectTransform>();
                outlineRect.anchorMin = Vector2.zero;
                outlineRect.anchorMax = Vector2.one;
                outlineRect.offsetMin = new Vector2(-2, -2);
                outlineRect.offsetMax = new Vector2(2, 2);
                Image outline = outlineObj.AddComponent<Image>();
                outline.color = new Color(1f, 1f, 0f, 0.9f);
                outline.raycastTarget = false;
                outlineObj.transform.SetAsFirstSibling();
            }

            Button btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;

            return obj;
        }

        private GameObject CreateTextButton(Transform parent, float x, float yPos,
            float width, float height, string label, Color bgColor)
        {
            GameObject obj = new GameObject($"Btn_{label}");
            obj.transform.SetParent(parent, false);

            RectTransform rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, yPos);
            rect.sizeDelta = new Vector2(width, height);

            Image img = obj.AddComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true;

            Button btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(obj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text t = textObj.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = label;
            t.fontSize = Mathf.RoundToInt(12 * RecipesUIManager.ScaleFactor);
            t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.fontStyle = FontStyle.Bold;
            t.raycastTarget = false;

            return obj;
        }

        private static void LoadPalette()
        {
            if (_customPalette != null) return;

            _customPalette = new List<string>();
            try
            {
                if (File.Exists(PalettePath))
                {
                    string json = File.ReadAllText(PalettePath);
                    var loaded = JsonConvert.DeserializeObject<List<string>>(json);
                    if (loaded != null)
                    {
                        _customPalette = loaded;
                        if (_customPalette.Count > MaxPaletteSlots)
                            _customPalette.RemoveRange(MaxPaletteSlots,
                                _customPalette.Count - MaxPaletteSlots);
                    }
                }
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Failed to load palette: {ex.Message}");
            }
        }

        private static void SavePalette()
        {
            try
            {
                string dir = Path.GetDirectoryName(PalettePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonConvert.SerializeObject(_customPalette, Formatting.Indented);
                File.WriteAllText(PalettePath, json);
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Failed to save palette: {ex.Message}");
            }
        }

        private static Color GetPaletteColor(int index)
        {
            if (_customPalette == null || index < 0 || index >= _customPalette.Count)
                return Color.clear;

            Color c;
            if (ColorUtility.TryParseHtmlString(_customPalette[index], out c))
                return c;
            return Color.clear;
        }

        private static bool Approximately(Color a, Color b, float variance = 0.015f)
        {
            return Mathf.Abs(a.r - b.r) < variance &&
                   Mathf.Abs(a.g - b.g) < variance &&
                   Mathf.Abs(a.b - b.b) < variance &&
                   Mathf.Abs(a.a - b.a) < variance;
        }

        public void Hide()
        {
            if (_svTexture != null) Destroy(_svTexture);
            if (_hueTexture != null) Destroy(_hueTexture);

            if (_menuObject != null) Destroy(_menuObject);
            if (gameObject != null) Destroy(gameObject);
            _instance = null;
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                if (EventSystem.current != null && _menuObject != null)
                {
                    PointerEventData pointerData = new PointerEventData(EventSystem.current)
                    {
                        position = Input.mousePosition
                    };

                    List<RaycastResult> results = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(pointerData, results);

                    bool clickedInMenu = false;
                    foreach (var result in results)
                    {
                        if (result.gameObject == _menuObject ||
                            result.gameObject.transform.IsChildOf(_menuObject.transform))
                        {
                            clickedInMenu = true;
                            break;
                        }
                    }

                    if (!clickedInMenu)
                    {
                        Hide();
                    }
                }
            }
        }
    }

    public class PickerDragHandler : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public Action<Vector2> onInteract;

        public void OnPointerDown(PointerEventData eventData)
        {
            Notify(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Notify(eventData);
        }

        private void Notify(PointerEventData eventData)
        {
            RectTransform rect = GetComponent<RectTransform>();
            if (rect == null) return;

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect, eventData.position, eventData.pressEventCamera, out localPoint);

            Vector2 size = rect.rect.size;
            Vector2 normalized = new Vector2(
                Mathf.Clamp01((localPoint.x - rect.rect.xMin) / size.x),
                Mathf.Clamp01((localPoint.y - rect.rect.yMin) / size.y)
            );

            onInteract?.Invoke(normalized);
        }
    }
}