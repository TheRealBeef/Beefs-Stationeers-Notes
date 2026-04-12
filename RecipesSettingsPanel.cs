using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BeefsRecipes
{
    public class RecipesSettingsPanel
    {
        private readonly RecipesUIManager _uiManager;
        private readonly RecipesPanelManager _panelManager;

        private GameObject _overlayObject;
        private GameObject _scrollViewObject;
        private RectTransform _contentRect;
        private bool _isOpen;

        private RecipesContentManager _lastContentManager;

        private Text _fontSizeValueText;
        private Slider _fontSizeSlider;
        private Text _uiScaleValueText;
        private Slider _uiScaleSlider;
        private Text _edgeWidthValueText;
        private Slider _edgeWidthSlider;
        private Text _edgeHeightValueText;
        private Slider _edgeHeightSlider;
        private Text _hoverZoneValueText;
        private Slider _hoverZoneSlider;

        public bool IsOpen => _isOpen;

        public RecipesSettingsPanel(RecipesUIManager uiManager, RecipesPanelManager panelManager)
        {
            _uiManager = uiManager;
            _panelManager = panelManager;
        }

        public void Show(RecipesContentManager contentManager)
        {
            _lastContentManager = contentManager;

            if (_overlayObject != null)
            {
                UnityEngine.Object.Destroy(_overlayObject);
            }

            BuildOverlay(contentManager);
            _isOpen = true;
        }

        public void Hide()
        {
            if (_overlayObject != null)
            {
                UnityEngine.Object.Destroy(_overlayObject);
                _overlayObject = null;
            }
            _isOpen = false;
            _lastContentManager = null;
        }

        private void BuildOverlay(RecipesContentManager contentManager)
        {
            _overlayObject = new GameObject("SettingsOverlay");
            _overlayObject.transform.SetParent(_uiManager.PanelObject.transform, false);

            RectTransform overlayRect = _overlayObject.AddComponent<RectTransform>();
            overlayRect.anchorMin = new Vector2(0.1f, 0.1f);
            overlayRect.anchorMax = new Vector2(0.9f, 0.9f);
            overlayRect.offsetMin = new Vector2(0, RecipesUIManager.ResizeHandleHeight);
            overlayRect.offsetMax = new Vector2(0, -RecipesUIManager.ResizeHandleHeight);

            Image overlayBg = _overlayObject.AddComponent<Image>();
            overlayBg.color = new Color(0.08f, 0.08f, 0.08f, 0.99f);
            overlayBg.raycastTarget = true;

            _scrollViewObject = new GameObject("SettingsScrollView");
            _scrollViewObject.transform.SetParent(_overlayObject.transform, false);

            RectTransform scrollRect = _scrollViewObject.AddComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(5, 5);
            scrollRect.offsetMax = new Vector2(-5, -5);

            _scrollViewObject.AddComponent<RectMask2D>();

            ScrollRect scroll = _scrollViewObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 40f;

            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(_scrollViewObject.transform, false);

            RectTransform viewportRect = viewportObj.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;

            viewportObj.AddComponent<RectMask2D>();

            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);

            _contentRect = contentObj.AddComponent<RectTransform>();
            _contentRect.anchorMin = new Vector2(0, 1);
            _contentRect.anchorMax = new Vector2(1, 1);
            _contentRect.pivot = new Vector2(0.5f, 1);
            _contentRect.anchoredPosition = Vector2.zero;
            _contentRect.sizeDelta = new Vector2(0, 0);

            VerticalLayoutGroup layout = contentObj.AddComponent<VerticalLayoutGroup>();
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 4f;
            layout.padding = new RectOffset(10, 10, 8, 8);

            ContentSizeFitter fitter = contentObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = _contentRect;
            scroll.viewport = viewportRect;

            int fontSize = Mathf.Max(14, RecipesUIManager.ExpandedFontSize);
            float uiScale = BeefsRecipesPlugin.UIScaleMultiplier.Value;

            GameObject closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(_overlayObject.transform, false);

            RectTransform closeRect = closeObj.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 1);
            closeRect.anchorMax = new Vector2(1, 1);
            closeRect.pivot = new Vector2(1, 1);
            int closeSize = Mathf.RoundToInt(28 * uiScale);
            closeRect.anchoredPosition = new Vector2(-4, -4);
            closeRect.sizeDelta = new Vector2(closeSize, closeSize);

            Image closeBg = closeObj.AddComponent<Image>();
            closeBg.color = new Color(0.6f, 0.2f, 0.2f, 0.8f);
            closeBg.raycastTarget = true;

            Button closeBtn = closeObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() => Hide());

            ColorBlock closeBtnColors = closeBtn.colors;
            closeBtnColors.highlightedColor = new Color(0.8f, 0.3f, 0.3f, 1f);
            closeBtnColors.pressedColor = new Color(1f, 0.4f, 0.4f, 1f);
            closeBtn.colors = closeBtnColors;

            GameObject closeTextObj = new GameObject("Text");
            closeTextObj.transform.SetParent(closeObj.transform, false);

            RectTransform closeTextRect = closeTextObj.AddComponent<RectTransform>();
            closeTextRect.anchorMin = Vector2.zero;
            closeTextRect.anchorMax = Vector2.one;
            closeTextRect.offsetMin = Vector2.zero;
            closeTextRect.offsetMax = Vector2.zero;

            Text closeText = closeTextObj.AddComponent<Text>();
            closeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            closeText.text = "X";
            closeText.fontSize = Mathf.RoundToInt(16 * uiScale);
            closeText.color = Color.white;
            closeText.fontStyle = FontStyle.Bold;
            closeText.alignment = TextAnchor.MiddleCenter;
            closeText.raycastTarget = false;

            CreateSectionHeader("SETTINGS", fontSize + 2);
            CreateSeparator();

            CreateSectionHeader("Display", fontSize);

            _fontSizeSlider = CreateSliderRow("Font size offset", -6, 16,
                contentManager?.FontSizeOffset ?? 0, 0, fontSize,
                (val) =>
                {
                    if (contentManager != null)
                        contentManager.FontSizeOffset = Mathf.RoundToInt(val);
                },
                out _fontSizeValueText);

            _uiScaleSlider = CreateSliderRow("UI scale", 0.5f, 3.0f,
                BeefsRecipesPlugin.UIScaleMultiplier.Value, 1.0f, fontSize,
                (val) =>
                {
                    BeefsRecipesPlugin.UIScaleMultiplier.Value = Mathf.Round(val * 20f) / 20f;
                },
                out _uiScaleValueText, deferApply: true);

            CreateSeparator();
            CreateSectionHeader("Edge bar", fontSize);

            _edgeWidthSlider = CreateSliderRow("Width", 1.0f, 5.0f,
                BeefsRecipesPlugin.EdgeBarWidthMultiplier.Value, 1.0f, fontSize,
                (val) =>
                {
                    BeefsRecipesPlugin.EdgeBarWidthMultiplier.Value = Mathf.Round(val * 10f) / 10f;
                },
                out _edgeWidthValueText);

            _edgeHeightSlider = CreateSliderRow("Height", 0.5f, 5.0f,
                BeefsRecipesPlugin.EdgeBarHeightMultiplier.Value, 1.0f, fontSize,
                (val) =>
                {
                    BeefsRecipesPlugin.EdgeBarHeightMultiplier.Value = Mathf.Round(val * 10f) / 10f;
                },
                out _edgeHeightValueText);

            _hoverZoneSlider = CreateSliderRow("Hover zone", 10f, 100f,
                BeefsRecipesPlugin.HoverZoneWidth.Value, 20f, fontSize,
                (val) =>
                {
                    BeefsRecipesPlugin.HoverZoneWidth.Value = Mathf.Round(val);
                },
                out _hoverZoneValueText);

            CreateSeparator();
            CreateSectionHeader("Notes panel", fontSize);

            CreateSliderRow("Drag bar width", 2f, 20f,
                BeefsRecipesPlugin.DragBarWidth.Value, 5f, fontSize,
                (val) =>
                {
                    BeefsRecipesPlugin.DragBarWidth.Value = Mathf.Round(val);
                },
                out _);

            if (BeefsRecipesPlugin.RuntimeContext.IsMultiplayer)
            {
                CreateSeparator();
                CreateSectionHeader("Multiplayer", fontSize);

                CreateAccentColorRow(fontSize);

                var hiddenItems = contentManager?.GetHiddenSectionInfo() ?? new List<(string, string)>();
                if (hiddenItems.Count > 0)
                {
                    CreateSeparator();
                    CreateSectionHeader($"Hidden notes ({hiddenItems.Count})", fontSize);

                    foreach (var (id, label) in hiddenItems)
                    {
                        CreateUnhideRow(id, label, fontSize, contentManager);
                    }
                }
            }

            CreateSeparator();
            CreateInfoRow($"BeefsRecipes v{PluginInfo.PLUGIN_VERSION}", fontSize - 2);

            _overlayObject.transform.SetAsLastSibling();
        }

        private void CreateSectionHeader(string text, int fontSize)
        {
            GameObject obj = new GameObject("Header");
            obj.transform.SetParent(_contentRect, false);

            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = fontSize + 4;

            Text t = obj.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = text;
            t.fontSize = fontSize;
            t.color = new Color(1f, 0.39f, 0.08f, 0.9f);
            t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleLeft;
            t.raycastTarget = false;
        }

        private void CreateSeparator()
        {
            GameObject obj = new GameObject("Separator");
            obj.transform.SetParent(_contentRect, false);

            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = 1;
            le.flexibleWidth = 1;

            Image img = obj.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.1f);
            img.raycastTarget = false;
        }

        private Slider CreateSliderRow(string label, float min, float max, float initial,
            float defaultValue, int fontSize, Action<float> onChanged, out Text valueText,
            bool deferApply = false)
        {
            GameObject rowObj = new GameObject($"Row_{label}");
            rowObj.transform.SetParent(_contentRect, false);

            HorizontalLayoutGroup rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.spacing = 6f;
            rowLayout.padding = new RectOffset(4, 4, 0, 0);
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            LayoutElement rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredHeight = fontSize + 6;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(rowObj.transform, false);

            Text labelText = labelObj.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.text = label;
            labelText.fontSize = fontSize;
            labelText.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.raycastTarget = false;

            LayoutElement labelLE = labelObj.AddComponent<LayoutElement>();
            labelLE.preferredWidth = Mathf.Max(60f, 120f * RecipesUIManager.ScaleFactor) * BeefsRecipesPlugin.UIScaleMultiplier.Value;
            labelLE.flexibleWidth = 0;

            GameObject sliderObj = CreateSliderObject(rowObj.transform, min, max, initial);
            Slider slider = sliderObj.GetComponent<Slider>();

            LayoutElement sliderLE = sliderObj.AddComponent<LayoutElement>();
            sliderLE.flexibleWidth = 1;
            sliderLE.preferredHeight = fontSize + 4;

            GameObject valueObj = new GameObject("Value");
            valueObj.transform.SetParent(rowObj.transform, false);

            Image valueBg = valueObj.AddComponent<Image>();
            valueBg.color = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            valueBg.raycastTarget = true;

            InputField valueInput = valueObj.AddComponent<InputField>();
            valueInput.contentType = InputField.ContentType.DecimalNumber;

            GameObject valueTextObj = new GameObject("Text");
            valueTextObj.transform.SetParent(valueObj.transform, false);

            RectTransform valueTextRect = valueTextObj.AddComponent<RectTransform>();
            valueTextRect.anchorMin = Vector2.zero;
            valueTextRect.anchorMax = Vector2.one;
            valueTextRect.offsetMin = new Vector2(2, 0);
            valueTextRect.offsetMax = new Vector2(-2, 0);

            valueText = valueTextObj.AddComponent<Text>();
            valueText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            valueText.fontSize = fontSize;
            valueText.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            valueText.alignment = TextAnchor.MiddleRight;
            valueText.raycastTarget = false;

            valueInput.textComponent = valueText;

            var focusHandler = valueObj.AddComponent<SettingsInputFocusHandler>();
            focusHandler.Init(_panelManager);

            LayoutElement valueLE = valueObj.AddComponent<LayoutElement>();
            valueLE.preferredWidth = Mathf.Max(35f, 50f * RecipesUIManager.ScaleFactor) * BeefsRecipesPlugin.UIScaleMultiplier.Value;
            valueLE.flexibleWidth = 0;

            FormatSliderValue(valueText, initial, min, max, valueInput);

            Text capturedValueText = valueText;
            InputField capturedValueInput = valueInput;
            Slider capturedSlider = slider;
            float capturedMin = min;
            float capturedMax = max;

            valueInput.onEndEdit.AddListener((text) =>
            {
                if (float.TryParse(text, out float val))
                {
                    val = Mathf.Clamp(val, capturedMin, capturedMax);
                    capturedSlider.value = val;
                }
                FormatSliderValue(capturedValueText, capturedSlider.value, capturedMin, capturedMax, capturedValueInput);
            });
            GameObject resetObj = new GameObject("Reset");
            resetObj.transform.SetParent(rowObj.transform, false);

            LayoutElement resetLE = resetObj.AddComponent<LayoutElement>();
            resetLE.preferredWidth = fontSize + 2;
            resetLE.preferredHeight = fontSize + 2;
            resetLE.flexibleWidth = 0;

            Image resetBg = resetObj.AddComponent<Image>();
            resetBg.color = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            resetBg.raycastTarget = true;

            Button resetBtn = resetObj.AddComponent<Button>();
            resetBtn.targetGraphic = resetBg;
            resetBtn.onClick.AddListener(() =>
            {
                capturedSlider.value = defaultValue;
            });

            GameObject resetTextObj = new GameObject("Text");
            resetTextObj.transform.SetParent(resetObj.transform, false);

            RectTransform resetTextRect = resetTextObj.AddComponent<RectTransform>();
            resetTextRect.anchorMin = Vector2.zero;
            resetTextRect.anchorMax = Vector2.one;
            resetTextRect.offsetMin = Vector2.zero;
            resetTextRect.offsetMax = Vector2.zero;

            Text resetText = resetTextObj.AddComponent<Text>();
            resetText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            resetText.text = "R";
            resetText.fontSize = Mathf.Max(10, fontSize - 3);
            resetText.color = new Color(0.6f, 0.6f, 0.6f, 0.7f);
            resetText.alignment = TextAnchor.MiddleCenter;
            resetText.raycastTarget = false;

            if (deferApply)
            {
                slider.onValueChanged.AddListener((val) =>
                {
                    FormatSliderValue(capturedValueText, val, min, max, capturedValueInput);
                });

                EventTrigger trigger = sliderObj.AddComponent<EventTrigger>();
                EventTrigger.Entry pointerUp = new EventTrigger.Entry();
                pointerUp.eventID = EventTriggerType.PointerUp;
                pointerUp.callback.AddListener((data) => onChanged?.Invoke(capturedSlider.value));
                trigger.triggers.Add(pointerUp);

                resetBtn.onClick.AddListener(() => onChanged?.Invoke(defaultValue));

                capturedValueInput.onEndEdit.AddListener((text) =>
                {
                    onChanged?.Invoke(capturedSlider.value);
                });
            }
            else
            {
                slider.onValueChanged.AddListener((val) =>
                {
                    FormatSliderValue(capturedValueText, val, min, max, capturedValueInput);
                    onChanged?.Invoke(val);
                });
            }

            return slider;
        }

        private static void FormatSliderValue(Text text, float val, float min, float max, InputField inputField = null)
        {
            string formatted;
            if (max <= 5f && min >= 0.4f)
                formatted = Mathf.Round(val * 10f) / 10f + "";
            else if (max <= 16 && min >= -6)
                formatted = Mathf.RoundToInt(val).ToString();
            else
                formatted = Mathf.RoundToInt(val).ToString() + "px";

            text.text = formatted;
            if (inputField != null)
                inputField.text = formatted;
        }

        private GameObject CreateSliderObject(Transform parent, float min, float max, float initial)
        {
            GameObject sliderObj = new GameObject("Slider");
            sliderObj.transform.SetParent(parent, false);

            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(0, 20);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = initial;
            slider.wholeNumbers = false;

            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);

            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.35f);
            bgRect.anchorMax = new Vector2(1, 0.65f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);

            GameObject fillAreaObj = new GameObject("FillArea");
            fillAreaObj.transform.SetParent(sliderObj.transform, false);

            RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0, 0.35f);
            fillAreaRect.anchorMax = new Vector2(1, 0.65f);
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);

            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.color = new Color(1f, 0.39f, 0.08f, 0.7f);

            slider.fillRect = fillRect;

            GameObject handleAreaObj = new GameObject("HandleSlideArea");
            handleAreaObj.transform.SetParent(sliderObj.transform, false);

            RectTransform handleAreaRect = handleAreaObj.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0, 0);
            handleAreaRect.anchorMax = new Vector2(1, 1);
            handleAreaRect.offsetMin = new Vector2(5, 0);
            handleAreaRect.offsetMax = new Vector2(-5, 0);

            GameObject handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(handleAreaObj.transform, false);

            RectTransform handleRect = handleObj.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(12, 0);

            Image handleImage = handleObj.AddComponent<Image>();
            handleImage.color = new Color(1f, 0.5f, 0.15f, 1f);

            slider.targetGraphic = handleImage;
            slider.handleRect = handleRect;

            return sliderObj;
        }

        private void CreateAccentColorRow(int fontSize)
        {
            var syncManager = BeefsRecipesPlugin.Instance?.ClientSyncManager;
            if (syncManager == null) return;

            ulong localId = 0;
            try { localId = Assets.Scripts.Networking.NetworkManager.LocalClientId; } catch { }
            string myHex = syncManager.GetPlayerColor(localId);
            Color myAccent = new Color(0.5f, 0.8f, 1f);
            if (!string.IsNullOrEmpty(myHex))
                ColorUtility.TryParseHtmlString(myHex, out myAccent);

            string currentOverride = syncManager.GetAccentColorOverride();
            bool hasOverride = !string.IsNullOrEmpty(currentOverride);

            // --- Row 1: label + swatch bar ---
            GameObject rowObj = new GameObject("AccentColorRow");
            rowObj.transform.SetParent(_contentRect, false);

            HorizontalLayoutGroup rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.spacing = 8f;
            rowLayout.padding = new RectOffset(4, 4, 0, 0);
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            LayoutElement rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredHeight = fontSize + 6;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(rowObj.transform, false);

            Text labelText = labelObj.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.text = "Badge color";
            labelText.fontSize = fontSize;
            labelText.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.raycastTarget = false;

            LayoutElement labelLE = labelObj.AddComponent<LayoutElement>();
            labelLE.preferredWidth = 120 * BeefsRecipesPlugin.UIScaleMultiplier.Value;

            float swatchWidth = 80 * BeefsRecipesPlugin.UIScaleMultiplier.Value;
            int swatchHeight = fontSize + 2;
            GameObject swatchObj = new GameObject("AccentSwatch");
            swatchObj.transform.SetParent(rowObj.transform, false);

            LayoutElement swatchLE = swatchObj.AddComponent<LayoutElement>();
            swatchLE.preferredWidth = swatchWidth;
            swatchLE.preferredHeight = swatchHeight;

            Image swatchImage = swatchObj.AddComponent<Image>();
            swatchImage.color = myAccent;
            swatchImage.raycastTarget = true;

            Button swatchButton = swatchObj.AddComponent<Button>();
            swatchButton.targetGraphic = swatchImage;

            // --- Row 2: status subtitle (both states created, toggled dynamically) ---
            GameObject subtitleObj = new GameObject("AccentSubtitle");
            subtitleObj.transform.SetParent(_contentRect, false);

            LayoutElement subtitleLE = subtitleObj.AddComponent<LayoutElement>();
            subtitleLE.preferredHeight = fontSize;

            HorizontalLayoutGroup subtitleLayout = subtitleObj.AddComponent<HorizontalLayoutGroup>();
            subtitleLayout.childControlWidth = false;
            subtitleLayout.childControlHeight = true;
            subtitleLayout.childForceExpandWidth = false;
            subtitleLayout.spacing = 0;
            subtitleLayout.padding = new RectOffset(
                4 + Mathf.RoundToInt(120 * BeefsRecipesPlugin.UIScaleMultiplier.Value) + 8,
                4, 0, 0);
            subtitleLayout.childAlignment = TextAnchor.MiddleLeft;

            int subFontSize = Mathf.Max(10, fontSize - 2);

            // -- Suit color state --
            GameObject suitLabel = new GameObject("SuitLabel");
            suitLabel.transform.SetParent(subtitleObj.transform, false);

            Text suitText = suitLabel.AddComponent<Text>();
            suitText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            suitText.text = "Mirroring suit color";
            suitText.fontSize = subFontSize;
            suitText.color = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            suitText.alignment = TextAnchor.MiddleLeft;
            suitText.raycastTarget = false;

            LayoutElement suitLE = suitLabel.AddComponent<LayoutElement>();
            suitLE.preferredWidth = suitText.preferredWidth + 4;

            // -- Custom override state --
            GameObject customGroup = new GameObject("CustomGroup");
            customGroup.transform.SetParent(subtitleObj.transform, false);

            HorizontalLayoutGroup customLayout = customGroup.AddComponent<HorizontalLayoutGroup>();
            customLayout.childControlWidth = false;
            customLayout.childControlHeight = true;
            customLayout.childForceExpandWidth = false;
            customLayout.spacing = 0;
            customLayout.childAlignment = TextAnchor.MiddleLeft;

            GameObject customLabel = new GameObject("CustomLabel");
            customLabel.transform.SetParent(customGroup.transform, false);

            Text customText = customLabel.AddComponent<Text>();
            customText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            customText.text = "Custom \u00b7 ";
            customText.fontSize = subFontSize;
            customText.color = new Color(0.55f, 0.55f, 0.55f, 0.8f);
            customText.alignment = TextAnchor.MiddleLeft;
            customText.raycastTarget = false;

            LayoutElement customLE = customLabel.AddComponent<LayoutElement>();
            customLE.preferredWidth = customText.preferredWidth + 4;

            GameObject resetObj = new GameObject("ResetLink");
            resetObj.transform.SetParent(customGroup.transform, false);

            Text resetText = resetObj.AddComponent<Text>();
            resetText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            resetText.text = "Use suit color";
            resetText.fontSize = subFontSize;
            resetText.color = new Color(0.7f, 0.55f, 0.3f, 0.9f);
            resetText.alignment = TextAnchor.MiddleLeft;
            resetText.raycastTarget = true;

            LayoutElement resetLE = resetObj.AddComponent<LayoutElement>();
            resetLE.preferredWidth = resetText.preferredWidth + 4;

            Button resetBtn = resetObj.AddComponent<Button>();
            resetBtn.targetGraphic = resetText;
            resetBtn.transition = Selectable.Transition.ColorTint;
            ColorBlock resetColors = resetBtn.colors;
            resetColors.normalColor = resetText.color;
            resetColors.highlightedColor = new Color(1f, 0.7f, 0.4f, 1f);
            resetColors.pressedColor = new Color(1f, 0.8f, 0.5f, 1f);
            resetBtn.colors = resetColors;

            resetBtn.onClick.AddListener(() =>
            {
                syncManager.SetAccentColorOverride(null);
                string newHex = syncManager.GetPlayerColor(localId);
                Color newColor = new Color(0.5f, 0.8f, 1f);
                if (!string.IsNullOrEmpty(newHex))
                    ColorUtility.TryParseHtmlString(newHex, out newColor);
                swatchImage.color = newColor;
                suitLabel.SetActive(true);
                customGroup.SetActive(false);
            });

            suitLabel.SetActive(!hasOverride);
            customGroup.SetActive(hasOverride);

            swatchButton.onClick.AddListener(() =>
            {
                RecipesColorPicker.Show(Input.mousePosition, swatchImage.color, (selectedColor) =>
                {
                    string hex = selectedColor == Color.clear
                        ? null
                        : "#" + ColorUtility.ToHtmlStringRGB(selectedColor);
                    syncManager.SetAccentColorOverride(hex);

                    string newHex = syncManager.GetPlayerColor(localId);
                    Color newColor = new Color(0.5f, 0.8f, 1f);
                    if (!string.IsNullOrEmpty(newHex))
                        ColorUtility.TryParseHtmlString(newHex, out newColor);
                    swatchImage.color = newColor;
                    suitLabel.SetActive(false);
                    customGroup.SetActive(true);
                });
            });
        }

        private void CreateUnhideRow(string sectionId, string label, int fontSize,
            RecipesContentManager contentManager)
        {
            GameObject rowObj = new GameObject($"Unhide_{sectionId.Substring(0, 8)}");
            rowObj.transform.SetParent(_contentRect, false);

            HorizontalLayoutGroup rowLayout = rowObj.AddComponent<HorizontalLayoutGroup>();
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = false;
            rowLayout.spacing = 6f;
            rowLayout.padding = new RectOffset(8, 4, 2, 2);
            rowLayout.childAlignment = TextAnchor.MiddleLeft;

            LayoutElement rowLE = rowObj.AddComponent<LayoutElement>();
            rowLE.preferredHeight = fontSize + 6;

            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(rowObj.transform, false);

            Text labelText = labelObj.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.text = label;
            labelText.fontSize = Mathf.Max(10, fontSize - 1);
            labelText.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
            labelText.raycastTarget = false;

            LayoutElement labelLE = labelObj.AddComponent<LayoutElement>();
            labelLE.flexibleWidth = 1;

            GameObject btnObj = new GameObject("UnhideBtn");
            btnObj.transform.SetParent(rowObj.transform, false);

            LayoutElement btnLE = btnObj.AddComponent<LayoutElement>();
            btnLE.preferredWidth = 55 * BeefsRecipesPlugin.UIScaleMultiplier.Value;
            btnLE.preferredHeight = fontSize + 4;

            Image btnBg = btnObj.AddComponent<Image>();
            btnBg.color = new Color(0.2f, 0.5f, 0.3f, 0.7f);
            btnBg.raycastTarget = true;

            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = btnBg;

            GameObject btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);

            RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;

            Text btnText = btnTextObj.AddComponent<Text>();
            btnText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            btnText.text = "unhide";
            btnText.fontSize = Mathf.Max(10, fontSize - 2);
            btnText.color = Color.white;
            btnText.alignment = TextAnchor.MiddleCenter;
            btnText.raycastTarget = false;

            string capturedId = sectionId;
            btn.onClick.AddListener(() =>
            {
                contentManager?.UnhideSectionById(capturedId);
                if (_lastContentManager != null)
                {
                    Show(_lastContentManager);
                }
            });
        }

        private void CreateInfoRow(string text, int fontSize)
        {
            GameObject obj = new GameObject("Info");
            obj.transform.SetParent(_contentRect, false);

            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = fontSize + 6;

            Text t = obj.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = text;
            t.fontSize = fontSize;
            t.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            t.fontStyle = FontStyle.Italic;
            t.alignment = TextAnchor.MiddleLeft;
            t.raycastTarget = false;
        }
    }

    public class SettingsInputFocusHandler : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private RecipesPanelManager _panelManager;
        private bool _wasFocused;

        public void Init(RecipesPanelManager panelManager)
        {
            _panelManager = panelManager;
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (_panelManager == null) return;
            if (!_panelManager.IsEditing)
            {
                _wasFocused = true;
                _panelManager.SetEditingMode(true);
            }
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (_panelManager == null) return;
            if (_wasFocused)
            {
                _wasFocused = false;
                _panelManager.SetEditingMode(false);
            }
        }
    }
}