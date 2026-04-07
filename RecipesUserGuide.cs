using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace BeefsRecipes
{
    public class RecipesUserGuide
    {
        private readonly RecipesUIManager _uiManager;
        private readonly RecipesPanelManager _panelManager;

        private GameObject _overlayObject;
        private GameObject _popoutObject;
        private bool _isOpen;

        public bool IsOpen => _isOpen;

        private const int HeadingOffset = -4;

        private static readonly Dictionary<string, Texture2D> _imageCache = new Dictionary<string, Texture2D>();
        private static string _guidePath;

        public RecipesUserGuide(RecipesUIManager uiManager, RecipesPanelManager panelManager)
        {
            _uiManager = uiManager;
            _panelManager = panelManager;
        }

        public void Show()
        {
            if (_overlayObject != null)
                Object.Destroy(_overlayObject);

            BuildOverlay();
            _isOpen = true;
        }

        public void Hide()
        {
            DismissImagePopout();
            if (_overlayObject != null)
            {
                Object.Destroy(_overlayObject);
                _overlayObject = null;
            }
            _isOpen = false;
        }

        public void Toggle()
        {
            if (_isOpen) Hide();
            else Show();
        }

        private void BuildOverlay()
        {
            float uiScale = BeefsRecipesPlugin.UIScaleMultiplier.Value;
            int baseFontSize = Mathf.Max(12, RecipesUIManager.ExpandedFontSize);

            _overlayObject = new GameObject("UserGuideOverlay");
            _overlayObject.transform.SetParent(_uiManager.PanelObject.transform, false);

            RectTransform overlayRect = _overlayObject.AddComponent<RectTransform>();
            overlayRect.anchorMin = new Vector2(0.02f, 0.02f);
            overlayRect.anchorMax = new Vector2(0.98f, 0.98f);
            overlayRect.offsetMin = new Vector2(0, RecipesUIManager.ResizeHandleHeight);
            overlayRect.offsetMax = new Vector2(0, -RecipesUIManager.ResizeHandleHeight);

            Image overlayBg = _overlayObject.AddComponent<Image>();
            overlayBg.color = new Color(0.05f, 0.05f, 0.07f, 0.99f);
            overlayBg.raycastTarget = true;

            CreateCloseButton(uiScale);

            GameObject scrollObj = new GameObject("ScrollView");
            scrollObj.transform.SetParent(_overlayObject.transform, false);

            float scrollbarWidth = Mathf.RoundToInt(14 * uiScale);

            RectTransform scrollRect = scrollObj.AddComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(8, 8);
            scrollRect.offsetMax = new Vector2(-8, -Mathf.RoundToInt(36 * uiScale));

            ScrollRect scroll = scrollObj.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 40f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(scrollObj.transform, false);

            RectTransform viewportRt = viewportObj.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = new Vector2(-scrollbarWidth - 4, 0);

            Image viewportImg = viewportObj.AddComponent<Image>();
            viewportImg.color = new Color(0, 0, 0, 0);
            viewportImg.raycastTarget = true;

            viewportObj.AddComponent<RectMask2D>();

            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);

            RectTransform contentRt = contentObj.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1);
            contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            HorizontalLayoutGroup hlg = contentObj.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = true;
            hlg.childForceExpandHeight = false;
            hlg.spacing = 0;
            hlg.padding = new RectOffset(0, 0, 0, 0);

            ContentSizeFitter contentFitter = contentObj.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject scrollbarObj = new GameObject("Scrollbar");
            scrollbarObj.transform.SetParent(scrollObj.transform, false);

            RectTransform scrollbarRt = scrollbarObj.AddComponent<RectTransform>();
            scrollbarRt.anchorMin = new Vector2(1, 0);
            scrollbarRt.anchorMax = new Vector2(1, 1);
            scrollbarRt.pivot = new Vector2(1, 0.5f);
            scrollbarRt.anchoredPosition = Vector2.zero;
            scrollbarRt.sizeDelta = new Vector2(scrollbarWidth, 0);

            Image scrollbarBg = scrollbarObj.AddComponent<Image>();
            scrollbarBg.color = new Color(0.1f, 0.1f, 0.1f, 0.4f);
            scrollbarBg.raycastTarget = true;

            GameObject handleArea = new GameObject("HandleArea");
            handleArea.transform.SetParent(scrollbarObj.transform, false);

            RectTransform handleAreaRt = handleArea.AddComponent<RectTransform>();
            handleAreaRt.anchorMin = Vector2.zero;
            handleAreaRt.anchorMax = Vector2.one;
            handleAreaRt.offsetMin = new Vector2(2, 2);
            handleAreaRt.offsetMax = new Vector2(-2, -2);

            GameObject handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(handleArea.transform, false);

            RectTransform handleRt = handleObj.AddComponent<RectTransform>();
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = Vector2.zero;
            handleRt.offsetMax = Vector2.zero;

            Image handleImg = handleObj.AddComponent<Image>();
            handleImg.color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            handleImg.raycastTarget = true;

            Scrollbar scrollbar = scrollbarObj.AddComponent<Scrollbar>();
            scrollbar.handleRect = handleRt;
            scrollbar.targetGraphic = handleImg;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            ColorBlock sbColors = scrollbar.colors;
            sbColors.normalColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            sbColors.highlightedColor = new Color(0.65f, 0.65f, 0.65f, 0.8f);
            sbColors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 0.9f);
            scrollbar.colors = sbColors;

            scroll.content = contentRt;
            scroll.viewport = viewportRt;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;

            GameObject mainCol = CreateColumn(contentObj.transform, "MainColumn", 1f);

            PopulateGuideContent(mainCol.transform, baseFontSize, uiScale);

            _overlayObject.transform.SetAsLastSibling();
        }

        private void CreateCloseButton(float uiScale)
        {
            GameObject closeObj = new GameObject("CloseButton");
            closeObj.transform.SetParent(_overlayObject.transform, false);

            int closeSize = Mathf.RoundToInt(28 * uiScale);

            RectTransform closeRect = closeObj.AddComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1, 1);
            closeRect.anchorMax = new Vector2(1, 1);
            closeRect.pivot = new Vector2(1, 1);
            closeRect.anchoredPosition = new Vector2(-4, -4);
            closeRect.sizeDelta = new Vector2(closeSize, closeSize);

            Image closeBg = closeObj.AddComponent<Image>();
            closeBg.color = new Color(0.6f, 0.2f, 0.2f, 0.8f);
            closeBg.raycastTarget = true;

            Button closeBtn = closeObj.AddComponent<Button>();
            closeBtn.targetGraphic = closeBg;
            closeBtn.onClick.AddListener(() => _panelManager.CloseGuide());

            ColorBlock colors = closeBtn.colors;
            colors.highlightedColor = new Color(0.8f, 0.3f, 0.3f, 1f);
            colors.pressedColor = new Color(1f, 0.4f, 0.4f, 1f);
            closeBtn.colors = colors;

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
        }

        private GameObject CreateColumn(Transform parent, string name, float flexWeight)
        {
            GameObject column = new GameObject(name);
            column.transform.SetParent(parent, false);

            LayoutElement colLE = column.AddComponent<LayoutElement>();
            colLE.flexibleWidth = flexWeight;

            VerticalLayoutGroup vlg = column.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(8, 8, 4, 8);

            ContentSizeFitter csf = column.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return column;
        }

        private void PopulateGuideContent(Transform content, int fontSize, float uiScale)
        {
            AddRichTextBlock(content, TitleBlock, fontSize + 2, uiScale);

            // --- GETTING STARTED ---
            AddSectionBanner(content, "GETTING STARTED", new Color(0.5f, 0.75f, 1f, 0.9f), fontSize, uiScale);
            AddRichTextBlock(content, GettingStarted_PanelModes, fontSize, uiScale);
            AddInlineImage(content, "Panel Sidebar Modes",
                "The four sidebar states: Hidden \u2192 Peeking \u2192 Locked \u2192 Expanded",
                0.8f, uiScale);
            AddRichTextBlock(content, GettingStarted_Editing, fontSize, uiScale);
            AddInlineImage(content, "Fullscreen Mode",
                "Fullscreen two-column layout with personal notes left, shared right",
                0.8f, uiScale);
            AddRichTextBlock(content, GettingStarted_AddingNotes, fontSize, uiScale);
            AddInlineImage(content, "Insert Buttons",
                "The +Note and +Draw buttons between notes",
                0.6f, uiScale);

            // --- NOTES CONTENT ---
            AddSectionBanner(content, "NOTES CONTENT", new Color(0.4f, 0.9f, 0.5f, 0.9f), fontSize, uiScale);
            AddRichTextBlock(content, Content_Markdown, fontSize, uiScale);
            AddInlineImage(content, "Markdown: Source vs Rendered",
                "Raw markdown on the left, formatted output on the right",
                0.8f, uiScale);
            AddRichTextBlock(content, Content_Drawings, fontSize, uiScale);
            AddInlineImage(content, "Drawing Toolbar",
                "The drawing toolbar with color, size, eraser, and action buttons",
                0.75f, uiScale);
            AddRichTextBlock(content, Content_Organizing, fontSize, uiScale);
            AddInlineImage(content, "Drag Handle Interactions",
                "Drag to reorder, double-click to collapse (red = collapsed)",
                0.75f, uiScale);
            AddRichTextBlock(content, Content_Colors, fontSize, uiScale);

            // --- MULTIPLAYER ---
            AddSectionBanner(content, "MULTIPLAYER", new Color(1f, 0.7f, 0.4f, 0.9f), fontSize, uiScale);
            AddRichTextBlock(content, Multi_Sharing, fontSize, uiScale);
            AddRichTextBlock(content, Multi_Hiding, fontSize, uiScale);
            AddRichTextBlock(content, Multi_Voting, fontSize, uiScale);
            AddRichTextBlock(content, Multi_AccentColors, fontSize, uiScale);

            // --- REFERENCE ---
            AddSectionBanner(content, "REFERENCE", new Color(0.7f, 0.7f, 1f, 0.9f), fontSize, uiScale);
            AddRichTextBlock(content, Ref_Shortcuts, fontSize, uiScale);
            AddRichTextBlock(content, Ref_Settings, fontSize, uiScale);
            AddInlineImage(content, "Settings Panel",
                "The settings overlay with sliders, input fields, and reset buttons",
                0.7f, uiScale);
            AddRichTextBlock(content, Ref_Saving, fontSize, uiScale);
            AddRichTextBlock(content, Ref_Tips, fontSize, uiScale);
            AddInlineImage(content, "Context Menu",
                "Right-click menu with all available actions",
                0.55f, uiScale);

            // Reference cards at the bottom
            AddVisualCard(content, "Keyboard Shortcuts",
                "Editing\n" +
                "\u2022 Tab - next field\n" +
                "\u2022 Shift+Tab - previous field\n" +
                "\u2022 Enter - title \u2192 content\n" +
                "\u2022 Escape - exit / close\n" +
                "\u2022 Ctrl+Scroll - font size\n\n" +
                "Navigation\n" +
                "\u2022 Right-click - context menu\n" +
                "\u2022 Dbl-click drag bar - collapse\n" +
                "\u2022 Dbl-click panel - toggle edit",
                new Color(0.15f, 0.15f, 0.2f, 0.95f),
                new Color(0.7f, 0.7f, 1f, 0.9f),
                fontSize, uiScale);

            AddVisualCard(content, "Markdown Cheat Sheet",
                "### Headings\n" +
                "# H1  ## H2  ### H3\n\n" +
                "### Text Style\n" +
                "**bold**  *italic*  ***both***\n\n" +
                "### Lists & Tasks\n" +
                "- [ ] Unchecked  - [x] Done\n" +
                "- Bullet  1. Numbered\n\n" +
                "### Other\n" +
                "> Blockquote\n" +
                "==highlight==  ---  rule",
                new Color(0.1f, 0.15f, 0.1f, 0.95f),
                new Color(0.4f, 0.9f, 0.5f, 0.9f),
                fontSize, uiScale);
        }

        private void AddRichTextBlock(Transform parent, string markdown, int fontSize, float uiScale)
        {
            List<NotesMarkdownConverter.CheckboxInfo> checkboxes;
            string richText = NotesMarkdownConverter.MarkdownToUGUI(markdown, out checkboxes, HeadingOffset);

            GameObject obj = new GameObject("TextBlock");
            obj.transform.SetParent(parent, false);

            Text text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.color = new Color(0.82f, 0.82f, 0.82f, 1f);
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.text = richText;

            ContentSizeFitter fitter = obj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
        }

        private void AddSectionBanner(Transform parent, string title, Color color,
            int fontSize, float uiScale)
        {
            GameObject bannerObj = new GameObject("Banner_" + title);
            bannerObj.transform.SetParent(parent, false);

            int bannerHeight = Mathf.RoundToInt(28 * uiScale);

            Image bannerBg = bannerObj.AddComponent<Image>();
            bannerBg.color = new Color(color.r * 0.15f, color.g * 0.15f, color.b * 0.15f, 0.95f);
            bannerBg.raycastTarget = false;

            LayoutElement le = bannerObj.AddComponent<LayoutElement>();
            le.preferredHeight = bannerHeight;

            GameObject barObj = new GameObject("AccentBar");
            barObj.transform.SetParent(bannerObj.transform, false);

            RectTransform barRect = barObj.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0, 0);
            barRect.anchorMax = new Vector2(0, 1);
            barRect.pivot = new Vector2(0, 0.5f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(3, 0);

            Image barImg = barObj.AddComponent<Image>();
            barImg.color = color;
            barImg.raycastTarget = false;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(bannerObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 0);
            textRect.offsetMax = new Vector2(-8, 0);

            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = Mathf.Max(11, fontSize - 1);
            text.color = color;
            text.text = title;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;
        }

        private void AddVisualCard(Transform parent, string title, string body,
            Color bgColor, Color titleColor, int fontSize, float uiScale)
        {
            GameObject cardObj = new GameObject("Card_" + title.Replace(" ", ""));
            cardObj.transform.SetParent(parent, false);

            Image cardBg = cardObj.AddComponent<Image>();
            cardBg.color = bgColor;
            cardBg.raycastTarget = false;

            VerticalLayoutGroup vlg = cardObj.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 3f;
            int pad = Mathf.RoundToInt(8 * uiScale);
            vlg.padding = new RectOffset(pad, pad, pad, pad);

            ContentSizeFitter fitter = cardObj.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(cardObj.transform, false);

            Text titleText = titleObj.AddComponent<Text>();
            titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleText.fontSize = fontSize;
            titleText.color = titleColor;
            titleText.fontStyle = FontStyle.Bold;
            titleText.text = title;
            titleText.alignment = TextAnchor.UpperLeft;
            titleText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleText.verticalOverflow = VerticalWrapMode.Overflow;
            titleText.raycastTarget = false;

            ContentSizeFitter titleFitter = titleObj.AddComponent<ContentSizeFitter>();
            titleFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject lineObj = new GameObject("Line");
            lineObj.transform.SetParent(cardObj.transform, false);

            LayoutElement lineLE = lineObj.AddComponent<LayoutElement>();
            lineLE.preferredHeight = 1;
            lineLE.flexibleWidth = 1;

            Image lineImg = lineObj.AddComponent<Image>();
            lineImg.color = new Color(titleColor.r, titleColor.g, titleColor.b, 0.25f);
            lineImg.raycastTarget = false;

            GameObject bodyObj = new GameObject("Body");
            bodyObj.transform.SetParent(cardObj.transform, false);

            Text bodyText = bodyObj.AddComponent<Text>();
            bodyText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            bodyText.fontSize = Mathf.Max(10, fontSize - 2);
            bodyText.color = new Color(0.75f, 0.75f, 0.75f, 0.9f);
            bodyText.text = body;
            bodyText.alignment = TextAnchor.UpperLeft;
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.verticalOverflow = VerticalWrapMode.Overflow;
            bodyText.supportRichText = true;
            bodyText.raycastTarget = false;

            ContentSizeFitter bodyFitter = bodyObj.AddComponent<ContentSizeFitter>();
            bodyFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void AddImagePlaceholder(Transform parent, string label, string description,
            float height, float uiScale)
        {
            string imageKey = label.ToLowerInvariant().Replace(" ", "_").Replace(":", "");
            Texture2D texture = LoadGuideImage(imageKey);

            float aspect = 16f / 9f;
            if (texture != null)
                aspect = (float)texture.width / texture.height;

            GameObject obj = new GameObject("Placeholder_" + label.Replace(" ", ""));
            obj.transform.SetParent(parent, false);

            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.preferredHeight = height * uiScale;
            le.flexibleWidth = 1f;

            AspectRatioFitter arf = obj.AddComponent<AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            arf.aspectRatio = aspect;

            Image bg = obj.AddComponent<Image>();
            bg.raycastTarget = true;

            Outline outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.35f, 0.4f, 0.4f);
            outline.effectDistance = new Vector2(1, -1);

            Button btn = obj.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;

            string capturedLabel = label;
            string capturedDesc = description;
            Texture2D capturedTex = texture;
            float capturedScale = uiScale;

            if (texture != null)
            {
                bg.color = new Color(0.08f, 0.08f, 0.08f, 1f);

                GameObject imageObj = new GameObject("Image");
                imageObj.transform.SetParent(obj.transform, false);

                RectTransform imgRect = imageObj.AddComponent<RectTransform>();
                imgRect.anchorMin = Vector2.zero;
                imgRect.anchorMax = Vector2.one;
                imgRect.offsetMin = new Vector2(2, 2);
                imgRect.offsetMax = new Vector2(-2, -2);

                RawImage rawImg = imageObj.AddComponent<RawImage>();
                rawImg.texture = texture;
                rawImg.raycastTarget = false;

                ColorBlock colors = btn.colors;
                colors.normalColor = bg.color;
                colors.highlightedColor = new Color(0.12f, 0.12f, 0.14f, 1f);
                colors.pressedColor = new Color(0.16f, 0.16f, 0.18f, 1f);
                btn.colors = colors;

                btn.onClick.AddListener(() => ShowImagePopout(capturedLabel, capturedDesc, capturedTex, capturedScale));

                GameObject captionBar = new GameObject("CaptionBar");
                captionBar.transform.SetParent(obj.transform, false);

                RectTransform captionRect = captionBar.AddComponent<RectTransform>();
                captionRect.anchorMin = new Vector2(0, 0);
                captionRect.anchorMax = new Vector2(1, 0);
                captionRect.pivot = new Vector2(0.5f, 0);
                captionRect.anchoredPosition = new Vector2(0, 2);
                float captionH = Mathf.RoundToInt(18 * uiScale);
                captionRect.sizeDelta = new Vector2(-4, captionH);

                Image captionBg = captionBar.AddComponent<Image>();
                captionBg.color = new Color(0f, 0f, 0f, 0.6f);
                captionBg.raycastTarget = false;

                GameObject captionTextObj = new GameObject("Text");
                captionTextObj.transform.SetParent(captionBar.transform, false);

                RectTransform ctRect = captionTextObj.AddComponent<RectTransform>();
                ctRect.anchorMin = Vector2.zero;
                ctRect.anchorMax = Vector2.one;
                ctRect.offsetMin = new Vector2(6, 0);
                ctRect.offsetMax = new Vector2(-6, 0);

                Text ct = captionTextObj.AddComponent<Text>();
                ct.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                ct.fontSize = Mathf.Max(10, Mathf.RoundToInt(11 * uiScale));
                ct.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
                ct.text = label;
                ct.alignment = TextAnchor.MiddleLeft;
                ct.raycastTarget = false;
            }
            else
            {
                bg.color = new Color(0.1f, 0.1f, 0.12f, 0.9f);

                ColorBlock colors = btn.colors;
                colors.normalColor = bg.color;
                colors.highlightedColor = new Color(0.15f, 0.15f, 0.18f, 0.95f);
                colors.pressedColor = new Color(0.2f, 0.2f, 0.24f, 1f);
                btn.colors = colors;

                btn.onClick.AddListener(() => ShowImagePopout(capturedLabel, capturedDesc, null, capturedScale));

                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(obj.transform, false);

                RectTransform textRt = textObj.AddComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(8, 8);
                textRt.offsetMax = new Vector2(-8, -8);

                Text text = textObj.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.fontSize = Mathf.Max(10, Mathf.RoundToInt(11 * uiScale));
                text.color = new Color(0.45f, 0.45f, 0.5f, 0.7f);
                text.alignment = TextAnchor.MiddleCenter;
                text.fontStyle = FontStyle.Italic;
                text.text = "[ " + label + " ]\n\n" + description + "\n\nClick to enlarge";
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
            }
        }

        private void AddInlineImage(Transform parent, string label, string description,
            float widthFraction, float uiScale)
        {
            GameObject wrapper = new GameObject("InlineImage_" + label.Replace(" ", ""));
            wrapper.transform.SetParent(parent, false);

            HorizontalLayoutGroup wrapperHlg = wrapper.AddComponent<HorizontalLayoutGroup>();
            wrapperHlg.childAlignment = TextAnchor.MiddleCenter;
            wrapperHlg.childControlWidth = false;
            wrapperHlg.childControlHeight = true;
            wrapperHlg.childForceExpandWidth = false;
            wrapperHlg.childForceExpandHeight = false;
            int vpad = Mathf.RoundToInt(4 * uiScale);
            wrapperHlg.padding = new RectOffset(0, 0, vpad, vpad);

            ContentSizeFitter wrapperFitter = wrapper.AddComponent<ContentSizeFitter>();
            wrapperFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            float maxWidth = Mathf.RoundToInt(Screen.width * widthFraction);
            float imgHeight = maxWidth / (16f / 9f);

            GameObject inner = new GameObject("Inner");
            inner.transform.SetParent(wrapper.transform, false);

            LayoutElement innerLE = inner.AddComponent<LayoutElement>();
            innerLE.preferredWidth = maxWidth;
            innerLE.preferredHeight = imgHeight;
            innerLE.flexibleWidth = 0;

            VerticalLayoutGroup innerVlg = inner.AddComponent<VerticalLayoutGroup>();
            innerVlg.childControlWidth = true;
            innerVlg.childControlHeight = true;
            innerVlg.childForceExpandWidth = true;
            innerVlg.childForceExpandHeight = true;

            AddImagePlaceholder(inner.transform, label, description, 100, uiScale);
        }

        private void ShowImagePopout(string label, string caption, Texture2D texture, float uiScale)
        {
            DismissImagePopout();

            _popoutObject = new GameObject("ImagePopout");
            _popoutObject.transform.SetParent(_overlayObject.transform, false);

            RectTransform popoutRect = _popoutObject.AddComponent<RectTransform>();
            popoutRect.anchorMin = Vector2.zero;
            popoutRect.anchorMax = Vector2.one;
            popoutRect.offsetMin = Vector2.zero;
            popoutRect.offsetMax = Vector2.zero;

            Image backdrop = _popoutObject.AddComponent<Image>();
            backdrop.color = new Color(0.02f, 0.02f, 0.04f, 0.95f);
            backdrop.raycastTarget = true;

            Button dismissBtn = _popoutObject.AddComponent<Button>();
            dismissBtn.targetGraphic = backdrop;
            dismissBtn.transition = Selectable.Transition.None;
            dismissBtn.onClick.AddListener(DismissImagePopout);

            float captionHeight = 70f * uiScale;
            float hintHeight = 20f * uiScale;
            float padding = 30f * uiScale;

            GameObject imageObj = new GameObject("Image");
            imageObj.transform.SetParent(_popoutObject.transform, false);

            RectTransform imageRect = imageObj.AddComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(0.1f, 0f);
            imageRect.anchorMax = new Vector2(0.9f, 1f);
            imageRect.offsetMin = new Vector2(0, captionHeight + hintHeight + padding * 1.5f);
            imageRect.offsetMax = new Vector2(0, -padding);

            if (texture != null)
            {
                RawImage rawImg = imageObj.AddComponent<RawImage>();
                rawImg.texture = texture;
                rawImg.raycastTarget = false;

                AspectRatioFitter arf = imageObj.AddComponent<AspectRatioFitter>();
                arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                arf.aspectRatio = (float)texture.width / texture.height;
            }
            else
            {
                Image placeholderBg = imageObj.AddComponent<Image>();
                placeholderBg.color = new Color(0.1f, 0.1f, 0.12f, 0.8f);
                placeholderBg.raycastTarget = false;

                Outline placeholderOutline = imageObj.AddComponent<Outline>();
                placeholderOutline.effectColor = new Color(0.4f, 0.4f, 0.5f, 0.5f);
                placeholderOutline.effectDistance = new Vector2(1, -1);

                GameObject placeholderText = new GameObject("PlaceholderText");
                placeholderText.transform.SetParent(imageObj.transform, false);

                RectTransform ptRect = placeholderText.AddComponent<RectTransform>();
                ptRect.anchorMin = Vector2.zero;
                ptRect.anchorMax = Vector2.one;
                ptRect.offsetMin = new Vector2(20, 20);
                ptRect.offsetMax = new Vector2(-20, -20);

                Text pt = placeholderText.AddComponent<Text>();
                pt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                pt.fontSize = Mathf.RoundToInt(16 * uiScale);
                pt.color = new Color(0.5f, 0.5f, 0.55f, 0.6f);
                pt.text = "[ " + label + " ]\n\nImage will be added here";
                pt.alignment = TextAnchor.MiddleCenter;
                pt.raycastTarget = false;
            }

            GameObject captionObj = new GameObject("Caption");
            captionObj.transform.SetParent(_popoutObject.transform, false);

            RectTransform captionRect = captionObj.AddComponent<RectTransform>();
            captionRect.anchorMin = new Vector2(0.1f, 0f);
            captionRect.anchorMax = new Vector2(0.9f, 0f);
            captionRect.pivot = new Vector2(0.5f, 0f);
            captionRect.anchoredPosition = new Vector2(0, hintHeight + padding * 0.5f);
            captionRect.sizeDelta = new Vector2(0, captionHeight);

            Image captionBg = captionObj.AddComponent<Image>();
            captionBg.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);
            captionBg.raycastTarget = false;

            GameObject captionTextObj = new GameObject("CaptionText");
            captionTextObj.transform.SetParent(captionObj.transform, false);

            RectTransform captionTextRect = captionTextObj.AddComponent<RectTransform>();
            captionTextRect.anchorMin = Vector2.zero;
            captionTextRect.anchorMax = Vector2.one;
            captionTextRect.offsetMin = new Vector2(12, 6);
            captionTextRect.offsetMax = new Vector2(-12, -6);

            Text captionText = captionTextObj.AddComponent<Text>();
            captionText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            captionText.fontSize = Mathf.RoundToInt(14 * uiScale);
            captionText.color = new Color(0.8f, 0.8f, 0.8f, 0.95f);
            captionText.text = "<b>" + label + "</b>\n" + caption;
            captionText.alignment = TextAnchor.MiddleCenter;
            captionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            captionText.verticalOverflow = VerticalWrapMode.Overflow;
            captionText.raycastTarget = false;

            GameObject hintObj = new GameObject("DismissHint");
            hintObj.transform.SetParent(_popoutObject.transform, false);

            RectTransform hintRect = hintObj.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.1f, 0f);
            hintRect.anchorMax = new Vector2(0.9f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0, padding * 0.25f);
            hintRect.sizeDelta = new Vector2(0, hintHeight);

            Text hintText = hintObj.AddComponent<Text>();
            hintText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            hintText.fontSize = Mathf.RoundToInt(11 * uiScale);
            hintText.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
            hintText.text = "Click anywhere to close";
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.raycastTarget = false;

            _popoutObject.transform.SetAsLastSibling();
        }

        private void DismissImagePopout()
        {
            if (_popoutObject != null)
            {
                Object.Destroy(_popoutObject);
                _popoutObject = null;
            }
        }
        private static string GuidePath
        {
            get
            {
                if (_guidePath == null)
                {
                    string dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    _guidePath = Path.Combine(dllDir, "Content", "guide");
                }
                return _guidePath;
            }
        }

        private static Texture2D LoadGuideImage(string name)
        {
            if (_imageCache.TryGetValue(name, out var cached))
                return cached;

            string filePath = Path.Combine(GuidePath, name + ".png");

            if (!File.Exists(filePath))
            {
                _imageCache[name] = null;
                return null;
            }

            try
            {
                byte[] data = File.ReadAllBytes(filePath);
                Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Bilinear;
                tex.wrapMode = TextureWrapMode.Clamp;

                if (tex.LoadImage(data))
                {
                    _imageCache[name] = tex;
                    BeefsRecipesPlugin.Log.LogInfo($"Loaded guide image: {name} ({tex.width}x{tex.height})");
                    return tex;
                }
                else
                {
                    Object.Destroy(tex);
                    _imageCache[name] = null;
                    BeefsRecipesPlugin.Log.LogWarning($"Failed to decode guide image: {filePath}");
                    return null;
                }
            }
            catch (System.Exception ex)
            {
                _imageCache[name] = null;
                BeefsRecipesPlugin.Log.LogWarning($"Failed to load guide image {filePath}: {ex.Message}");
                return null;
            }
        }

        public static void ClearImageCache()
        {
            foreach (var kvp in _imageCache)
            {
                if (kvp.Value != null)
                    Object.Destroy(kvp.Value);
            }
            _imageCache.Clear();
        }
        private const string TitleBlock =
            "## Beef's Recipes\n" +
            "An in-game notepad for Stationeers. Share notes with friends, sketch out base layouts, or even add your favorite Beef recipe.";
        private const string GettingStarted_PanelModes =
            "### The Panel\n\n" +
            "The notes panel sits on the **right edge** of your screen. Hover over it and it slides in. Here's how the different modes work:\n\n" +
            "**Hidden** - Panel is off-screen. Hover the right edge to bring it back.\n\n" +
            "**Peeking** - A quick read-only preview that slides in when you hover. Move your mouse away and it disappears.\n\n" +
            "**Peek Locked** - Click the peeking panel and it stays put. You can read your notes and click checkboxes, but you can't edit except clicking checkboxes. Click the empty background area (of the panel) to dismiss it.\n\n" +
            "**Expanded** - Click the **orange edge bar** and you're in edit mode. The panel widens and all your notes become editable.\n\n" +
            "**Fullscreen** - Hit the expand icon (or right-click \u2192 Fullscreen) for a two-column layout. Your personal notes go on the left, shared notes on the right.";

        private const string GettingStarted_Editing =
            "### Editing\n\n" +
            "Every note has a **title** (one line) and **content** (multi-line). You'll need to be in Expanded or Fullscreen mode to make changes.\n\n" +
            "You can also enter edit mode by **Double-clicking** anywhere on the panel background. When you're done, hit **Escape** to stop editing and get your game controls back.\n\n" +
            "While you're editing, your keyboard goes to the notes instead of the game. **Tab** jumps to the next field, **Shift+Tab** goes back. If you press **Enter** while in a title, it'll jump straight to that note's content.\n\n" +
            "Need bigger (or smaller) text? Hold **Ctrl** and scroll the **mouse wheel**.";

        private const string GettingStarted_AddingNotes =
            "### Adding & Removing Notes\n\n" +
            "In edit mode, you'll see a subtle line between your notes. Hover over it and two buttons appear:\n\n" +
            "- **+Note** (green) - adds a new text note\n" +
            "- **+Draw** (blue) - adds a new drawing canvas\n\n" +
            "You can also right-click any note to get these options, along with **Delete section** to remove it. You can also add and remove notes from the context menu.\n\n" +
            "Deleting is immediate, so be careful! If you delete something by accident, there's no undo for that!";
        private const string Content_Markdown =
            "### Markdown\n\n" +
            "The content field supports markdown formatting. You write it in edit mode and see the formatted result in read mode:\n\n" +
            "**Headings** - Start a line with # through ###### for different sizes\n\n" +
            "**Bold / Italic** - Wrap text in **double asterisks** for bold, _single_ for italic, or _**triple**_ for both\n\n" +
            "**Checkboxes** - Write - [ ] for unchecked or - [x] for checked. You can click these to toggle them in read mode!\n\n" +
            "**Lists** - Use - or * for bullet points, or 1. 2. 3. for numbered lists\n\n" +
            "**Code** - Backticks for `inline code` (shows in green), or triple backticks on their own lines for code blocks\n\n" +
            "**Extras** - ==highlighted text== shows as yellow bold, > for blockquotes (italic gray), and --- for horizontal dividers";

        private const string Content_Drawings =
            "### Drawings\n\n" +
            "Click a drawing canvas while in edit mode and the toolbar appears at the top.\n\n" +
            "You've got a **color picker**, a **brush size slider**, and a **brush/eraser toggle**. The second row has **BG** (toggle a dark background), **Undo**, **Clear**, **Cancel** (reverts unsaved changes), and **Save**.\n\n" +
            "Drawings save as PNG data automatically whenever you exit edit mode or the game saves. If a drawing is taking up space, double-click its drag handle to collapse it down to just the title.";

        private const string Content_Organizing =
            "### Organizing Your Notes\n\n" +
            "**Reorder** - The colored bar on the left side of each note is a drag handle. Drag it up or down to rearrange your notes. A blue line shows where the note will land.\n\n" +
            "**Collapse** - Double-click the drag bar to collapse a note down to a single-line preview. The bar turns red so you know it's collapsed. Double-click again to expand it.\n\n" +
            "**Colors** - Right-click any note and pick **Title color** or **Content color** to color-code your notes.";

        private const string Content_Colors =
            "### Color Picker\n\n" +
            "The color picker pops up whenever you're changing a note color or your accent color. It's got preset swatches across the top, a saturation/value square, a hue bar, a hex input field for exact values, and 6 custom palette slots so you can save your favorite colors.";
        private const string Multi_Sharing =
            "### Sharing Notes\n\n" +
            "To share a note with other players, **drag it below the SHARED NOTES divider** in sidebar mode, or into the **right column** in fullscreen. You can also right-click \u2192 **Share to server**.\n\n" +
            "To take it back, drag it back to your personal section or right-click \u2192 **Unshare**.\n\n" +
            "Shared notes get a colored badge showing the owner's name. Only the owner can edit or unshare their notes. Changes sync to other players in near real-time.";

        private const string Multi_Hiding =
            "### Hiding Notes\n\n" +
            "If someone is sharing notes you don't need to see, right-click their note \u2192 **Hide this note**. This only hides it for you, not anyone else.\n\n" +
            "You can manage your hidden notes a few ways:\n\n" +
            "- The **Show Hidden** button on the divider or column header\n" +
            "- **Settings** \u2192 Multiplayer section lists hidden notes with unhide buttons\n" +
            "- Right-click the divider for **Hide all** / **Unhide all** options";

        private const string Multi_Voting =
            "### Voting & Admin Tools\n\n" +
            "On **dedicated servers**, if someone posts inappropriate content, right-click their note \u2192 **Vote to remove**. When enough players vote (default 2), the note is automatically removed. Changed your mind? Use **Retract removal vote**.\n\n" +
            "If you're the **host**, you get direct control - right-click any shared note \u2192 **Delete (admin)** removes it immediately. You can also **Kick** or **Ban** players from the same menu.";

        private const string Multi_AccentColors =
            "### Accent Colors\n\n" +
            "Your badge color on shared notes defaults to your **suit paint color** in-game. If your suit isn't painted, you get a color generated from your SteamID.\n\n" +
            "To pick your own, right-click any shared note \u2192 **Change accent color**, or go to **Settings** and click the accent color swatch. Hit **reset** to go back to your suit color.";
        private const string Ref_Shortcuts =
            "### Keyboard Shortcuts\n\n" +
            "**Tab / Shift+Tab** - Jump to next / previous field\n" +
            "**Enter** - Title \u2192 jumps to content (in content it adds a newline)\n" +
            "**Escape** - Closes menus \u2192 overlays \u2192 exits editing \u2192 exits fullscreen\n" +
            "**Ctrl+Scroll** - Make text bigger or smaller\n" +
            "**Right-click** - Opens the context menu\n" +
            "**Double-click drag bar** - Collapse or expand a note\n" +
            "**Double-click panel** - Toggle edit mode on/off";

        private const string Ref_Settings =
            "### Settings\n\n" +
            "Click the **gear icon** to open settings. Every value has a slider you can drag, or you can type a number directly into the box. Hit **R** to reset any value to its default.\n\n" +
            "**Font size offset** - Make text bigger or smaller (-6 to +16)\n" +
            "**UI scale** - Scales everything in the panel (0.5x to 3.0x)\n" +
            "**Edge bar** - Adjust the width, height, and hover zone of the orange activation bar\n" +
            "**Drag bar width** - How wide the reorder handles are on the left side of notes";

        private const string Ref_Saving =
            "### How Saving Works\n\n" +
            "**Singleplayer / Host** - Your notes save alongside each game save. Load a save and your notes come back to that point. When old saves roll off, their notes clean up too.\n\n" +
            "**Client** - Personal notes save locally in the BepInEx config folder, keyed to the server you're on. They persist across sessions.\n\n" +
            "**Shared notes** - Managed by the server and persist across restarts. They auto-save every time the game saves.\n\n" +
            "Personal notes also auto-save every 5 minutes in the background and whenever you exit edit mode.";

        private const string Ref_Tips =
            "### Tips & Tricks\n\n" +
            "- Right-click \u2192 **Copy as markdown** to paste notes into Discord, wikis, or anywhere else\n" +
            "- **Copy all notes** grabs everything at once\n" +
            "- Empty notes automatically hide in read mode so they won't clutter things up\n" +
            "- Type --- in a note to create a visual divider line\n" +
            "- In fullscreen, each column scrolls independently\n" +
            "- The panel remembers its height, position, and scroll between sessions";
    }
}