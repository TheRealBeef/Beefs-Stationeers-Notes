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

        public static float CollapsedWidth => BaseCollapsedWidth * ScaleFactor;
        public static float ResizeHandleHeight => BaseResizeHandleHeight * ScaleFactor;
        public static int ButtonSize => Mathf.Max(24, Mathf.RoundToInt(BaseButtonSize * ScaleFactor));
        public static int EdgeButtonWidth => Mathf.Max(20, Mathf.RoundToInt(BaseEdgeButtonWidth * ScaleFactor * BeefsRecipesPlugin.EdgeBarWidthMultiplier.Value));
        public static int SlideButtonSize => Mathf.Max(20, Mathf.RoundToInt(BaseSlideButtonSize * ScaleFactor));
        public static float ButtonGap => Mathf.Max(2f, 4f * ScaleFactor);
        public static float ResizeHandleWidth => Mathf.Max(30f, 60f * ScaleFactor);

        private static float ButtonX(int index) => index * (SlideButtonSize + ButtonGap);
        public static int ChevronFontSize => Mathf.Max(12, Mathf.RoundToInt(BaseChevronFontSize * ScaleFactor));
        public static int ResizeIndicatorFontSize => Mathf.Max(10, Mathf.RoundToInt(BaseResizeIndicatorFontSize * ScaleFactor));

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
        private RectTransform _viewportRect;
        private RectTransform _scrollViewRect;
        private VerticalLayoutGroup _contentLayoutGroup;
        private GameObject _topResizeHandle;
        private GameObject _bottomResizeHandle;
        private bool _anyHandleHovered;
        private Button _topHandleButton;
        private Button _bottomHandleButton;
        private bool _lastAnyHandleHovered;
        private GameObject _slideButtonObject;
        private Button _slideButton;
        private GameObject _gearButtonObject;
        private Button _gearButton;
        private Sprite _edgeButtonSprite;
        private int _lastCornerRadius = -1;
        private Sprite _panelFullscreenSprite;
        private GameObject _fullscreenButtonObject;
        private Button _fullscreenButton;
        private GameObject _helpButtonObject;
        private Button _helpButton;
        private GameObject _fullscreenBackdrop;

        private GameObject _fullscreenColumnsContainer;
        private GameObject _leftContentObject;
        private RectTransform _leftContentRect;
        private ScrollRect _leftScrollRect;
        private GameObject _rightContentObject;
        private RectTransform _rightContentRect;
        private ScrollRect _rightScrollRect;
        private GameObject _rightColumnRoot;
        private GameObject _fullscreenBorder;
        private bool _isFullscreenLayout;

        public GameObject SlideButtonObject => _slideButtonObject;
        public Button SlideButton => _slideButton;
        public GameObject GearButtonObject => _gearButtonObject;
        public Button GearButton => _gearButton;
        public GameObject FullscreenButtonObject => _fullscreenButtonObject;
        public GameObject HelpButtonObject => _helpButtonObject;

        public Canvas Canvas => _canvas;
        public GameObject PanelObject => _panelObject;
        public RectTransform PanelRect => _panelRect;
        public GameObject EdgeButtonObject => _edgeButtonObject;
        public Button EdgeButton => _edgeButton;
        public Text EdgeButtonText => _edgeButtonText;
        public GameObject TopResizeHandle => _topResizeHandle;
        public GameObject BottomResizeHandle => _bottomResizeHandle;
        public GameObject ContentObject => _isFullscreenLayout ? _leftContentObject : _contentObject;
        public RectTransform ContentRect => _isFullscreenLayout ? _leftContentRect : _contentRect;
        public GameObject RightContentObject => _rightContentObject;
        public RectTransform RightContentRect => _rightContentRect;
        public GameObject RightColumnRoot => _rightColumnRoot;
        public bool IsFullscreenLayout => _isFullscreenLayout;
        public Image BackgroundImage => _backgroundImage;

        public float ScrollPosition
        {
            get
            {
                if (_isFullscreenLayout && _leftScrollRect != null)
                    return _leftScrollRect.verticalNormalizedPosition;
                return _scrollRect != null ? _scrollRect.verticalNormalizedPosition : 1f;
            }
            set
            {
                if (_isFullscreenLayout && _leftScrollRect != null)
                    _leftScrollRect.verticalNormalizedPosition = value;
                else if (_scrollRect != null)
                    _scrollRect.verticalNormalizedPosition = value;
            }
        }

        public void CreateUI()
        {
            CreateCanvas();
            CreateFullscreenBackdrop();
            CreatePanel();
            CreateEdgeButton();
            CreateSlideButton();
            CreateGearButton();
            CreateFullscreenButton();
            CreateHelpButton();
            CreateResizeHandles();
            CreateScrollView();

            _slideButtonObject.transform.SetAsLastSibling();
            _gearButtonObject.transform.SetAsLastSibling();
            _fullscreenButtonObject.transform.SetAsLastSibling();
            _helpButtonObject.transform.SetAsLastSibling();
            _edgeButtonObject.transform.SetAsLastSibling();
            _canvas.enabled = false;
        }

        private void CreateCanvas()
        {
            var existing = GameObject.Find("BeefsRecipesCanvas");
            if (existing != null)
            {
                Object.Destroy(existing);
            }

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
            _backgroundImage.type = Image.Type.Sliced;
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
            _lastCornerRadius = cornerRadius;
            edgeImage.sprite = _edgeButtonSprite;
            edgeImage.type = Image.Type.Sliced;

            _panelFullscreenSprite = CreateMirroredSprite(_edgeButtonSprite, cornerRadius);
            _backgroundImage.sprite = _edgeButtonSprite;

            _edgeButton = _edgeButtonObject.AddComponent<Button>();
            _edgeButton.targetGraphic = edgeImage;
            _edgeButton.transition = Selectable.Transition.None;

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
            topRect.anchorMin = new Vector2(0.5f, 1);
            topRect.anchorMax = new Vector2(0.5f, 1);
            topRect.pivot = new Vector2(0.5f, 1);
            topRect.anchoredPosition = Vector2.zero;
            topRect.sizeDelta = new Vector2(ResizeHandleWidth, ResizeHandleHeight);

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
            bottomRect.anchorMin = new Vector2(0.5f, 0);
            bottomRect.anchorMax = new Vector2(0.5f, 0);
            bottomRect.pivot = new Vector2(0.5f, 0);
            bottomRect.anchoredPosition = Vector2.zero;
            bottomRect.sizeDelta = new Vector2(ResizeHandleWidth, ResizeHandleHeight);

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
            rt.anchoredPosition = new Vector2(ButtonX(0), -ResizeHandleHeight);
            rt.sizeDelta = new Vector2(SlideButtonSize, SlideButtonSize);

            var img = _slideButtonObject.AddComponent<Image>();
            img.color = new Color(0.1f, 0.1f, 0.1f, 0f);
            img.raycastTarget = true;

            _slideButton = _slideButtonObject.AddComponent<Button>();
            _slideButton.targetGraphic = img;
            _slideButton.transition = Selectable.Transition.None;

            var textObj = new GameObject("Icon");
            textObj.transform.SetParent(_slideButtonObject.transform, false);
            var tr = textObj.AddComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.15f, 0.15f);
            tr.anchorMax = new Vector2(0.85f, 0.85f);
            tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;

            Image slideIcon = textObj.AddComponent<Image>();
            slideIcon.sprite = CreateSlideSprite();
            slideIcon.color = new Color(1f, 1f, 1f, 0.85f);
            slideIcon.raycastTarget = false;

            var slideFade = _slideButtonObject.AddComponent<ButtonHoverFade>();
            slideFade.idleColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);

            _slideButtonObject.SetActive(false);
        }

        private void CreateGearButton()
        {
            _gearButtonObject = new GameObject("GearButton");
            _gearButtonObject.transform.SetParent(_panelObject.transform, false);

            var rt = _gearButtonObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot     = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(ButtonX(1), -ResizeHandleHeight);
            rt.sizeDelta = new Vector2(SlideButtonSize, SlideButtonSize);

            var img = _gearButtonObject.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f, 0f);
            img.raycastTarget = true;

            _gearButton = _gearButtonObject.AddComponent<Button>();
            _gearButton.targetGraphic = img;
            _gearButton.transition = Selectable.Transition.None;

            GameObject iconObj = new GameObject("GearIcon");
            iconObj.transform.SetParent(_gearButtonObject.transform, false);
            var ir = iconObj.AddComponent<RectTransform>();
            ir.anchorMin = new Vector2(0.15f, 0.15f);
            ir.anchorMax = new Vector2(0.85f, 0.85f);
            ir.offsetMin = Vector2.zero;
            ir.offsetMax = Vector2.zero;

            Image iconImage = iconObj.AddComponent<Image>();
            iconImage.sprite = CreateGearSprite();
            iconImage.color = new Color(1f, 1f, 1f, 0.85f);
            iconImage.raycastTarget = false;

            _gearButtonObject.AddComponent<ButtonHoverFade>();

            _gearButtonObject.SetActive(false);
        }

        private void CreateFullscreenButton()
        {
            _fullscreenButtonObject = new GameObject("FullscreenButton");
            _fullscreenButtonObject.transform.SetParent(_panelObject.transform, false);

            var rt = _fullscreenButtonObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot     = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(ButtonX(2), -ResizeHandleHeight);
            rt.sizeDelta = new Vector2(SlideButtonSize, SlideButtonSize);

            var img = _fullscreenButtonObject.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f, 0f);
            img.raycastTarget = true;

            _fullscreenButton = _fullscreenButtonObject.AddComponent<Button>();
            _fullscreenButton.targetGraphic = img;
            _fullscreenButton.transition = Selectable.Transition.None;

            GameObject iconObj = new GameObject("FullscreenIcon");
            iconObj.transform.SetParent(_fullscreenButtonObject.transform, false);
            var ir = iconObj.AddComponent<RectTransform>();
            ir.anchorMin = new Vector2(0.15f, 0.15f);
            ir.anchorMax = new Vector2(0.85f, 0.85f);
            ir.offsetMin = Vector2.zero;
            ir.offsetMax = Vector2.zero;

            Image iconImage = iconObj.AddComponent<Image>();
            iconImage.sprite = CreateExpandSprite();
            iconImage.color = new Color(1f, 1f, 1f, 0.85f);
            iconImage.raycastTarget = false;

            _fullscreenButtonObject.AddComponent<ButtonHoverFade>();

            _fullscreenButtonObject.SetActive(false);
        }

        private void CreateHelpButton()
        {
            _helpButtonObject = new GameObject("HelpButton");
            _helpButtonObject.transform.SetParent(_panelObject.transform, false);

            var rt = _helpButtonObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot     = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(ButtonX(3), -ResizeHandleHeight);
            rt.sizeDelta = new Vector2(SlideButtonSize, SlideButtonSize);

            var img = _helpButtonObject.AddComponent<Image>();
            img.color = new Color(0.15f, 0.15f, 0.15f, 0f);
            img.raycastTarget = true;

            _helpButton = _helpButtonObject.AddComponent<Button>();
            _helpButton.targetGraphic = img;
            _helpButton.transition = Selectable.Transition.None;

            GameObject textObj = new GameObject("HelpIcon");
            textObj.transform.SetParent(_helpButtonObject.transform, false);
            var tr = textObj.AddComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.1f, 0.1f);
            tr.anchorMax = new Vector2(0.9f, 0.9f);
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;

            Text helpText = textObj.AddComponent<Text>();
            helpText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            helpText.text = "?";
            helpText.fontSize = Mathf.RoundToInt(SlideButtonSize * 0.6f);
            helpText.color = new Color(1f, 1f, 1f, 0.85f);
            helpText.alignment = TextAnchor.MiddleCenter;
            helpText.fontStyle = FontStyle.Bold;
            helpText.raycastTarget = false;

            _helpButtonObject.AddComponent<ButtonHoverFade>();

            _helpButtonObject.SetActive(false);
        }

        private void CreateFullscreenBackdrop()
        {
            _fullscreenBackdrop = new GameObject("FullscreenBackdrop");
            _fullscreenBackdrop.transform.SetParent(_canvasObject.transform, false);

            RectTransform backdropRect = _fullscreenBackdrop.AddComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;

            Image backdropImage = _fullscreenBackdrop.AddComponent<Image>();
            backdropImage.color = new Color(0, 0, 0, 0.01f);
            backdropImage.raycastTarget = true;

            _fullscreenBackdrop.SetActive(false);
        }

        public void ShowFullscreenBackdrop(bool show)
        {
            if (_fullscreenBackdrop != null)
            {
                _fullscreenBackdrop.SetActive(show);
                if (show)
                {
                    _fullscreenBackdrop.transform.SetSiblingIndex(_panelObject.transform.GetSiblingIndex());
                    _panelObject.transform.SetAsLastSibling();
                }
            }
        }

        public void SetFullscreenPanelAnchoring(bool fullscreen)
        {
            if (_panelRect == null) return;

            if (fullscreen)
            {
                _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
                _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
                _panelRect.pivot = new Vector2(0.5f, 0.5f);
                _panelRect.anchoredPosition = Vector2.zero;
            }
            else
            {
                _panelRect.anchorMin = new Vector2(1, 0.5f);
                _panelRect.anchorMax = new Vector2(1, 0.5f);
                _panelRect.pivot = new Vector2(1, 0.5f);
            }

            if (_backgroundImage != null)
                _backgroundImage.sprite = fullscreen ? _panelFullscreenSprite : _edgeButtonSprite;
        }

        public void SetSidebarControlsVisible(bool visible)
        {
            if (_edgeButtonObject != null) _edgeButtonObject.SetActive(visible);
            if (_slideButtonObject != null) _slideButtonObject.SetActive(visible);
        }

        public void SetResizeHandlesVisible(bool visible)
        {
            if (_topResizeHandle != null) _topResizeHandle.SetActive(visible);
            if (_bottomResizeHandle != null) _bottomResizeHandle.SetActive(visible);
        }

        public void SetFullscreenButtonVisible(bool visible)
        {
            if (_fullscreenButtonObject != null)
                _fullscreenButtonObject.SetActive(visible);
        }

        public void SetHelpButtonVisible(bool visible)
        {
            if (_helpButtonObject != null)
                _helpButtonObject.SetActive(visible);
        }

        private static Sprite _gearSprite;

        private static Sprite CreateGearSprite()
        {
            if (_gearSprite != null) return _gearSprite;

            const int size = 32;
            float center = size / 2f;
            float outerR = size * 0.47f;
            float bodyR = size * 0.34f;
            float innerR = size * 0.14f;
            const int teeth = 8;
            float toothHalf = Mathf.PI / teeth * 0.45f;

            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - center;
                    float dy = y + 0.5f - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx);
                    if (angle < 0) angle += Mathf.PI * 2f;

                    float sector = angle % (Mathf.PI * 2f / teeth);
                    bool inTooth = Mathf.Abs(sector - Mathf.PI / teeth) < toothHalf;
                    float edgeR = inTooth ? outerR : bodyR;

                    float alpha = 0f;
                    if (dist <= edgeR && dist >= innerR)
                    {
                        float outerFade = Mathf.Clamp01((edgeR - dist) * 2f);
                        float innerFade = Mathf.Clamp01((dist - innerR) * 2f);
                        alpha = Mathf.Min(outerFade, innerFade);
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            _gearSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
            return _gearSprite;
        }

        private static Sprite _expandSprite;

        private static Sprite _slideSprite;
        private static Sprite _gripSprite;

        private static Sprite CreateGripSprite()
        {
            if (_gripSprite != null) return _gripSprite;

            const int w = 32;
            const int h = 8;
            Texture2D tex = new Texture2D(w, h, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float alpha = 0f;
                    if (y == 2 || y == 5)
                    {
                        float edgeFade = Mathf.Clamp01(Mathf.Min(x, w - 1 - x) / 3f);
                        alpha = 0.9f * edgeFade;
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            _gripSprite = Sprite.Create(tex, new Rect(0, 0, w, h),
                new Vector2(0.5f, 0.5f), 100f);
            return _gripSprite;
        }

        private static Sprite CreateSlideSprite()
        {
            if (_slideSprite != null) return _slideSprite;

            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float cx = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float alpha = 0f;

                    float upTipY = size - 4f;
                    float upBaseY = size * 0.6f;
                    float upHalfW = 8f;
                    if (py >= upBaseY && py <= upTipY)
                    {
                        float t = (py - upBaseY) / (upTipY - upBaseY);
                        float halfW = upHalfW * (1f - t);
                        if (Mathf.Abs(px - cx) <= halfW + 0.5f)
                            alpha = Mathf.Clamp01(halfW + 0.5f - Mathf.Abs(px - cx));
                    }

                    float dnTipY = 4f;
                    float dnBaseY = size * 0.4f;
                    float dnHalfW = 8f;
                    if (py >= dnTipY && py <= dnBaseY)
                    {
                        float t = (dnBaseY - py) / (dnBaseY - dnTipY);
                        float halfW = dnHalfW * (1f - t);
                        if (Mathf.Abs(px - cx) <= halfW + 0.5f)
                            alpha = Mathf.Max(alpha, Mathf.Clamp01(halfW + 0.5f - Mathf.Abs(px - cx)));
                    }

                    if (py >= size * 0.38f && py <= size * 0.62f && Mathf.Abs(px - cx) <= 2f)
                        alpha = Mathf.Max(alpha, 0.8f);

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            _slideSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
            return _slideSprite;
        }

        private static Sprite CreateExpandSprite()
        {
            if (_expandSprite != null) return _expandSprite;

            const int size = 32;
            const int border = 3;
            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            int bx0 = 8, by0 = 8, bx1 = size - 2, by1 = size - 2;
            int fx0 = 2, fy0 = 2, fx1 = size - 8, fy1 = size - 8;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float alpha = 0f;

                    bool inBackOuter = x >= bx0 && x <= bx1 && y >= by0 && y <= by1;
                    bool inBackInner = x >= bx0 + border && x <= bx1 - border && y >= by0 + border && y <= by1 - border;
                    if (inBackOuter && !inBackInner) alpha = 0.5f;

                    bool inFrontOuter = x >= fx0 && x <= fx1 && y >= fy0 && y <= fy1;
                    bool inFrontInner = x >= fx0 + border && x <= fx1 - border && y >= fy0 + border && y <= fy1 - border;
                    if (inFrontOuter && !inFrontInner) alpha = 1f;
                    else if (inFrontInner) alpha = 0.15f;

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            _expandSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
            return _expandSprite;
        }

        private static Sprite _brushSprite;

        public static Sprite CreateBrushSprite()
        {
            if (_brushSprite != null) return _brushSprite;

            const int size = 32;
            float centerX = size / 2f;
            float tipCenterY = size * 0.7f;
            float tipRadius = size * 0.22f;
            float handleBottom = size * 0.12f;
            float handleTop = tipCenterY - tipRadius * 0.5f;
            float handleHalfWidth = size * 0.08f;

            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float alpha = 0f;

                    float distFromTip = Mathf.Sqrt((px - centerX) * (px - centerX) + (py - tipCenterY) * (py - tipCenterY));
                    if (distFromTip <= tipRadius)
                        alpha = Mathf.Clamp01((tipRadius - distFromTip) * 2f);

                    if (py >= handleBottom && py <= handleTop && Mathf.Abs(px - centerX) <= handleHalfWidth + 0.5f)
                    {
                        float fade = Mathf.Clamp01(handleHalfWidth + 0.5f - Mathf.Abs(px - centerX));
                        alpha = Mathf.Max(alpha, fade * 0.9f);
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            _brushSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
            return _brushSprite;
        }

        private static Sprite _eraserSprite;

        public static Sprite CreateEraserSprite()
        {
            if (_eraserSprite != null) return _eraserSprite;

            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float ax = 10f, ay = 29f;
            float bx = 27f, by = 29f;
            float cx = 22f, cy = 3f;
            float dx = 5f,  dy = 3f;

            float bandY = 12f;
            float bandLx = 7f, bandRx = 24f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;

                    float crossAB = (bx - ax) * (py - ay) - (by - ay) * (px - ax);
                    float crossBC = (cx - bx) * (py - by) - (cy - by) * (px - bx);
                    float crossCD = (dx - cx) * (py - cy) - (dy - cy) * (px - cx);
                    float crossDA = (ax - dx) * (py - dy) - (ay - dy) * (px - dx);

                    bool inside = crossAB <= 0 && crossBC <= 0 && crossCD <= 0 && crossDA <= 0;

                    float alpha = 0f;

                    if (inside)
                    {
                        float dEdge = Mathf.Min(
                            Mathf.Min(DistToSegment(px, py, ax, ay, bx, by),
                                      DistToSegment(px, py, bx, by, cx, cy)),
                            Mathf.Min(DistToSegment(px, py, cx, cy, dx, dy),
                                      DistToSegment(px, py, dx, dy, ax, ay)));

                        alpha = Mathf.Clamp01(dEdge * 1.5f);

                        if (py <= bandY + 1f)
                            alpha *= 0.5f;

                        float dBand = Mathf.Abs(py - bandY);
                        if (dBand < 1.2f && px >= bandLx && px <= bandRx)
                            alpha = Mathf.Max(alpha, Mathf.Clamp01(1.2f - dBand) * 0.9f);
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            _eraserSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
            return _eraserSprite;
        }

        private static Sprite _checkboxOnSprite;
        private static Sprite _checkboxOffSprite;

        public static Sprite CreateCheckboxSprite(bool isChecked)
        {
            if (isChecked && _checkboxOnSprite != null) return _checkboxOnSprite;
            if (!isChecked && _checkboxOffSprite != null) return _checkboxOffSprite;

            const int size = 24;
            const int border = 2;

            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool inOuter = x >= 1 && x < size - 1 && y >= 1 && y < size - 1;
                    bool inInner = x >= 1 + border && x < size - 1 - border &&
                                   y >= 1 + border && y < size - 1 - border;

                    float alpha = 0f;

                    if (inOuter && !inInner)
                        alpha = 0.9f;

                    if (isChecked && inInner)
                    {
                        float px = x + 0.5f;
                        float py = y + 0.5f;

                        float d1 = DistToSegment(px, py, 5f, 13f, 9f, 8f);
                        float d2 = DistToSegment(px, py, 9f, 8f, 19f, 18f);
                        float d = Mathf.Min(d1, d2);

                        if (d < 2.0f)
                            alpha = Mathf.Clamp01(2.0f - d);
                        else
                            alpha = 0.12f;
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);

            if (isChecked)
                _checkboxOnSprite = sprite;
            else
                _checkboxOffSprite = sprite;

            return sprite;
        }

        private static float DistToSegment(float px, float py,
            float ax, float ay, float bx, float by)
        {
            float dx = bx - ax;
            float dy = by - ay;
            float lenSq = dx * dx + dy * dy;
            if (lenSq < 0.001f) return Mathf.Sqrt((px - ax) * (px - ax) + (py - ay) * (py - ay));

            float t = Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / lenSq);
            float closestX = ax + t * dx;
            float closestY = ay + t * dy;
            return Mathf.Sqrt((px - closestX) * (px - closestX) + (py - closestY) * (py - closestY));
        }

        private void CreateResizeIndicator(Transform parent)
        {
            GameObject indicator = new GameObject("Indicator");
            indicator.transform.SetParent(parent, false);

            RectTransform indicatorRect = indicator.AddComponent<RectTransform>();
            indicatorRect.anchorMin = new Vector2(0.1f, 0.15f);
            indicatorRect.anchorMax = new Vector2(0.9f, 0.85f);
            indicatorRect.offsetMin = Vector2.zero;
            indicatorRect.offsetMax = Vector2.zero;

            Image indicatorImage = indicator.AddComponent<Image>();
            indicatorImage.sprite = CreateGripSprite();
            indicatorImage.color = new Color(1f, 1f, 1f, 0.8f);
            indicatorImage.raycastTarget = false;
        }

        private void CreateScrollView()
        {
            _scrollViewObject = new GameObject("ScrollView");
            _scrollViewObject.transform.SetParent(_panelObject.transform, false);

            _scrollViewRect = _scrollViewObject.AddComponent<RectTransform>();
            _scrollViewRect.anchorMin = new Vector2(0, 0);
            _scrollViewRect.anchorMax = new Vector2(1, 1);

            float dragBarSpace = BeefsRecipesPlugin.DragBarWidth.Value * 2f + 2f;
            float scrollLeftOffset = Mathf.Max(2f, 15f - Mathf.Max(0f, dragBarSpace - 10f));
            _scrollViewRect.offsetMin = new Vector2(scrollLeftOffset, ResizeHandleHeight + 5);
            _scrollViewRect.offsetMax = new Vector2(-10, -(ResizeHandleHeight + 5));

            Image scrollViewImage = _scrollViewObject.AddComponent<Image>();
            scrollViewImage.color = new Color(0, 0, 0, 0.01f);
            scrollViewImage.raycastTarget = true;

            _scrollRect = _scrollViewObject.AddComponent<ScrollRect>();
            _scrollRect.horizontal = false;
            _scrollRect.vertical = true;
            _scrollRect.scrollSensitivity = 40f;

            GameObject viewportObject = new GameObject("Viewport");
            viewportObject.transform.SetParent(_scrollViewObject.transform, false);

            _viewportRect = viewportObject.AddComponent<RectTransform>();
            _viewportRect.anchorMin = Vector2.zero;
            _viewportRect.anchorMax = Vector2.one;
            float extraLeft = Mathf.Max(0f, dragBarSpace - 10f);
            _viewportRect.offsetMin = new Vector2(Mathf.Max(0f, 5f - extraLeft), 5);
            _viewportRect.offsetMax = new Vector2(-5, -5);

            viewportObject.AddComponent<RectMask2D>();

            _contentObject = new GameObject("Content");
            _contentObject.transform.SetParent(viewportObject.transform, false);

            _contentRect = _contentObject.AddComponent<RectTransform>();
            _contentRect.anchorMin = new Vector2(0, 1);
            _contentRect.anchorMax = new Vector2(1, 1);
            _contentRect.pivot = new Vector2(0.5f, 1);
            _contentRect.anchoredPosition = Vector2.zero;
            _contentRect.sizeDelta = new Vector2(0, 0);

            _contentLayoutGroup = _contentObject.AddComponent<VerticalLayoutGroup>();
            _contentLayoutGroup.childControlHeight = false;
            _contentLayoutGroup.childControlWidth = true;
            _contentLayoutGroup.childForceExpandHeight = false;
            _contentLayoutGroup.childForceExpandWidth = true;
            _contentLayoutGroup.spacing = 11f;
            int leftPadding = Mathf.Max(10, Mathf.RoundToInt(dragBarSpace));
            _contentLayoutGroup.padding = new RectOffset(leftPadding, 10, 22, 10);

            ContentSizeFitter sizeFitter = _contentObject.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scrollRect.content = _contentRect;
            _scrollRect.viewport = _viewportRect;
        }

        private void CreateFullscreenColumns()
        {
            _fullscreenColumnsContainer = new GameObject("FullscreenColumns");
            _fullscreenColumnsContainer.transform.SetParent(_panelObject.transform, false);

            RectTransform colsRect = _fullscreenColumnsContainer.AddComponent<RectTransform>();
            colsRect.anchorMin = Vector2.zero;
            colsRect.anchorMax = Vector2.one;
            colsRect.offsetMin = new Vector2(10, ResizeHandleHeight + 5);
            colsRect.offsetMax = new Vector2(-10, -(ResizeHandleHeight + 5));

            HorizontalLayoutGroup hlg = _fullscreenColumnsContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = true;
            hlg.spacing = 12f;
            hlg.padding = new RectOffset(0, 0, 0, 0);

            GameObject leftColumnRoot;
            CreateFullscreenColumn(_fullscreenColumnsContainer.transform, "LeftColumn",
                out _leftContentObject, out _leftContentRect, out _leftScrollRect, out leftColumnRoot);

            GameObject divider = new GameObject("ColumnDivider");
            divider.transform.SetParent(_fullscreenColumnsContainer.transform, false);

            LayoutElement divLE = divider.AddComponent<LayoutElement>();
            divLE.preferredWidth = 1f;
            divLE.flexibleWidth = 0f;

            Image divImage = divider.AddComponent<Image>();
            divImage.color = new Color(1f, 1f, 1f, 0.12f);
            divImage.raycastTarget = false;

            CreateFullscreenColumn(_fullscreenColumnsContainer.transform, "RightColumn",
                out _rightContentObject, out _rightContentRect, out _rightScrollRect, out _rightColumnRoot);

            _fullscreenColumnsContainer.SetActive(false);
        }

        private void CreateFullscreenColumn(Transform parent, string name,
            out GameObject contentObj, out RectTransform contentRect, out ScrollRect scrollRect,
            out GameObject columnRoot)
        {
            float dragBarSpace = BeefsRecipesPlugin.DragBarWidth.Value * 2f + 2f;

            GameObject column = new GameObject(name);
            column.transform.SetParent(parent, false);
            columnRoot = column;

            LayoutElement colLE = column.AddComponent<LayoutElement>();
            colLE.flexibleWidth = 1f;

            GameObject scrollObj = new GameObject("ScrollView");
            scrollObj.transform.SetParent(column.transform, false);

            RectTransform scrollRt = scrollObj.AddComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;

            Image scrollBg = scrollObj.AddComponent<Image>();
            scrollBg.color = new Color(0, 0, 0, 0.01f);
            scrollBg.raycastTarget = true;

            scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 40f;

            GameObject viewportObj = new GameObject("Viewport");
            viewportObj.transform.SetParent(scrollObj.transform, false);

            RectTransform viewportRt = viewportObj.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            float extraLeft = Mathf.Max(0f, dragBarSpace - 10f);
            viewportRt.offsetMin = new Vector2(Mathf.Max(0f, 5f - extraLeft), 5);
            viewportRt.offsetMax = new Vector2(-5, -5);

            viewportObj.AddComponent<RectMask2D>();

            contentObj = new GameObject("Content");
            contentObj.transform.SetParent(viewportObj.transform, false);

            contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);

            int leftPadding = Mathf.Max(10, Mathf.RoundToInt(dragBarSpace));
            VerticalLayoutGroup vlg = contentObj.AddComponent<VerticalLayoutGroup>();
            vlg.childControlHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 11f;
            vlg.padding = new RectOffset(leftPadding, 10, 10, 10);

            ContentSizeFitter csf = contentObj.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRt;
        }

        public void SetFullscreenLayout(bool fullscreen)
        {
            if (_fullscreenColumnsContainer == null)
                CreateFullscreenColumns();

            _isFullscreenLayout = fullscreen;

            _scrollViewObject.SetActive(!fullscreen);
            _fullscreenColumnsContainer.SetActive(fullscreen);

            if (_fullscreenBorder == null)
            {
                _fullscreenBorder = CreatePanelBorderFrame(_panelObject.transform,
                    new Color(1f, 0.39f, 0.08f, 0.35f), 2f);
                _fullscreenBorder.SetActive(false);
            }
            _fullscreenBorder.SetActive(fullscreen);

            if (fullscreen)
            {
                _fullscreenColumnsContainer.transform.SetSiblingIndex(
                    _scrollViewObject.transform.GetSiblingIndex() + 1);
                _fullscreenBorder.transform.SetAsLastSibling();
                _slideButtonObject.transform.SetAsLastSibling();
                _gearButtonObject.transform.SetAsLastSibling();
                _fullscreenButtonObject.transform.SetAsLastSibling();
                _helpButtonObject.transform.SetAsLastSibling();

                Canvas.ForceUpdateCanvases();
            }
        }

        private static GameObject CreatePanelBorderFrame(Transform parent, Color color, float thickness)
        {
            GameObject frame = new GameObject("FullscreenBorder");
            frame.transform.SetParent(parent, false);

            RectTransform frameRect = frame.AddComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;

            LayoutElement le = frame.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            Color shadowColor = new Color(0, 0, 0, 0.25f);
            float shadowWidth = 6f;

            CreateBorderEdge(frame.transform, "ShadowTop", shadowColor,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                new Vector2(0, -thickness), new Vector2(0, shadowWidth));
            CreateBorderEdge(frame.transform, "ShadowBottom", shadowColor,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0),
                new Vector2(0, thickness), new Vector2(0, shadowWidth));
            CreateBorderEdge(frame.transform, "ShadowLeft", shadowColor,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f),
                new Vector2(thickness, 0), new Vector2(shadowWidth, 0));
            CreateBorderEdge(frame.transform, "ShadowRight", shadowColor,
                new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f),
                new Vector2(-thickness, 0), new Vector2(shadowWidth, 0));

            CreateBorderEdge(frame.transform, "Top", color,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
                Vector2.zero, new Vector2(0, thickness));
            CreateBorderEdge(frame.transform, "Bottom", color,
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0),
                Vector2.zero, new Vector2(0, thickness));
            CreateBorderEdge(frame.transform, "Left", color,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0.5f),
                Vector2.zero, new Vector2(thickness, 0));
            CreateBorderEdge(frame.transform, "Right", color,
                new Vector2(1, 0), new Vector2(1, 1), new Vector2(1, 0.5f),
                Vector2.zero, new Vector2(thickness, 0));

            return frame;
        }

        private static void CreateBorderEdge(Transform parent, string name, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject edge = new GameObject(name);
            edge.transform.SetParent(parent, false);

            RectTransform rt = edge.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;

            Image img = edge.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
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
            float chevronNewA = Mathf.MoveTowards(curChevron.a, chevronTargetA, Time.unscaledDeltaTime * TransitionSpeed);
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

                float newA = Mathf.MoveTowards(edgeImage.color.a, bgTargetA, Time.unscaledDeltaTime * TransitionSpeed);
                edgeImage.color = new Color(orange.r, orange.g, orange.b, newA);
                edgeImage.raycastTarget = true;
            }

            if (_slideButtonObject != null)
                _slideButtonObject.SetActive((isExpanded || isPeekLocked) || (isEditing && isHoveringPanel));

            if (_gearButtonObject != null)
                _gearButtonObject.SetActive(isExpanded || isPeekLocked);

            UpdateHandleFade(_topResizeHandle,    isExpanded || isPeekLocked, _anyHandleHovered, isEditing);
            UpdateHandleFade(_bottomResizeHandle, isExpanded || isPeekLocked, _anyHandleHovered, isEditing);
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

        private void UpdateHandleFade(GameObject handle, bool isVisible, bool highlight, bool isEditing)
        {
            if (handle == null) return;

            var img = handle.GetComponent<Image>();
            if (!isVisible)
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
                float newA = Mathf.MoveTowards(img.color.a, targetBgA, Time.unscaledDeltaTime * TransitionSpeed);
                img.color = new Color(orange.r, orange.g, orange.b, newA);
                img.raycastTarget = true;
            }

            const float glyphIdleAlpha = 0.80f;
            float targetGlyphA = highlight ? 1f : glyphIdleAlpha;

            var indicator = handle.transform.Find("Indicator")?.GetComponent<Image>();
            if (indicator != null)
            {
                var c = indicator.color;
                float newGlyphA = Mathf.MoveTowards(c.a, targetGlyphA, Time.unscaledDeltaTime * TransitionSpeed);
                indicator.color = new Color(1f, 1f, 1f, newGlyphA);
                indicator.raycastTarget = false;
            }
        }

        public void UpdateBackgroundTransparency(float targetAlpha)
        {
            if (_backgroundImage == null) return;

            float targetGray = _isFullscreenLayout ? 0.09f : 0f;

            Color currentColor = _backgroundImage.color;
            float t = Time.unscaledDeltaTime * TransitionSpeed;
            currentColor.r = Mathf.Lerp(currentColor.r, targetGray, t);
            currentColor.g = Mathf.Lerp(currentColor.g, targetGray, t);
            currentColor.b = Mathf.Lerp(currentColor.b, targetGray, t);
            currentColor.a = Mathf.Lerp(currentColor.a, targetAlpha, t);
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
                    if (cornerRadius != _lastCornerRadius)
                    {
                        if (_edgeButtonSprite != null)
                        {
                            Object.Destroy(_edgeButtonSprite.texture);
                            Object.Destroy(_edgeButtonSprite);
                        }
                        if (_panelFullscreenSprite != null)
                        {
                            Object.Destroy(_panelFullscreenSprite.texture);
                            Object.Destroy(_panelFullscreenSprite);
                        }
                        _edgeButtonSprite = CreateEdgeSprite(cornerRadius);
                        _panelFullscreenSprite = CreateMirroredSprite(_edgeButtonSprite, cornerRadius);
                        edgeImage.sprite = _edgeButtonSprite;
                        edgeImage.type = Image.Type.Sliced;
                        _backgroundImage.sprite = _isFullscreenLayout ? _panelFullscreenSprite : _edgeButtonSprite;
                        _lastCornerRadius = cornerRadius;
                    }
                }
            }

            if (_edgeButtonText != null)
            {
                _edgeButtonText.fontSize = ChevronFontSize;
            }

            if (_topResizeHandle != null)
            {
                var rect = _topResizeHandle.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(ResizeHandleWidth, ResizeHandleHeight);
            }

            if (_bottomResizeHandle != null)
            {
                var rect = _bottomResizeHandle.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(ResizeHandleWidth, ResizeHandleHeight);
            }

            if (_slideButtonObject != null)
            {
                var rect = _slideButtonObject.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(ButtonX(0), -ResizeHandleHeight);
                rect.sizeDelta = new Vector2(SlideButtonSize, SlideButtonSize);
            }

            if (_gearButtonObject != null)
            {
                var rect = _gearButtonObject.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(ButtonX(1), -ResizeHandleHeight);
                rect.sizeDelta = new Vector2(SlideButtonSize, SlideButtonSize);
            }

            if (_fullscreenButtonObject != null)
            {
                var rect = _fullscreenButtonObject.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(ButtonX(2), -ResizeHandleHeight);
                rect.sizeDelta = new Vector2(SlideButtonSize, SlideButtonSize);
            }

            if (_helpButtonObject != null)
            {
                var rect = _helpButtonObject.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(ButtonX(3), -ResizeHandleHeight);
                rect.sizeDelta = new Vector2(SlideButtonSize, SlideButtonSize);

                var helpText = _helpButtonObject.GetComponentInChildren<Text>();
                if (helpText != null)
                    helpText.fontSize = Mathf.RoundToInt(SlideButtonSize * 0.6f);
            }

            if (_scrollViewRect != null && _viewportRect != null && _contentLayoutGroup != null)
            {
                float dragBarSpace = BeefsRecipesPlugin.DragBarWidth.Value * 2f + 2f;
                float extraLeft = Mathf.Max(0f, dragBarSpace - 10f);

                float scrollLeftOffset = Mathf.Max(2f, 15f - extraLeft);
                _scrollViewRect.offsetMin = new Vector2(scrollLeftOffset, ResizeHandleHeight + 5);

                _viewportRect.offsetMin = new Vector2(Mathf.Max(0f, 5f - extraLeft), 5);

                int leftPadding = Mathf.Max(10, Mathf.RoundToInt(dragBarSpace));
                _contentLayoutGroup.padding = new RectOffset(leftPadding, 10, 22, 10);
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

        private Sprite CreateMirroredSprite(Sprite source, int cornerRadius)
        {
            Texture2D srcTex = source.texture;
            int size = srcTex.width;
            Color[] pixels = srcTex.GetPixels();
            Color[] result = new Color[pixels.Length];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    int m = y * size + (size - 1 - x);
                    result[i] = (pixels[i].a > 0.5f && pixels[m].a > 0.5f) ? Color.white : Color.clear;
                }
            }

            Texture2D texture = new Texture2D(size, size);
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels(result);
            texture.Apply();

            return Sprite.Create(
                texture,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius)
            );
        }
    }

    public class ButtonHoverFade : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        public Color idleColor = new Color(0.15f, 0.15f, 0.15f, 0.5f);
        public Color hoverColor = new Color(1f, 0.39f, 0.08f, 0.45f);
        public Color pressedColor = new Color(1f, 0.39f, 0.08f, 0.7f);
        public float fadeSpeed = 8f;

        private Image _image;
        private bool _hovered;
        private Color _targetColor;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _targetColor = idleColor;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovered = true;
            _targetColor = hoverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _targetColor = idleColor;
        }

        private void Update()
        {
            if (_image == null) return;

            if (_hovered && Input.GetMouseButton(0))
                _targetColor = pressedColor;
            else if (_hovered)
                _targetColor = hoverColor;

            _image.color = Color.Lerp(_image.color, _targetColor, Time.unscaledDeltaTime * fadeSpeed);
        }
    }
}