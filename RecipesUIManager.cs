using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace BeefsRecipes
{
    public class RecipesUIManager
    {
        private const float RefHeight = 2160f;

        private const float BaseCollapsedWidth = 10f;
        private const float BaseResizeHandleHeight = 20f;
        private const int BaseButtonSize = 40;
        private const int BaseEdgeButtonWidth = 30;
        private const int BaseSlideButtonSize = 30;
        private const int BaseChevronFontSize = 24;
        private const int BaseResizeIndicatorFontSize = 16;

        private static float ScreenScaleFactor => Mathf.Clamp(Screen.height / RefHeight, 0.25f, 1.5f);
        public static float ScaleFactor => ScreenScaleFactor * BeefsRecipesPlugin.UIScaleMultiplier.Value;

        // Scaled constants
        public static float CollapsedWidth => BaseCollapsedWidth * ScaleFactor;
        public static float ResizeHandleHeight => BaseResizeHandleHeight * ScaleFactor;
        public static int ButtonSize => Mathf.RoundToInt(BaseButtonSize * ScaleFactor);
        public static int EdgeButtonWidth => Mathf.RoundToInt(BaseEdgeButtonWidth * ScaleFactor * BeefsRecipesPlugin.EdgeBarWidthMultiplier.Value);
        public static int SlideButtonSize => Mathf.RoundToInt(BaseSlideButtonSize * ScaleFactor);
        public static int ChevronFontSize => Mathf.RoundToInt(BaseChevronFontSize * ScaleFactor);
        public static int ResizeIndicatorFontSize => Mathf.RoundToInt(BaseResizeIndicatorFontSize * ScaleFactor);

        private const float BasePeekWidthPercent = 0.16f;
        private const float BaseExpandedWidthPercent = 0.1875f;
        public static float PeekWidthPercent => BasePeekWidthPercent * BeefsRecipesPlugin.UIScaleMultiplier.Value;
        public static float ExpandedWidthPercent => BaseExpandedWidthPercent * BeefsRecipesPlugin.UIScaleMultiplier.Value;
        public const float MinPanelHeight = 200f;
        public const float MaxPanelHeightPercent = 0.95f;
        public const float TransitionSpeed = 8f;
        private const int BasePeekFontSize = 12;
        private const int BaseExpandedFontSize = 16;
        private const int BaseTitleFontSize = 18;
        public static int PeekFontSize => Mathf.RoundToInt(BasePeekFontSize * BeefsRecipesPlugin.UIScaleMultiplier.Value);
        public static int ExpandedFontSize => Mathf.RoundToInt(BaseExpandedFontSize * BeefsRecipesPlugin.UIScaleMultiplier.Value);
        public static int TitleFontSize => Mathf.RoundToInt(BaseTitleFontSize * BeefsRecipesPlugin.UIScaleMultiplier.Value);
        public const int TitleMaxChars = 30;
        public const float TextBoxWidthPercent = 0.80f;
        public const float ButtonAreaWidthPercent = 0.20f;
        public const float TextHideWidthThreshold = 0.05f;

        private GameObject _canvasObject;
        private Canvas _canvas;
        private GameObject _panelObject;
        private RectTransform _panelRect;
        private Image _backgroundImage;
        private GameObject _edgeButtonObject;
        private Button _edgeButton;
        private Text _edgeButtonText;
        private GameObject _scrollViewObject;
        private ScrollRect _scrollRect;
        private GameObject _contentObject;
        private RectTransform _contentRect;
        private GameObject _topResizeHandle;
        private GameObject _bottomResizeHandle;
        private bool _anyHandleHovered;
        private Button _topHandleButton;
        private Button _bottomHandleButton;
        private bool _lastAnyHandleHovered;
        private GameObject _slideButtonObject;
        private Button _slideButton;
        private Sprite _edgeButtonSprite;

        public GameObject SlideButtonObject => _slideButtonObject;
        public Button SlideButton => _slideButton;

        public Canvas Canvas => _canvas;
        public GameObject PanelObject => _panelObject;
        public RectTransform PanelRect => _panelRect;
        public GameObject EdgeButtonObject => _edgeButtonObject;
        public Button EdgeButton => _edgeButton;
        public Text EdgeButtonText => _edgeButtonText;
        public GameObject TopResizeHandle => _topResizeHandle;
        public GameObject BottomResizeHandle => _bottomResizeHandle;
        public GameObject ContentObject => _contentObject;
        public RectTransform ContentRect => _contentRect;
        public Image BackgroundImage => _backgroundImage;

        public void CreateUI()
        {
            CreateCanvas();
            CreatePanel();
            CreateEdgeButton();
            CreateSlideButton();
            CreateResizeHandles();
            CreateScrollView();

            _edgeButtonObject.transform.SetAsLastSibling();
            _canvas.enabled = false;
        }

        private void CreateCanvas()
        {
            _canvasObject = new GameObject("BeefsRecipesCanvas");
            Object.DontDestroyOnLoad(_canvasObject);

            _canvas = _canvasObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9999;

            _canvasObject.AddComponent<GraphicRaycaster>();
        }

        private void CreatePanel()
        {
            _panelObject = new GameObject("RecipesPanel");
            _panelObject.transform.SetParent(_canvasObject.transform, false);

            _panelRect = _panelObject.AddComponent<RectTransform>();
            _panelRect.anchorMin = new Vector2(1, 0.5f);
            _panelRect.anchorMax = new Vector2(1, 0.5f);
            _panelRect.pivot = new Vector2(1, 0.5f);
            _panelRect.anchoredPosition = Vector2.zero;
            _panelRect.sizeDelta = new Vector2(CollapsedWidth, 600f);

            _backgroundImage = _panelObject.AddComponent<Image>();
            _backgroundImage.color = new Color(0, 0, 0, 0.95f);
            _backgroundImage.raycastTarget = true;
        }

        private void CreateEdgeButton()
        {
            _edgeButtonObject = new GameObject("EdgeButton");
            _edgeButtonObject.transform.SetParent(_panelObject.transform, false);

            RectTransform edgeRect = _edgeButtonObject.AddComponent<RectTransform>();
            edgeRect.anchorMin = new Vector2(0, 0.5f);
            edgeRect.anchorMax = new Vector2(0, 0.5f);
            edgeRect.pivot = new Vector2(1, 0.5f);
            edgeRect.anchoredPosition = Vector2.zero;
            edgeRect.sizeDelta = new Vector2(EdgeButtonWidth, Screen.height * 0.05f * BeefsRecipesPlugin.EdgeBarHeightMultiplier.Value);

            Image edgeImage = _edgeButtonObject.AddComponent<Image>();
            edgeImage.color = new Color(0.1f, 0.1f, 0.1f, 0f);
            edgeImage.raycastTarget = true;

            int cornerRadius = Mathf.RoundToInt(14 * ScaleFactor);
            _edgeButtonSprite = CreateEdgeSprite(cornerRadius);
            edgeImage.sprite = _edgeButtonSprite;
            edgeImage.type = Image.Type.Sliced;

            _edgeButton = _edgeButtonObject.AddComponent<Button>();
            _edgeButton.targetGraphic = edgeImage;
            _edgeButton.transition = Selectable.Transition.ColorTint;

            ColorBlock colors = _edgeButton.colors;
            colors.normalColor = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            colors.highlightedColor = new Color(0.6f, 0.6f, 0.6f, 1.0f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            _edgeButton.colors = colors;

            GameObject chevronObj = new GameObject("Chevron");
            chevronObj.transform.SetParent(_edgeButtonObject.transform, false);

            RectTransform chevronRect = chevronObj.AddComponent<RectTransform>();
            chevronRect.anchorMin = Vector2.zero;
            chevronRect.anchorMax = Vector2.one;
            chevronRect.pivot = new Vector2(0.5f, 0.5f);
            chevronRect.anchoredPosition = Vector2.zero;
            chevronRect.sizeDelta = Vector2.zero;

            _edgeButtonText = chevronObj.AddComponent<Text>();
            _edgeButtonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            _edgeButtonText.fontSize = ChevronFontSize;
            _edgeButtonText.color = Color.white;
            _edgeButtonText.alignment = TextAnchor.MiddleCenter;
            _edgeButtonText.fontStyle = FontStyle.Bold;
            _edgeButtonText.text = "◀";
            _edgeButtonText.raycastTarget = false;
            _edgeButtonText.gameObject.SetActive(false);
        }

        private void CreateResizeHandles()
        {
            _topResizeHandle = new GameObject("TopResizeHandle");
            _topResizeHandle.transform.SetParent(_panelObject.transform, false);

            RectTransform topRect = _topResizeHandle.AddComponent<RectTransform>();
            topRect.anchorMin = new Vector2(0, 1);
            topRect.anchorMax = new Vector2(1, 1);
            topRect.pivot = new Vector2(0.5f, 1);
            topRect.anchoredPosition = Vector2.zero;
            topRect.sizeDelta = new Vector2(0, ResizeHandleHeight);

            Image topImage = _topResizeHandle.AddComponent<Image>();
            topImage.color    = new Color(0.1f, 0.1f, 0.1f, 0f);
            topImage.raycastTarget = true;
            var topBtn = _topResizeHandle.AddComponent<Button>();
            topBtn.targetGraphic = topImage;
            topBtn.colors = _edgeButton.colors;
            topBtn.transition = Selectable.Transition.ColorTint;
            topBtn.interactable = true;
            _topHandleButton = topBtn;

            CreateResizeIndicator(_topResizeHandle.transform);

            _bottomResizeHandle = new GameObject("BottomResizeHandle");
            _bottomResizeHandle.transform.SetParent(_panelObject.transform, false);

            RectTransform bottomRect = _bottomResizeHandle.AddComponent<RectTransform>();
            bottomRect.anchorMin = new Vector2(0, 0);
            bottomRect.anchorMax = new Vector2(1, 0);
            bottomRect.pivot = new Vector2(0.5f, 0);
            bottomRect.anchoredPosition = Vector2.zero;
            bottomRect.sizeDelta = new Vector2(0, ResizeHandleHeight);

            Image bottomImage = _bottomResizeHandle.AddComponent<Image>();
            bottomImage.color = new Color(0.1f, 0.1f, 0.1f, 0f);
            bottomImage.raycastTarget = true;
            var bottomBtn = _bottomResizeHandle.AddComponent<Button>();
            bottomBtn.targetGraphic = bottomImage;
            bottomBtn.colors = _edgeButton.colors;
            bottomBtn.transition = Selectable.Transition.ColorTint;
            bottomBtn.interactable = true;
            _bottomHandleButton = bottomBtn;

            CreateResizeIndicator(_bottomResizeHandle.transform);
        }

        private void CreateSlideButton()
        {
            _slideButtonObject = new GameObject("SlideButton");
            _slideButtonObject.transform.SetParent(_panelObject.transform, false);

            var rt = _slideButtonObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot     = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(0f, -ResizeHandleHeight);
            rt.sizeDelta = new Vector2(SlideButtonSize, SlideButtonSize);

            var img = _slideButtonObject.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.1f, 0f);
            img.raycastTarget = true;

            _slideButton = _slideButtonObject.AddComponent<Button>();
            _slideButton.targetGraphic = img;
            _slideButton.transition = Selectable.Transition.ColorTint;
            _slideButton.colors = _edgeButton.colors;

            var textObj = new GameObject("Icon");
            textObj.transform.SetParent(_slideButtonObject.transform, false);
            var tr = textObj.AddComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;

            var t = textObj.AddComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.text = "↕";
            t.fontSize = Mathf.RoundToInt(SlideButtonSize * 0.67f);
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.raycastTarget = false;

            _slideButtonObject.SetActive(false);
        }

        private void CreateResizeIndicator(Transform parent)
        {
            GameObject indicator = new GameObject("Indicator");
            indicator.transform.SetParent(parent, false);

            RectTransform indicatorRect = indicator.AddComponent<RectTransform>();
            indicatorRect.anchorMin = new Vector2(0.5f, 0.5f);
            indicatorRect.anchorMax = new Vector2(0.5f, 0.5f);
            indicatorRect.pivot = new Vector2(0.5f, 0.5f);
            indicatorRect.anchoredPosition = Vector2.zero;
            indicatorRect.sizeDelta = new Vector2(100, ResizeHandleHeight);

            Text indicatorText = indicator.AddComponent<Text>();
            indicatorText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            indicatorText.text = "═";
            indicatorText.fontSize = ResizeIndicatorFontSize;
            indicatorText.color = new Color(1f, 1f, 1f, 0.8f);
            indicatorText.alignment = TextAnchor.MiddleCenter;
            indicatorText.raycastTarget = false;
        }

        private void CreateScrollView()
        {
            _scrollViewObject = new GameObject("ScrollView");
            _scrollViewObject.transform.SetParent(_panelObject.transform, false);

            RectTransform scrollViewRect = _scrollViewObject.AddComponent<RectTransform>();
            scrollViewRect.anchorMin = new Vector2(0, 0);
            scrollViewRect.anchorMax = new Vector2(1, 1);
            scrollViewRect.offsetMin = new Vector2(15, ResizeHandleHeight + 5);
            scrollViewRect.offsetMax = new Vector2(-10, -(ResizeHandleHeight + 5));

            Image scrollViewImage = _scrollViewObject.AddComponent<Image>();
            scrollViewImage.color = new Color(0, 0, 0, 0.01f);
            scrollViewImage.raycastTarget = true;

            _scrollRect = _scrollViewObject.AddComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.scrollSensitivity = 40f;

            GameObject viewportObject = new GameObject("Viewport");
            viewportObject.transform.SetParent(_scrollViewObject.transform, false);

            RectTransform viewportRect = viewportObject.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(5, 5);
            viewportRect.offsetMax = new Vector2(-5, -5);

            viewportObject.AddComponent<RectMask2D>();

            _contentObject = new GameObject("Content");
            _contentObject.transform.SetParent(viewportObject.transform, false);

            _contentRect = _contentObject.AddComponent<RectTransform>();
            _contentRect.anchorMin = new Vector2(0, 1);
            _contentRect.anchorMax = new Vector2(1, 1);
            _contentRect.pivot = new Vector2(0.5f, 1);
            _contentRect.anchoredPosition = Vector2.zero;
            _contentRect.sizeDelta = new Vector2(0, 0);

            VerticalLayoutGroup layoutGroup = _contentObject.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = true;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = true;
            layoutGroup.spacing = 10f;
            layoutGroup.padding = new RectOffset(10, 10, 10, 10);

            ContentSizeFitter sizeFitter = _contentObject.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollRect.content = _contentRect;
            _scrollRect.viewport = viewportRect;
        }

        public InputField CreateInputField(Transform parent, string name, int fontSize, bool multiline)
        {
            GameObject fieldObj = new GameObject(name);
            fieldObj.transform.SetParent(parent, false);

            ContentSizeFitter fieldFitter = fieldObj.AddComponent<ContentSizeFitter>();
            fieldFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image fieldBg = fieldObj.AddComponent<Image>();
            fieldBg.color = new Color(0, 0, 0, 0);
            fieldBg.raycastTarget = true;

            InputField inputField = fieldObj.AddComponent<InputField>();

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(fieldObj.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();

            Text textComponent = textObj.AddComponent<Text>();
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComponent.fontSize = fontSize;
            textComponent.color = Color.white;
            textComponent.alignment = TextAnchor.UpperLeft;
            textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComponent.verticalOverflow = VerticalWrapMode.Overflow;
            textComponent.supportRichText = true;
            textComponent.raycastTarget = false;

            if (multiline)
            {
                inputField.lineType = InputField.LineType.MultiLineNewline;

                textRect.anchorMin = new Vector2(0, 1);
                textRect.anchorMax = new Vector2(1, 1);
                textRect.pivot = new Vector2(0.5f, 1);
                textRect.anchoredPosition = new Vector2(0, -5);
                textRect.sizeDelta = new Vector2(-10, 800);
            }
            else
            {
                inputField.lineType = InputField.LineType.SingleLine;

                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(5, 5);
                textRect.offsetMax = new Vector2(-5, -5);

                LayoutElement textLayout = textObj.AddComponent<LayoutElement>();
                textLayout.preferredHeight = fontSize + 10;
            }

            inputField.textComponent = textComponent;
            inputField.interactable = false;

            return inputField;
        }

        public void SetPlaceholder(InputField field, string placeholderText)
        {
            GameObject placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(field.transform, false);

            RectTransform placeholderRect = placeholderObj.AddComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(5, 5);
            placeholderRect.offsetMax = new Vector2(-5, -5);

            Text placeholder = placeholderObj.AddComponent<Text>();
            placeholder.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            placeholder.text = placeholderText;
            placeholder.fontSize = field.textComponent.fontSize;
            placeholder.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            placeholder.alignment = TextAnchor.UpperLeft;
            placeholder.fontStyle = FontStyle.Italic;
            placeholder.horizontalOverflow = HorizontalWrapMode.Wrap;
            placeholder.verticalOverflow = VerticalWrapMode.Overflow;
            placeholder.raycastTarget = false;

            field.placeholder = placeholder;
            field.ForceLabelUpdate();
        }

        public Button CreateButton(Transform parent, string text, Color color, bool square, int size)
        {
            GameObject buttonObj = new GameObject($"Button_{text}");
            buttonObj.transform.SetParent(parent, false);

            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            if (square)
            {
                LayoutElement layout = buttonObj.AddComponent<LayoutElement>();
                layout.minWidth = size;
                layout.minHeight = size;
                layout.preferredWidth = size;
                layout.preferredHeight = size;
                layout.flexibleWidth = 0f;
                layout.flexibleHeight = 0f;
            }
            else
            {
                buttonRect.sizeDelta = new Vector2(40, 0);
            }

            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = color;
            buttonImage.raycastTarget = true;

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text textComponent = textObj.AddComponent<Text>();
            textComponent.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            textComponent.text = text;
            textComponent.fontSize = Mathf.Max(12, size - 8);
            textComponent.color = Color.white;
            textComponent.alignment = TextAnchor.MiddleCenter;
            textComponent.fontStyle = FontStyle.Bold;
            textComponent.raycastTarget = false;

            return button;
        }

        public void UpdateChevronAndHandleVisibility(bool isHoveringPanel, bool isExpanded, bool isPeekLocked, bool isEditing, bool isHoveringEdge)
        {
            if (_edgeButtonText == null) return;

            const float chevronIdleAlpha = 0.80f;
            const float hoverAlpha = 1f;

            var orange = new Color(1f, 0.39f, 0.08f, 1f);

            const float buttonIdleAlphaDefault = 0.50f;
            const float buttonIdleAlphaEdit    = 0.55f;
            float buttonIdleA = isEditing ? buttonIdleAlphaEdit : buttonIdleAlphaDefault;

            bool chevronVisible = isExpanded || isPeekLocked || isHoveringPanel;
            _edgeButtonText.gameObject.SetActive(chevronVisible);
            _edgeButtonText.text = isExpanded ? "▶" : "◀";

            float chevronTargetA;
            if (isHoveringEdge)
                chevronTargetA = hoverAlpha;
            else if (isHoveringPanel)
                chevronTargetA = chevronIdleAlpha;
            else
                chevronTargetA = 0f;

            var curChevron = _edgeButtonText.color;
            float chevronNewA = Mathf.MoveTowards(curChevron.a, chevronTargetA, Time.deltaTime * TransitionSpeed);
            _edgeButtonText.color = new Color(1f, 1f, 1f, chevronNewA);

            var edgeImage = _edgeButtonObject.GetComponent<Image>();
            if (edgeImage != null)
            {
                float bgTargetA;
                if (isHoveringEdge)
                    bgTargetA = hoverAlpha;
                else if (isHoveringPanel)
                    bgTargetA = buttonIdleA;
                else
                    bgTargetA = 0f;

                float newA = Mathf.MoveTowards(edgeImage.color.a, bgTargetA, Time.deltaTime * TransitionSpeed);
                edgeImage.color = new Color(orange.r, orange.g, orange.b, newA);
                edgeImage.raycastTarget = true;
            }

            if (_slideButtonObject != null)
                _slideButtonObject.SetActive(isEditing && (isExpanded || isPeekLocked || isHoveringPanel ));

            UpdateHandleFade(_topResizeHandle,    isExpanded, _anyHandleHovered, isEditing);
            UpdateHandleFade(_bottomResizeHandle, isExpanded, _anyHandleHovered, isEditing);
        }

        public void SetAnyHandleHovered(bool hovered)
        {
            _anyHandleHovered = hovered;

            if (hovered == _lastAnyHandleHovered) return;
            _lastAnyHandleHovered = hovered;

            if (EventSystem.current == null) return;

            var ped = new PointerEventData(EventSystem.current) { position = Input.mousePosition };

            if (_topHandleButton != null)
            {
                if (hovered)
                    ExecuteEvents.Execute<IPointerEnterHandler>(_topHandleButton.gameObject, ped, ExecuteEvents.pointerEnterHandler);
                else
                    ExecuteEvents.Execute<IPointerExitHandler>(_topHandleButton.gameObject, ped, ExecuteEvents.pointerExitHandler);
            }

            if (_bottomHandleButton != null)
            {
                if (hovered)
                    ExecuteEvents.Execute<IPointerEnterHandler>(_bottomHandleButton.gameObject, ped, ExecuteEvents.pointerEnterHandler);
                else
                    ExecuteEvents.Execute<IPointerExitHandler>(_bottomHandleButton.gameObject, ped, ExecuteEvents.pointerExitHandler);
            }
        }

        private void UpdateHandleFade(GameObject handle, bool isExpanded, bool highlight, bool isEditing)
        {
            if (handle == null) return;

            var img = handle.GetComponent<Image>();
            if (!isExpanded)
            {
                if (img != null) img.color = new Color(0f, 0f, 0f, 0f);
                handle.SetActive(false);
                return;
            }
            handle.SetActive(true);

            var orange = new Color(1f, 0.39f, 0.08f, 1f);
            const float buttonIdleAlphaDefault = 0.50f;
            const float buttonIdleAlphaEdit    = 0.55f;
            float idleA = isEditing ? buttonIdleAlphaEdit : buttonIdleAlphaDefault;
            float targetBgA = highlight ? 1f : idleA;

            if (img != null)
            {
                float newA = Mathf.MoveTowards(img.color.a, targetBgA, Time.deltaTime * TransitionSpeed);
                img.color = new Color(orange.r, orange.g, orange.b, newA);
                img.raycastTarget = true;
            }

            const float glyphIdleAlpha = 0.80f;
            float targetGlyphA = highlight ? 1f : glyphIdleAlpha;

            var indicator = handle.transform.Find("Indicator")?.GetComponent<Text>();
            if (indicator != null)
            {
                var c = indicator.color;
                float newGlyphA = Mathf.MoveTowards(c.a, targetGlyphA, Time.deltaTime * TransitionSpeed);
                indicator.color = new Color(1f, 1f, 1f, newGlyphA);
                indicator.raycastTarget = false;
            }
        }

        public void UpdateBackgroundTransparency(float targetAlpha)
        {
            if (_backgroundImage == null) return;

            Color currentColor = _backgroundImage.color;
            currentColor.a = Mathf.Lerp(currentColor.a, targetAlpha, Time.deltaTime * TransitionSpeed);
            _backgroundImage.color = currentColor;
        }

        public void UpdateSizes()
        {
            if (_edgeButtonObject != null)
            {
                var rect = _edgeButtonObject.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(EdgeButtonWidth, Screen.height * 0.05f * BeefsRecipesPlugin.EdgeBarHeightMultiplier.Value);

                var edgeImage = _edgeButtonObject.GetComponent<Image>();
                if (edgeImage != null)
                {
                    int cornerRadius = Mathf.RoundToInt(14 * ScaleFactor);
                    if (_edgeButtonSprite != null)
                    {
                        Object.Destroy(_edgeButtonSprite.texture);
                        Object.Destroy(_edgeButtonSprite);
                    }
                    _edgeButtonSprite = CreateEdgeSprite(cornerRadius);
                    edgeImage.sprite = _edgeButtonSprite;
                    edgeImage.type = Image.Type.Sliced;
                }
            }

            if (_edgeButtonText != null)
            {
                _edgeButtonText.fontSize = ChevronFontSize;
            }

            if (_topResizeHandle != null)
            {
                var rect = _topResizeHandle.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(0, ResizeHandleHeight);

                var indicator = _topResizeHandle.transform.Find("Indicator")?.GetComponent<Text>();
                if (indicator != null)
                {
                    indicator.fontSize = ResizeIndicatorFontSize;
                }
            }

            if (_bottomResizeHandle != null)
            {
                var rect = _bottomResizeHandle.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(0, ResizeHandleHeight);

                var indicator = _bottomResizeHandle.transform.Find("Indicator")?.GetComponent<Text>();
                if (indicator != null)
                {
                    indicator.fontSize = ResizeIndicatorFontSize;
                }
            }

            if (_slideButtonObject != null)
            {
                var rect = _slideButtonObject.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(0f, -ResizeHandleHeight);
                rect.sizeDelta = new Vector2(SlideButtonSize, SlideButtonSize);

                var icon = _slideButtonObject.transform.Find("Icon")?.GetComponent<Text>();
                if (icon != null)
                {
                    icon.fontSize = Mathf.RoundToInt(SlideButtonSize * 0.67f);
                }
            }
        }

        private Sprite CreateEdgeSprite(int cornerRadius)
        {
            int size = cornerRadius * 2 + 2;
            Texture2D texture = new Texture2D(size, size);
            texture.filterMode = FilterMode.Bilinear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isInside = true;

                    if (x < cornerRadius && y < cornerRadius)
                    {
                        float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cornerRadius, cornerRadius));
                        isInside = dist <= cornerRadius;
                    }

                    else if (x < cornerRadius && y >= size - cornerRadius)
                    {
                        float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cornerRadius, size - cornerRadius));
                        isInside = dist <= cornerRadius;
                    }

                    texture.SetPixel(x, y, isInside ? Color.white : Color.clear);
                }
            }

            texture.Apply();

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(cornerRadius, cornerRadius, 1, cornerRadius)
            );

            return sprite;
        }
    }
}