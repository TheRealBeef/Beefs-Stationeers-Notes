using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BeefsRecipes
{
    public class RecipesContentManager
    {
        private readonly RecipesUIManager _uiManager;
        private readonly RecipesPanelManager _panelManager;

        private List<BeefsRecipesPlugin.RecipeSection> _personalSections = new List<BeefsRecipesPlugin.RecipeSection>();
        private List<BeefsRecipesPlugin.RecipeSection> _publicSections = new List<BeefsRecipesPlugin.RecipeSection>();
        private GameObject _dividerObject;
        private bool _showHiddenNotes = false;
        private Button _showHiddenButton;
        private Text _showHiddenButtonText;
        private Text _dividerLabelText;
        private Text _dividerHintText;

        private readonly Dictionary<string, RecipeSectionUI> _sectionUIMap = new Dictionary<string, RecipeSectionUI>();
        private readonly List<InputField> _allInputFields = new List<InputField>();
        private int _currentFocusedIndex = -1;
        private readonly Dictionary<string, List<NotesMarkdownConverter.CheckboxInfo>> _sectionCheckboxes = new Dictionary<string, List<NotesMarkdownConverter.CheckboxInfo>>();
        private enum ColorTarget { Title, Content }

        private int _fontSizeOffset = 0;
        private const int MinFontOffset = -6;
        private const int MaxFontOffset = 16;

        private string _currentWorldName = "";
        private string _currentSaveId = "";
        private string _sessionKey = null;
        private HashSet<string> _hiddenSectionIds = new HashSet<string>();

        private string _draggedSectionId = null;
        private GameObject _dragPlaceholder = null;
        private GameObject _dragShareIndicator = null;
        private Vector2 _lastDashSize;
        private int _dragTargetIndex = -1;
        private readonly List<GameObject> _gapObjects = new List<GameObject>();

        private readonly HashSet<string> _modifiedPublicSectionIds = new HashSet<string>();

        private float _lastPublicPushTime = 0f;
        private const float PublicPushInterval = 0.5f;

        private string _activeDrawingSectionId = null;

        private GameObject _leftColumnHeader;
        private GameObject _rightColumnHeader;

        private Color _lastLocalAccentColor = Color.clear;
        private bool _suitColorResolved = false;

        private bool _stateDirty = true;
        private bool _lastIsNarrow = false;
        private float _lastDragBarWidth = -1f;
        private int _layoutRebuildFrames = 0;

        private BeefsRecipesPlugin.RecipeSection FindSection(string sectionId)
        {
            return _personalSections.Find(s => s.id == sectionId)
                ?? _publicSections.Find(s => s.id == sectionId);
        }

        private List<BeefsRecipesPlugin.RecipeSection> GetOwningList(string sectionId)
        {
            if (_personalSections.Any(s => s.id == sectionId)) return _personalSections;
            if (_publicSections.Any(s => s.id == sectionId)) return _publicSections;
            return null;
        }

        private List<BeefsRecipesPlugin.RecipeSection> AllSections
        {
            get
            {
                var all = new List<BeefsRecipesPlugin.RecipeSection>(_personalSections);
                all.AddRange(_publicSections);
                return all;
            }
        }

        private class RecipeSectionUI
        {
            public GameObject SectionObject;
            public RectTransform RectTransform;
            public InputField TitleField;
            public GameObject TitleDisplayObject;
            public Text TitleDisplayText;
            public InputField ContentField;
            public GameObject DisplayObject;
            public Text DisplayText;
            public GameObject CollapsedPreviewObject;
            public Text CollapsedPreviewText;
            public GameObject DragHandle;
            public string SectionId;
            public LayoutElement TextBoxLayout;

            public GameObject OwnerTagObject;
            public Text OwnerTagText;
            public Image BadgeBackground;
            public Image PresenceDot;
            public Image PresenceGlow;
            public Outline BadgeOutline;
            public Shadow BadgeShadow;
            public bool IsPublic;
            public bool IsOwnedByLocal;

            public bool IsDrawing;
            public GameObject DrawingCanvasWrapper;
            public Image DrawingWrapperBg;
            public GameObject DrawingEditHint;
            public RawImage DrawingCanvas;
            public RecipesSketchPad SketchPad;
            public GameObject DrawingToolbar;
            public Button UndoButton;
            public Image EraserButtonImage;
            public Image BrushColorSwatch;
            public Image BrushIconImage;
            public Image EraserIconImage;
            public Text ToolModeLabel;
            public Image BgCheckboxImage;
            public VerticalLayoutGroup TextBoxVerticalLayout;

        }

        public RecipesContentManager(RecipesUIManager uiManager, RecipesPanelManager panelManager)
        {
            this._uiManager = uiManager;
            this._panelManager = panelManager;
        }

        public void Update()
        {

            if (Input.GetMouseButtonDown(1) && _panelManager.IsVisible)
            {
                HandleRightClick();
            }

            if (_panelManager.IsEditing)
            {
                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    HandleTabNavigation(forward: !isShiftHeld);
                    return;
                }

                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    bool shouldBlockEnter = false;
                    foreach (var field in _allInputFields)
                    {
                        if (field.isFocused && field.lineType != InputField.LineType.MultiLineNewline)
                        {
                            shouldBlockEnter = true;
                            HandleTabNavigation(forward: true);
                            break;
                        }
                    }

                    if (shouldBlockEnter)
                    {
                        return;
                    }
                }

                if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                {
                    float scroll = Input.mouseScrollDelta.y;
                    if (scroll != 0)
                    {
                        _fontSizeOffset = Mathf.Clamp(_fontSizeOffset + (int)(scroll * 2), MinFontOffset, MaxFontOffset);
                        ApplyState();
                    }
                }
            }

            for (int i = 0; i < _allInputFields.Count; i++)
            {
                if (_allInputFields[i].isFocused)
                {
                    _currentFocusedIndex = i;
                    break;
                }
            }

            bool isNarrow = _panelManager.IsTransitioning();
            if (isNarrow != _lastIsNarrow)
            {
                _lastIsNarrow = isNarrow;
                _stateDirty = true;
            }

            float currentDragBarWidth = BeefsRecipesPlugin.DragBarWidth.Value;
            if (Mathf.Abs(currentDragBarWidth - _lastDragBarWidth) > 0.01f)
            {
                _lastDragBarWidth = currentDragBarWidth;
                UpdateDragHandleSizes(currentDragBarWidth);
            }

            if (_stateDirty)
            {
                ApplyState();
                _stateDirty = false;
            }

            if (_layoutRebuildFrames > 0)
            {
                _layoutRebuildFrames--;
                var contentVLG = _uiManager.ContentObject?.GetComponent<VerticalLayoutGroup>();
                if (contentVLG != null) { contentVLG.enabled = false; contentVLG.enabled = true; }
                if (_uiManager.IsFullscreenLayout)
                {
                    var rightVLG = _uiManager.RightContentObject?.GetComponent<VerticalLayoutGroup>();
                    if (rightVLG != null) { rightVLG.enabled = false; rightVLG.enabled = true; }
                }
                Canvas.ForceUpdateCanvases();
            }

            if (_modifiedPublicSectionIds.Count > 0 &&
                Time.unscaledTime - _lastPublicPushTime >= PublicPushInterval)
            {
                FlushModifiedPublicSections(false);
            }

            UpdateDrawingCanvasHeightCap();

            if (!_suitColorResolved && Time.frameCount % 30 == 0)
            {
                Color current = GetAccentColorForPlayer(GetLocalClientId());
                if (current != _lastLocalAccentColor)
                {
                    _lastLocalAccentColor = current;
                    RefreshAccentColors();

                    string hashHex = ClientSyncManager.HashToHue(GetLocalClientId());
                    Color hashFallback = Color.clear;
                    ColorUtility.TryParseHtmlString(hashHex, out hashFallback);
                    if (current != hashFallback)
                        _suitColorResolved = true;
                }
            }
        }

        public void LoadNotes(string worldName, string saveFileName)
        {
            _currentWorldName = worldName;
            _currentSaveId = saveFileName;
            _sessionKey = null;

            if (string.IsNullOrEmpty(_currentWorldName) || string.IsNullOrEmpty(_currentSaveId)) return;

            try
            {
                var data = BeefsRecipesSaveManager.LoadNotesData(_currentWorldName, _currentSaveId);
                _personalSections = data.sections ?? new List<BeefsRecipesPlugin.RecipeSection>();

                _fontSizeOffset = data.fontSizeOffset;
                _panelManager.SetPanelHeight(data.panelHeight);
                _panelManager.RestoreSavedPanelMode(data.panelMode);
                _panelManager.SetYOffset(data.panelYOffset);

                if (!BeefsRecipesPlugin.WelcomeNoteShown.Value)
                {
                    _personalSections.Insert(0, CreateWelcomeSection());
                    BeefsRecipesPlugin.WelcomeNoteShown.Value = true;
                    SaveNotes(_currentWorldName, _currentSaveId);
                }

                RebuildUI();
                _uiManager.ScrollPosition = data.scrollPosition;
                RefreshAccentColors();
                _suitColorResolved = false;
                _lastLocalAccentColor = Color.clear;
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error loading notes: {ex.Message}");
                _personalSections = new List<BeefsRecipesPlugin.RecipeSection>();
                RebuildUI();
            }
        }

        public void SaveNotes(string worldName, string saveFileName)
        {
            if (string.IsNullOrEmpty(worldName) || string.IsNullOrEmpty(saveFileName))
            {
                return;
            }

            try
            {
                float panelHeight = _panelManager.GetPanelHeight();
                string panelMode = _panelManager.GetSavedPanelMode();
                float panelYOffset = _panelManager.GetYOffset();

                BeefsRecipesSaveManager.SaveNotes(
                    worldName,
                    saveFileName,
                    _personalSections,
                    _fontSizeOffset,
                    panelHeight,
                    panelYOffset,
                    panelMode,
                    _uiManager.ScrollPosition
                );
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error saving notes to {saveFileName}: {ex.Message}");
            }
        }

        public void LoadPersonalNotes(string sessionKey)
        {
            _sessionKey = sessionKey;

            if (string.IsNullOrEmpty(_sessionKey)) return;

            try
            {
                var data = BeefsRecipesSaveManager.LoadPersonalNotesData(_sessionKey);
                _personalSections = data.sections ?? new List<BeefsRecipesPlugin.RecipeSection>();

                _fontSizeOffset = data.fontSizeOffset;
                _hiddenSectionIds = new HashSet<string>(data.hiddenSectionIds ?? new List<string>());
                _panelManager.SetPanelHeight(data.panelHeight);
                _panelManager.RestoreSavedPanelMode(data.panelMode);
                _panelManager.SetYOffset(data.panelYOffset);

                if (!BeefsRecipesPlugin.WelcomeNoteShown.Value)
                {
                    _personalSections.Insert(0, CreateWelcomeSection());
                    BeefsRecipesPlugin.WelcomeNoteShown.Value = true;
                    SavePersonalNotes();
                }

                RebuildUI();
                _uiManager.ScrollPosition = data.scrollPosition;
                RefreshAccentColors();
                _suitColorResolved = false;
                _lastLocalAccentColor = Color.clear;
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error loading personal notes: {ex.Message}");
                _personalSections = new List<BeefsRecipesPlugin.RecipeSection>();
                _hiddenSectionIds = new HashSet<string>();
                RebuildUI();
            }
        }

        public void SavePersonalNotes()
        {
            if (string.IsNullOrEmpty(_sessionKey)) return;

            try
            {
                float panelHeight = _panelManager.GetPanelHeight();
                string panelMode = _panelManager.GetSavedPanelMode();
                float panelYOffset = _panelManager.GetYOffset();

                BeefsRecipesSaveManager.SavePersonalNotes(
                    _sessionKey,
                    _personalSections,
                    _fontSizeOffset,
                    panelHeight,
                    panelYOffset,
                    panelMode,
                    new List<string>(_hiddenSectionIds),
                    _uiManager.ScrollPosition
                );
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error saving personal notes: {ex.Message}");
            }
        }

        public bool HasSessionKey => !string.IsNullOrEmpty(_sessionKey);

        private static BeefsRecipesPlugin.RecipeSection CreateEmptySection()
        {
            return new BeefsRecipesPlugin.RecipeSection
            {
                id = Guid.NewGuid().ToString(),
                title = "",
                content = "",
                titleColorHex = null,
                contentColorHex = null,
                isCollapsed = false,
                isPublic = false,
                ownerId = 0
            };
        }

        private static BeefsRecipesPlugin.RecipeSection CreateWelcomeSection()
        {
            return new BeefsRecipesPlugin.RecipeSection
            {
                id = Guid.NewGuid().ToString(),
                title = "Welcome to Beef's Recipes!",
                content =
                    "### Getting Started\n" +
                    "- [ ] Hover the **right edge** of the screen to peek\n" +
                    "- [ ] Click the **orange bar** to enter edit mode\n" +
                    "- [ ] Press **Escape** to exit edit mode\n" +
                    "- [ ] **Right-click** a note for options (colors, delete, share)\n" +
                    "- [ ] Click checkboxes in read mode to toggle them \u2014 try it!\n\n" +
                    "### Things to Try\n" +
                    "- [ ] Use **markdown** - bold, *italic*, `code`, headers, and more\n" +
                    "- [ ] Add sections with the **+Note** and **+Draw** buttons between notes\n" +
                    "- [ ] **Collapse** a note by double-clicking its drag handle\n" +
                    "- [ ] Hold **Ctrl + scroll** to resize text\n\n" +
                    "### Multiplayer\n" +
                    "- [ ] **Share** a note by dragging it below the divider or right-click -> Share\n" +
                    "- [ ] **Unshare** by dragging it back or right-click -> Unshare\n" +
                    "- [ ] Your **badge color** matches your suit paint color\n\n" +
                    "Hit **?** for the full guide. Delete this note when you're ready!",
                isCollapsed = false,
                isPublic = false,
                ownerId = 0
            };
        }

        public void ClearNotes()
        {
            _personalSections.Clear();
            _publicSections.Clear();
            _sessionKey = null;
            _hiddenSectionIds.Clear();
            _modifiedPublicSectionIds.Clear();
            _activeDrawingSectionId = null;
            _showHiddenNotes = false;
            RebuildUI();
        }

        public void MergePublicNotes(List<BeefsRecipesPlugin.RecipeSection> publicSections)
        {
            _publicSections = publicSections ?? new List<BeefsRecipesPlugin.RecipeSection>();

            if (_publicSections.Count > 0)
            {
                _suitColorResolved = false;
                _lastLocalAccentColor = Color.clear;
            }

            var publicIds = new HashSet<string>();
            foreach (var s in _publicSections)
                publicIds.Add(s.id);

            _personalSections.RemoveAll(s => publicIds.Contains(s.id));

            RebuildUI();
        }

        public void UpdateSectionInPlace(BeefsRecipesPlugin.RecipeSection updated)
        {
            if (updated == null) return;

            int idx = _publicSections.FindIndex(s => s.id == updated.id);
            if (idx < 0)
            {
                return;
            }
            _publicSections[idx] = updated;

            if (!_sectionUIMap.TryGetValue(updated.id, out var ui))
            {
                return;
            }

            if (updated.isCollapsed != (ui.CollapsedPreviewObject != null && ui.CollapsedPreviewObject.activeSelf))
            {
                CollapseSection(updated.id);
            }

            if (ui.IsDrawing)
            {
                if (ui.SketchPad != null && !string.IsNullOrEmpty(updated.drawingPngBase64))
                {
                    bool isActiveDrawing = _activeDrawingSectionId == updated.id;
                    if (!isActiveDrawing)
                    {
                        ui.SketchPad.LoadFromPng(updated.drawingPngBase64);
                    }
                }
            }
            else
            {
                if (ui.ContentField != null && ui.ContentField.text != updated.content)
                {
                    ui.ContentField.text = updated.content;
                }
                if (ui.DisplayText != null)
                {
                    if (!string.IsNullOrWhiteSpace(updated.content))
                    {
                        List<NotesMarkdownConverter.CheckboxInfo> checkboxes;
                        string richText = NotesMarkdownConverter.MarkdownToUGUI(
                            updated.content, out checkboxes, _fontSizeOffset);
                        ui.DisplayText.text = richText;
                        _sectionCheckboxes[updated.id] = checkboxes;
                    }
                    else
                    {
                        ui.DisplayText.text = "";
                    }
                }
            }

            if (ui.TitleField != null && ui.TitleField.text != updated.title)
            {
                ui.TitleField.text = updated.title;
            }
            if (ui.TitleDisplayText != null)
            {
                if (!string.IsNullOrWhiteSpace(updated.title))
                {
                    ui.TitleDisplayText.supportRichText = true;
                    ui.TitleDisplayText.text = "<b>" + updated.title + "</b>";
                }
                else
                {
                    ui.TitleDisplayText.text = "";
                }
            }

            if (!string.IsNullOrEmpty(updated.titleColorHex) || !string.IsNullOrEmpty(updated.contentColorHex))
            {
                ApplyColor(updated.id);
            }
        }

        public List<BeefsRecipesPlugin.RecipeSection> GetPersonalSections()
        {
            return _personalSections;
        }

        public List<BeefsRecipesPlugin.RecipeSection> GetPublicSections()
        {
            return _publicSections;
        }

        private void RebuildUI()
        {
            foreach (var kvp in _sectionUIMap)
            {
                if (kvp.Value.SectionObject != null)
                {
                    UnityEngine.Object.Destroy(kvp.Value.SectionObject);
                }
            }
            _sectionUIMap.Clear();
            _allInputFields.Clear();
            _currentFocusedIndex = -1;

            foreach (var gap in _gapObjects)
            {
                if (gap != null) UnityEngine.Object.Destroy(gap);
            }
            _gapObjects.Clear();

            if (_dividerObject != null)
            {
                UnityEngine.Object.Destroy(_dividerObject);
                _dividerObject = null;
                _showHiddenButton = null;
                _showHiddenButtonText = null;
                _dividerLabelText = null;
                _dividerHintText = null;
            }

            if (_leftColumnHeader != null)
            {
                UnityEngine.Object.Destroy(_leftColumnHeader);
                _leftColumnHeader = null;
            }
            if (_rightColumnHeader != null)
            {
                UnityEngine.Object.Destroy(_rightColumnHeader);
                _rightColumnHeader = null;
            }

            if (_uiManager.RightContentObject != null)
            {
                Transform rightContent = _uiManager.RightContentObject.transform;
                for (int i = rightContent.childCount - 1; i >= 0; i--)
                    UnityEngine.Object.Destroy(rightContent.GetChild(i).gameObject);
            }

            var publicIds = new HashSet<string>();
            foreach (var s in _publicSections)
                publicIds.Add(s.id);
            _personalSections.RemoveAll(s => publicIds.Contains(s.id));

            var seenPersonal = new HashSet<string>();
            _personalSections.RemoveAll(s => !seenPersonal.Add(s.id));

            bool isFullscreen = _uiManager.IsFullscreenLayout;

            if (isFullscreen)
            {
                RebuildUI_Fullscreen();
            }
            else
            {
                RebuildUI_Sidebar();
            }

            ApplyState();
            Canvas.ForceUpdateCanvases();
        }

        private void RebuildUI_Sidebar()
        {
            if (_personalSections.Count == 0)
            {
                var gap = CreateInsertGap(0);
                _gapObjects.Add(gap);
            }

            for (int i = 0; i < _personalSections.Count; i++)
            {
                CreateAndRegisterSection(_personalSections[i]);

                var gap = CreateInsertGap(i + 1);
                _gapObjects.Add(gap);
            }

            int hiddenCount = 0;
            var visiblePublic = new List<BeefsRecipesPlugin.RecipeSection>();
            foreach (var section in _publicSections)
            {
                if (_hiddenSectionIds.Contains(section.id))
                {
                    hiddenCount++;
                    if (_showHiddenNotes)
                        visiblePublic.Add(section);
                }
                else
                {
                    visiblePublic.Add(section);
                }
            }

            _dividerObject = CreateDivider(hiddenCount);
            _dividerObject.SetActive(_publicSections.Count > 0 || BeefsRecipesPlugin.RuntimeContext.IsMultiplayer);

            foreach (var section in visiblePublic)
            {
                CreateAndRegisterSection(section);
            }
        }

        private void RebuildUI_Fullscreen()
        {
            Transform leftParent = _uiManager.ContentObject.transform;
            Transform rightParent = _uiManager.RightContentObject.transform;

            _leftColumnHeader = CreateColumnHeader(leftParent, "NOTES", new Color(0.7f, 0.85f, 1f, 0.7f));
            _rightColumnHeader = CreateColumnHeader(rightParent, "SHARED", new Color(1f, 0.39f, 0.08f, 0.7f));

            int hiddenCount = 0;
            foreach (var s in _publicSections)
            {
                if (_hiddenSectionIds.Contains(s.id)) hiddenCount++;
            }

            if (hiddenCount > 0)
            {
                GameObject toggleObj = new GameObject("ShowHiddenButton");
                toggleObj.transform.SetParent(rightParent, false);

                HorizontalLayoutGroup toggleRow = toggleObj.AddComponent<HorizontalLayoutGroup>();
                toggleRow.childControlWidth = false;
                toggleRow.childControlHeight = true;
                toggleRow.childForceExpandWidth = false;
                toggleRow.childAlignment = TextAnchor.MiddleLeft;

                LayoutElement toggleRowLE = toggleObj.AddComponent<LayoutElement>();
                toggleRowLE.preferredHeight = Mathf.RoundToInt(20 * BeefsRecipesPlugin.UIScaleMultiplier.Value);

                GameObject btnObj = new GameObject("ToggleBtn");
                btnObj.transform.SetParent(toggleObj.transform, false);

                Image btnBg = btnObj.AddComponent<Image>();
                btnBg.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                btnBg.raycastTarget = true;

                _showHiddenButton = btnObj.AddComponent<Button>();
                _showHiddenButton.targetGraphic = btnBg;
                _showHiddenButton.onClick.AddListener(OnShowHiddenToggled);

                LayoutElement btnLE = btnObj.AddComponent<LayoutElement>();
                btnLE.preferredWidth = 110 * BeefsRecipesPlugin.UIScaleMultiplier.Value;
                btnLE.preferredHeight = Mathf.RoundToInt(18 * BeefsRecipesPlugin.UIScaleMultiplier.Value);

                GameObject btnTextObj = new GameObject("Text");
                btnTextObj.transform.SetParent(btnObj.transform, false);

                RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
                btnTextRect.anchorMin = Vector2.zero;
                btnTextRect.anchorMax = Vector2.one;
                btnTextRect.offsetMin = new Vector2(4, 0);
                btnTextRect.offsetMax = new Vector2(-4, 0);

                _showHiddenButtonText = btnTextObj.AddComponent<Text>();
                _showHiddenButtonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                _showHiddenButtonText.text = _showHiddenNotes
                    ? $"Hide Hidden ({hiddenCount})"
                    : $"Show Hidden ({hiddenCount})";
                _showHiddenButtonText.fontSize = Mathf.Max(10, RecipesUIManager.PeekFontSize + _fontSizeOffset - 2);
                _showHiddenButtonText.color = new Color(0.7f, 0.7f, 0.7f);
                _showHiddenButtonText.alignment = TextAnchor.MiddleCenter;
                _showHiddenButtonText.raycastTarget = false;
            }

            if (_personalSections.Count == 0)
            {
                var gap = CreateInsertGap(0);
                gap.transform.SetParent(leftParent, false);
                _gapObjects.Add(gap);
            }

            for (int i = 0; i < _personalSections.Count; i++)
            {
                CreateAndRegisterSection(_personalSections[i], leftParent);

                var gap = CreateInsertGap(i + 1);
                _gapObjects.Add(gap);
            }

            var visiblePublic = new List<BeefsRecipesPlugin.RecipeSection>();
            foreach (var section in _publicSections)
            {
                if (_hiddenSectionIds.Contains(section.id))
                {
                    if (_showHiddenNotes)
                        visiblePublic.Add(section);
                }
                else
                {
                    visiblePublic.Add(section);
                }
            }

            foreach (var section in visiblePublic)
            {
                CreateAndRegisterSection(section, rightParent);
            }

            if (visiblePublic.Count == 0 && BeefsRecipesPlugin.RuntimeContext.IsMultiplayer)
            {
                GameObject hintObj = new GameObject("EmptyHint");
                hintObj.transform.SetParent(rightParent, false);

                LayoutElement hintLayout = hintObj.AddComponent<LayoutElement>();
                hintLayout.preferredHeight = 30;

                Text hintText = hintObj.AddComponent<Text>();
                hintText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                hintText.text = "Drag notes here to share";
                hintText.fontSize = Mathf.Max(10, RecipesUIManager.PeekFontSize + _fontSizeOffset);
                hintText.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                hintText.alignment = TextAnchor.MiddleCenter;
                hintText.fontStyle = FontStyle.Italic;
                hintText.raycastTarget = false;
            }
        }

        private GameObject CreateColumnHeader(Transform parent, string text, Color color)
        {
            GameObject headerObj = new GameObject("ColumnHeader");
            headerObj.transform.SetParent(parent, false);

            LayoutElement le = headerObj.AddComponent<LayoutElement>();
            le.preferredHeight = 30;

            Text headerText = headerObj.AddComponent<Text>();
            headerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            headerText.text = text;
            headerText.fontSize = RecipesUIManager.TitleFontSize + _fontSizeOffset + 2;
            headerText.color = color;
            headerText.alignment = TextAnchor.MiddleLeft;
            headerText.fontStyle = FontStyle.Bold;
            headerText.raycastTarget = false;

            return headerObj;
        }

        private void CreateAndRegisterSection(BeefsRecipesPlugin.RecipeSection section, Transform parentOverride = null)
        {
            var sectionUI = CreateNoteSection(section, parentOverride);
            _sectionUIMap[section.id] = sectionUI;
            _allInputFields.Add(sectionUI.TitleField);
            if (sectionUI.ContentField != null)
                _allInputFields.Add(sectionUI.ContentField);

            if (!string.IsNullOrEmpty(section.titleColorHex) || !string.IsNullOrEmpty(section.contentColorHex))
            {
                ApplyColor(section.id);
            }

            if (section.isCollapsed)
            {
                CollapseSection(section.id);
            }
        }

        private GameObject CreateInsertGap(int insertIndex)
        {
            float gapScale = RecipesUIManager.ScaleFactor;

            GameObject gapObj = new GameObject($"InsertGap_{insertIndex}");
            gapObj.transform.SetParent(_uiManager.ContentObject.transform, false);

            LayoutElement gapLayout = gapObj.AddComponent<LayoutElement>();
            gapLayout.preferredHeight = Mathf.Max(16, Mathf.RoundToInt(20 * gapScale));
            gapLayout.minHeight = Mathf.Max(16, Mathf.RoundToInt(20 * gapScale));
            gapLayout.flexibleWidth = 1f;

            RectTransform gapRect = gapObj.GetComponent<RectTransform>();
            gapRect.sizeDelta = new Vector2(0f, Mathf.Max(16f, Mathf.RoundToInt(20 * gapScale)));

            Image gapImage = gapObj.AddComponent<Image>();
            gapImage.color = new Color(0, 0, 0, 0);
            gapImage.raycastTarget = false;

            int capturedIndex = insertIndex;
            float pillWidth = Mathf.Max(50f, 70f * gapScale);
            float pillHeight = Mathf.Max(16f, 20f * gapScale);
            float pillGap = 6f * gapScale;
            float totalWidth = pillWidth * 2 + pillGap;
            int pillFontSize = Mathf.Max(10, Mathf.RoundToInt(11 * gapScale));

            CreateGapPillButton(
                gapObj.transform, "+Note",
                new Color(0.2f, 0.6f, 0.2f, 0.7f),
                new Color(0.3f, 0.7f, 0.3f, 0.9f),
                new Color(0.4f, 0.8f, 0.4f, 1f),
                new Color(0.3f, 0.7f, 0.3f, 0.35f),
                pillWidth, pillHeight, pillFontSize,
                new Vector2(-totalWidth * 0.5f, 0),
                () => InsertSectionAt(capturedIndex));

            CreateGapPillButton(
                gapObj.transform, "+Draw",
                new Color(0.2f, 0.4f, 0.7f, 0.7f),
                new Color(0.3f, 0.5f, 0.8f, 0.9f),
                new Color(0.4f, 0.6f, 0.9f, 1f),
                new Color(0.3f, 0.5f, 0.8f, 0.35f),
                pillWidth, pillHeight, pillFontSize,
                new Vector2(-totalWidth * 0.5f + pillWidth + pillGap, 0),
                () => InsertDrawingAt(capturedIndex));

            gapObj.SetActive(false);

            return gapObj;
        }

        private void InsertSectionAt(int index)
        {
            BeefsRecipesPlugin.RecipeSection newSection = CreateEmptySection();
            index = Mathf.Clamp(index, 0, _personalSections.Count);
            _personalSections.Insert(index, newSection);
            RebuildUI();
            _layoutRebuildFrames = 3;
        }

        private void InsertDrawingAt(int index)
        {
            var newSection = CreateDrawingSection();
            index = Mathf.Clamp(index, 0, _personalSections.Count);
            _personalSections.Insert(index, newSection);
            RebuildUI();
            _layoutRebuildFrames = 3;
        }

        private GameObject CreateGapPillButton(
            Transform parent, string label,
            Color normalColor, Color highlightColor, Color pressedColor,
            Color borderColor, float width, float height, int fontSize,
            Vector2 offset, Action onClick)
        {
            GameObject pillObj = new GameObject($"Pill_{label}");
            pillObj.transform.SetParent(parent, false);

            RectTransform pillRect = pillObj.AddComponent<RectTransform>();
            pillRect.anchorMin = new Vector2(0.5f, 0.5f);
            pillRect.anchorMax = new Vector2(0.5f, 0.5f);
            pillRect.pivot = new Vector2(0, 0.5f);
            pillRect.anchoredPosition = offset;
            pillRect.sizeDelta = new Vector2(width, height);

            LayoutElement pillLE = pillObj.AddComponent<LayoutElement>();
            pillLE.ignoreLayout = true;

            GameObject borderObj = new GameObject("PillBorder");
            borderObj.transform.SetParent(pillObj.transform, false);

            RectTransform borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = Vector2.zero;
            borderRect.offsetMax = Vector2.zero;

            Image borderImage = borderObj.AddComponent<Image>();
            borderImage.sprite = GetPillSprite();
            borderImage.type = Image.Type.Sliced;
            borderImage.color = borderColor;
            borderImage.raycastTarget = false;

            GameObject innerObj = new GameObject("PillInner");
            innerObj.transform.SetParent(pillObj.transform, false);

            RectTransform innerRect = innerObj.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(1.5f, 1.5f);
            innerRect.offsetMax = new Vector2(-1.5f, -1.5f);

            Image innerImage = innerObj.AddComponent<Image>();
            innerImage.sprite = GetPillSprite();
            innerImage.type = Image.Type.Sliced;
            innerImage.color = new Color(0.08f, 0.08f, 0.08f, 0.95f);
            innerImage.raycastTarget = false;

            Image pillHitArea = pillObj.AddComponent<Image>();
            pillHitArea.color = new Color(0, 0, 0, 0);
            pillHitArea.raycastTarget = true;

            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(pillObj.transform, false);

            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillBg = fillObj.AddComponent<Image>();
            fillBg.sprite = GetPillSprite();
            fillBg.type = Image.Type.Sliced;
            fillBg.color = normalColor;
            fillBg.raycastTarget = true;

            Button button = fillObj.AddComponent<Button>();
            button.targetGraphic = fillBg;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = normalColor;
            colors.highlightedColor = highlightColor;
            colors.pressedColor = pressedColor;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            button.onClick.AddListener(() => onClick?.Invoke());

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(fillObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = label;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.fontStyle = FontStyle.Bold;
            text.raycastTarget = false;

            fillObj.SetActive(false);

            EventTrigger trigger = pillObj.AddComponent<EventTrigger>();

            EventTrigger.Entry enterEntry = new EventTrigger.Entry();
            enterEntry.eventID = EventTriggerType.PointerEnter;
            enterEntry.callback.AddListener((_) => fillObj.SetActive(true));
            trigger.triggers.Add(enterEntry);

            EventTrigger.Entry exitEntry = new EventTrigger.Entry();
            exitEntry.eventID = EventTriggerType.PointerExit;
            exitEntry.callback.AddListener((_) => fillObj.SetActive(false));
            trigger.triggers.Add(exitEntry);

            return pillObj;
        }

        private void UpdateDragHandleSizes(float width)
        {
            foreach (var kvp in _sectionUIMap)
            {
                if (kvp.Value.DragHandle == null) continue;
                var handleRect = kvp.Value.DragHandle.GetComponent<RectTransform>();
                if (handleRect != null)
                {
                    handleRect.anchoredPosition = new Vector2(-width, 0);
                    handleRect.sizeDelta = new Vector2(width, 0);
                }
            }
        }

        private GameObject CreateDivider(int hiddenCount)
        {
            GameObject dividerObj = new GameObject("SharedNotesDivider");
            dividerObj.transform.SetParent(_uiManager.ContentObject.transform, false);

            VerticalLayoutGroup dividerLayout = dividerObj.AddComponent<VerticalLayoutGroup>();
            dividerLayout.childControlHeight = false;
            dividerLayout.childControlWidth = true;
            dividerLayout.childForceExpandWidth = true;
            dividerLayout.spacing = 4f;
            dividerLayout.padding = new RectOffset(5, 5, 10, 5);

            ContentSizeFitter dividerFitter = dividerObj.AddComponent<ContentSizeFitter>();
            dividerFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject lineObj = new GameObject("DividerLine");
            lineObj.transform.SetParent(dividerObj.transform, false);

            RectTransform lineRect = lineObj.AddComponent<RectTransform>();
            lineRect.sizeDelta = new Vector2(0, 1);

            LayoutElement lineLayout = lineObj.AddComponent<LayoutElement>();
            lineLayout.preferredHeight = 1;
            lineLayout.flexibleWidth = 1;

            Image lineImage = lineObj.AddComponent<Image>();
            lineImage.color = new Color(1f, 0.39f, 0.08f, 0.5f);
            lineImage.raycastTarget = false;

            GameObject labelRow = new GameObject("DividerLabelRow");
            labelRow.transform.SetParent(dividerObj.transform, false);

            HorizontalLayoutGroup labelRowLayout = labelRow.AddComponent<HorizontalLayoutGroup>();
            labelRowLayout.childControlWidth = false;
            labelRowLayout.childControlHeight = true;
            labelRowLayout.childForceExpandWidth = false;
            labelRowLayout.childForceExpandHeight = false;
            labelRowLayout.spacing = 10f;

            LayoutElement labelRowLayoutElem = labelRow.AddComponent<LayoutElement>();
            labelRowLayoutElem.preferredHeight = 20;

            ContentSizeFitter labelRowFitter = labelRow.AddComponent<ContentSizeFitter>();
            labelRowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject labelObj = new GameObject("DividerLabel");
            labelObj.transform.SetParent(labelRow.transform, false);

            Text labelText = labelObj.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.text = "SHARED NOTES";
            labelText.fontSize = RecipesUIManager.PeekFontSize + _fontSizeOffset;
            labelText.color = new Color(1f, 0.39f, 0.08f, 0.7f);
            labelText.alignment = TextAnchor.MiddleLeft;
            labelText.fontStyle = FontStyle.Bold;
            labelText.raycastTarget = false;
            _dividerLabelText = labelText;

            LayoutElement labelLayout = labelObj.AddComponent<LayoutElement>();
            labelLayout.preferredWidth = 120;
            labelLayout.preferredHeight = 20;

            if (hiddenCount > 0)
            {
                GameObject toggleObj = new GameObject("ShowHiddenButton");
                toggleObj.transform.SetParent(labelRow.transform, false);

                Image toggleBg = toggleObj.AddComponent<Image>();
                toggleBg.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                toggleBg.raycastTarget = true;

                _showHiddenButton = toggleObj.AddComponent<Button>();
                _showHiddenButton.targetGraphic = toggleBg;
                _showHiddenButton.onClick.AddListener(OnShowHiddenToggled);

                LayoutElement toggleLayout = toggleObj.AddComponent<LayoutElement>();
                toggleLayout.preferredWidth = 110 * BeefsRecipesPlugin.UIScaleMultiplier.Value;
                toggleLayout.preferredHeight = Mathf.RoundToInt(18 * BeefsRecipesPlugin.UIScaleMultiplier.Value);

                GameObject toggleTextObj = new GameObject("Text");
                toggleTextObj.transform.SetParent(toggleObj.transform, false);

                RectTransform toggleTextRect = toggleTextObj.AddComponent<RectTransform>();
                toggleTextRect.anchorMin = Vector2.zero;
                toggleTextRect.anchorMax = Vector2.one;
                toggleTextRect.offsetMin = new Vector2(4, 0);
                toggleTextRect.offsetMax = new Vector2(-4, 0);

                _showHiddenButtonText = toggleTextObj.AddComponent<Text>();
                _showHiddenButtonText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                _showHiddenButtonText.text = _showHiddenNotes
                    ? $"Hide Hidden ({hiddenCount})"
                    : $"Show Hidden ({hiddenCount})";
                _showHiddenButtonText.fontSize = Mathf.Max(10, RecipesUIManager.PeekFontSize + _fontSizeOffset - 2);
                _showHiddenButtonText.color = new Color(0.7f, 0.7f, 0.7f);
                _showHiddenButtonText.alignment = TextAnchor.MiddleCenter;
                _showHiddenButtonText.raycastTarget = false;
            }

            bool hasPublicContent = _publicSections.Count > 0;
            if (!hasPublicContent && BeefsRecipesPlugin.RuntimeContext.IsMultiplayer)
            {
                GameObject hintObj = new GameObject("EmptyHint");
                hintObj.transform.SetParent(dividerObj.transform, false);

                LayoutElement hintLayout = hintObj.AddComponent<LayoutElement>();
                hintLayout.preferredHeight = 20;

                Text hintText = hintObj.AddComponent<Text>();
                hintText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                hintText.text = "Drag a note section below this line to share it";
                hintText.fontSize = Mathf.Max(10, RecipesUIManager.PeekFontSize + _fontSizeOffset - 2);
                hintText.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);
                hintText.alignment = TextAnchor.MiddleLeft;
                hintText.fontStyle = FontStyle.Italic;
                hintText.raycastTarget = false;
                _dividerHintText = hintText;
            }

            return dividerObj;
        }

        private RecipeSectionUI CreateNoteSection(BeefsRecipesPlugin.RecipeSection section, Transform parentOverride = null)
        {
            GameObject sectionObj = new GameObject($"Section_{section.id}");
            sectionObj.transform.SetParent(parentOverride ?? _uiManager.ContentObject.transform, false);

            Image sectionRaycast = sectionObj.AddComponent<Image>();
            sectionRaycast.color = new Color(0, 0, 0, 0);
            sectionRaycast.raycastTarget = true;

            VerticalLayoutGroup sectionLayout = sectionObj.AddComponent<VerticalLayoutGroup>();
            sectionLayout.childControlHeight = false;
            sectionLayout.childControlWidth = true;
            sectionLayout.childForceExpandHeight = false;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.spacing = 3f;
            sectionLayout.padding = new RectOffset(5, 5, 2, 2);

            ContentSizeFitter sectionFitter = sectionObj.AddComponent<ContentSizeFitter>();
            sectionFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject contentRow = new GameObject("ContentRow");
            contentRow.transform.SetParent(sectionObj.transform, false);

            HorizontalLayoutGroup contentRowLayout = contentRow.AddComponent<HorizontalLayoutGroup>();
            contentRowLayout.childControlWidth = true;
            contentRowLayout.childControlHeight = true;
            contentRowLayout.childForceExpandWidth = false;
            contentRowLayout.childForceExpandHeight = false;
            contentRowLayout.spacing = 5f;
            contentRowLayout.childAlignment = TextAnchor.UpperLeft;

            ContentSizeFitter contentRowFitter = contentRow.AddComponent<ContentSizeFitter>();
            contentRowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject textBoxContainer = new GameObject("TextBoxContainer");
            textBoxContainer.transform.SetParent(contentRow.transform, false);

            LayoutElement textBoxLayout = textBoxContainer.AddComponent<LayoutElement>();
            textBoxLayout.flexibleWidth = 1f;

            VerticalLayoutGroup textBoxVerticalLayout = textBoxContainer.AddComponent<VerticalLayoutGroup>();
            textBoxVerticalLayout.childControlHeight = false;
            textBoxVerticalLayout.childControlWidth = true;
            textBoxVerticalLayout.childForceExpandHeight = false;
            textBoxVerticalLayout.childForceExpandWidth = true;
            textBoxVerticalLayout.spacing = 3f;
            textBoxVerticalLayout.padding = new RectOffset(0, 0, 0, 0);

            ContentSizeFitter textBoxFitter = textBoxContainer.AddComponent<ContentSizeFitter>();
            textBoxFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            InputField titleField = _uiManager.CreateInputField(textBoxContainer.transform, "TitleField", RecipesUIManager.TitleFontSize, false);
            titleField.text = section.title;
            titleField.characterLimit = RecipesUIManager.TitleMaxChars;
            _uiManager.SetPlaceholder(titleField, "NOTES");
            titleField.onValueChanged.AddListener((value) => {
                OnSectionTitleChanged(section.id, value);
            });
            titleField.onValidateInput = BlockTabAndNewline;

            GameObject titleDisplayObject = new GameObject("TitleDisplay");
            titleDisplayObject.transform.SetParent(textBoxContainer.transform, false);

            RectTransform titleDisplayRect = titleDisplayObject.AddComponent<RectTransform>();
            titleDisplayRect.sizeDelta = new Vector2(0, RecipesUIManager.TitleFontSize + 10);

            Text titleDisplayText = titleDisplayObject.AddComponent<Text>();
            titleDisplayText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            titleDisplayText.fontSize = RecipesUIManager.TitleFontSize;
            titleDisplayText.color = Color.white;
            titleDisplayText.alignment = TextAnchor.MiddleLeft;
            titleDisplayText.horizontalOverflow = HorizontalWrapMode.Wrap;
            titleDisplayText.verticalOverflow = VerticalWrapMode.Overflow;
            titleDisplayText.supportRichText = true;
            titleDisplayText.raycastTarget = false;

            LayoutElement titleDisplayLayout = titleDisplayObject.AddComponent<LayoutElement>();
            titleDisplayLayout.minHeight = RecipesUIManager.TitleFontSize + 10;
            titleDisplayLayout.preferredHeight = RecipesUIManager.TitleFontSize + 10;

            titleDisplayObject.SetActive(false);
            titleField.gameObject.SetActive(true);

            GameObject collapsedPreviewObject = new GameObject("CollapsedPreview");
            collapsedPreviewObject.transform.SetParent(textBoxContainer.transform, false);

            RectTransform collapsedPreviewRect = collapsedPreviewObject.AddComponent<RectTransform>();
            collapsedPreviewRect.sizeDelta = new Vector2(0, (RecipesUIManager.TitleFontSize + 10) * 2);

            Text collapsedPreviewText = collapsedPreviewObject.AddComponent<Text>();
            collapsedPreviewText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            collapsedPreviewText.fontSize = RecipesUIManager.TitleFontSize;
            collapsedPreviewText.color = Color.white;
            collapsedPreviewText.alignment = TextAnchor.UpperLeft;
            collapsedPreviewText.horizontalOverflow = HorizontalWrapMode.Wrap;
            collapsedPreviewText.verticalOverflow = VerticalWrapMode.Overflow;
            collapsedPreviewText.supportRichText = false;
            collapsedPreviewText.raycastTarget = true;

            LayoutElement collapsedPreviewLayout = collapsedPreviewObject.AddComponent<LayoutElement>();
            collapsedPreviewLayout.minHeight = (RecipesUIManager.TitleFontSize + 10) * 2;
            collapsedPreviewLayout.preferredHeight = (RecipesUIManager.TitleFontSize + 10) * 2;

            Button collapsedClickButton = collapsedPreviewObject.AddComponent<Button>();
            collapsedClickButton.targetGraphic = collapsedPreviewText;
            collapsedClickButton.transition = Selectable.Transition.None;
            string capturedSectionIdForCollapse = section.id;
            collapsedClickButton.onClick.AddListener(() => OnCollapsedSectionClicked(capturedSectionIdForCollapse));

            collapsedPreviewObject.SetActive(false);

            InputField contentField = null;
            GameObject displayObject = null;
            Text displayText = null;
            RawImage drawingCanvas = null;
            GameObject drawingCanvasWrapper = null;
            Image drawingWrapperBg = null;
            GameObject drawingEditHint = null;
            RecipesSketchPad sketchPad = null;
            GameObject drawingToolbar = null;
            Button undoButton = null;
            Image eraserButtonImage = null;
            Image brushColorSwatch = null;
            Image brushIconImage = null;
            Image eraserIconImage = null;
            Text toolModeLabel = null;
            Image bgCheckboxImage = null;

            if (section.isDrawing)
            {
                textBoxVerticalLayout.childControlHeight = true;
                textBoxVerticalLayout.spacing = 1f;
                sectionLayout.padding = new RectOffset(5, 5, 4, 2);

                float sf = RecipesUIManager.ScaleFactor;
                int scaledFontSize = Mathf.Max(8, Mathf.RoundToInt(11 * sf));
                bool compactToolbar = sf < 0.7f;

                drawingToolbar = new GameObject("DrawingToolbar");
                drawingToolbar.transform.SetParent(textBoxContainer.transform, false);

                VerticalLayoutGroup toolbarVLayout = drawingToolbar.AddComponent<VerticalLayoutGroup>();
                toolbarVLayout.childControlWidth = true;
                toolbarVLayout.childControlHeight = true;
                toolbarVLayout.childForceExpandWidth = true;
                toolbarVLayout.childForceExpandHeight = false;
                float rowSpacing = Mathf.Max(2f, 2f * sf);
                toolbarVLayout.spacing = rowSpacing;
                float topPad = Mathf.Max(4f, 10f * sf);
                toolbarVLayout.padding = new RectOffset(0, 0, Mathf.RoundToInt(topPad), 0);

                float rowHeight = Mathf.Max(18f, 22f * sf);
                int numRows = compactToolbar ? 4 : 2;
                LayoutElement toolbarLE = drawingToolbar.AddComponent<LayoutElement>();
                toolbarLE.preferredHeight = rowHeight * numRows + rowSpacing * (numRows - 1) + topPad;
                toolbarLE.minHeight = toolbarLE.preferredHeight;
                toolbarLE.flexibleHeight = 0f;

                GameObject toolRow = new GameObject("ToolRow");
                toolRow.transform.SetParent(drawingToolbar.transform, false);

                HorizontalLayoutGroup toolRowLayout = toolRow.AddComponent<HorizontalLayoutGroup>();
                toolRowLayout.childControlWidth = true;
                toolRowLayout.childControlHeight = true;
                toolRowLayout.childForceExpandWidth = false;
                toolRowLayout.childForceExpandHeight = false;
                toolRowLayout.spacing = Mathf.Max(3f, 4f * sf);
                toolRowLayout.padding = new RectOffset(Mathf.Max(2, Mathf.RoundToInt(2 * sf)), Mathf.Max(2, Mathf.RoundToInt(2 * sf)), 0, 0);
                toolRowLayout.childAlignment = TextAnchor.MiddleLeft;

                LayoutElement toolRowLE = toolRow.AddComponent<LayoutElement>();
                toolRowLE.preferredHeight = rowHeight;
                toolRowLE.minHeight = rowHeight;
                toolRowLE.flexibleHeight = 0f;

                GameObject sliderRow = null;
                if (compactToolbar)
                {
                    sliderRow = new GameObject("SliderRow");
                    sliderRow.transform.SetParent(drawingToolbar.transform, false);

                    HorizontalLayoutGroup sliderRowLayout = sliderRow.AddComponent<HorizontalLayoutGroup>();
                    sliderRowLayout.childControlWidth = true;
                    sliderRowLayout.childControlHeight = true;
                    sliderRowLayout.childForceExpandWidth = true;
                    sliderRowLayout.childForceExpandHeight = false;
                    sliderRowLayout.spacing = Mathf.Max(3f, 4f * sf);
                    sliderRowLayout.padding = new RectOffset(Mathf.Max(2, Mathf.RoundToInt(2 * sf)), Mathf.Max(2, Mathf.RoundToInt(2 * sf)), 0, 0);
                    sliderRowLayout.childAlignment = TextAnchor.MiddleLeft;

                    LayoutElement sliderRowLE = sliderRow.AddComponent<LayoutElement>();
                    sliderRowLE.preferredHeight = rowHeight;
                    sliderRowLE.minHeight = rowHeight;
                    sliderRowLE.flexibleHeight = 0f;
                }
                Transform sliderParent = compactToolbar ? sliderRow.transform : toolRow.transform;

                {
                    GameObject colorObj = new GameObject("BrushColorBtn");
                    colorObj.transform.SetParent(toolRow.transform, false);

                    LayoutElement colorLE = colorObj.AddComponent<LayoutElement>();
                    colorLE.preferredWidth = Mathf.Max(60f, 95f * sf);
                    colorLE.preferredHeight = rowHeight;

                    Image colorBg = colorObj.AddComponent<Image>();
                    colorBg.sprite = GetPillSprite();
                    colorBg.type = Image.Type.Sliced;
                    colorBg.color = new Color(0.55f, 0.55f, 0.55f, 0.85f);
                    colorBg.raycastTarget = true;

                    Outline colorOutline = colorObj.AddComponent<Outline>();
                    colorOutline.effectColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
                    colorOutline.effectDistance = new Vector2(1f, -1f);

                    Button colorBtn = colorObj.AddComponent<Button>();
                    colorBtn.targetGraphic = colorBg;
                    colorBtn.transition = Selectable.Transition.ColorTint;
                    ColorBlock cc = colorBtn.colors;
                    cc.normalColor = new Color(0.55f, 0.55f, 0.55f, 0.85f);
                    cc.highlightedColor = new Color(0.65f, 0.65f, 0.65f, 0.95f);
                    cc.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
                    cc.fadeDuration = 0.05f;
                    colorBtn.colors = cc;

                    string capturedIdForColor = section.id;
                    colorBtn.onClick.AddListener(() => ShowDrawingColorPicker(capturedIdForColor));

                    GameObject swatchObj = new GameObject("Swatch");
                    swatchObj.transform.SetParent(colorObj.transform, false);

                    RectTransform swatchRect = swatchObj.AddComponent<RectTransform>();
                    swatchRect.anchorMin = new Vector2(0, 0.5f);
                    swatchRect.anchorMax = new Vector2(0, 0.5f);
                    swatchRect.pivot = new Vector2(0, 0.5f);
                    swatchRect.anchoredPosition = new Vector2(7f * sf, 0);
                    swatchRect.sizeDelta = new Vector2(14f * sf, 14f * sf);

                    brushColorSwatch = swatchObj.AddComponent<Image>();
                    brushColorSwatch.color = Color.white;
                    brushColorSwatch.raycastTarget = false;

                    GameObject labelObj = new GameObject("Label");
                    labelObj.transform.SetParent(colorObj.transform, false);

                    RectTransform labelRect = labelObj.AddComponent<RectTransform>();
                    labelRect.anchorMin = new Vector2(0, 0);
                    labelRect.anchorMax = new Vector2(1, 1);
                    labelRect.offsetMin = new Vector2(25f * sf, 0);
                    labelRect.offsetMax = new Vector2(-4f * sf, 0);

                    Text colorText = labelObj.AddComponent<Text>();
                    colorText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    colorText.text = "Color";
                    colorText.fontSize = scaledFontSize;
                    colorText.color = new Color(1f, 1f, 1f, 0.9f);
                    colorText.alignment = TextAnchor.MiddleLeft;
                    colorText.raycastTarget = false;
                }

                {
                    GameObject sizeContainer = new GameObject("SizeContainer");
                    sizeContainer.transform.SetParent(sliderParent, false);

                    LayoutElement containerLE = sizeContainer.AddComponent<LayoutElement>();
                    containerLE.preferredWidth = compactToolbar ? 0f : Mathf.Max(120f, 190f * sf);
                    containerLE.flexibleWidth = compactToolbar ? 1f : 0f;

                    Image containerBg = sizeContainer.AddComponent<Image>();
                    containerBg.sprite = GetPillSprite();
                    containerBg.type = Image.Type.Sliced;
                    containerBg.color = new Color(0.18f, 0.18f, 0.18f, 0.9f);
                    containerBg.raycastTarget = false;

                    Outline sizeOutline = sizeContainer.AddComponent<Outline>();
                    sizeOutline.effectColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
                    sizeOutline.effectDistance = new Vector2(1f, -1f);

                    HorizontalLayoutGroup containerLayout = sizeContainer.AddComponent<HorizontalLayoutGroup>();
                    containerLayout.childControlWidth = true;
                    containerLayout.childControlHeight = true;
                    containerLayout.childForceExpandWidth = false;
                    containerLayout.childForceExpandHeight = false;
                    containerLayout.spacing = 2f * sf;
                    containerLayout.padding = new RectOffset(
                        Mathf.RoundToInt(6 * sf), Mathf.RoundToInt(4 * sf),
                        Mathf.RoundToInt(2 * sf), Mathf.RoundToInt(2 * sf));
                    containerLayout.childAlignment = TextAnchor.MiddleLeft;

                    GameObject labelObj = new GameObject("SizeLabel");
                    labelObj.transform.SetParent(sizeContainer.transform, false);

                    Text label = labelObj.AddComponent<Text>();
                    label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    label.text = "Size";
                    label.fontSize = scaledFontSize;
                    label.color = new Color(0.65f, 0.65f, 0.65f, 0.9f);
                    label.alignment = TextAnchor.MiddleLeft;
                    label.raycastTarget = false;

                    LayoutElement labelLE = labelObj.AddComponent<LayoutElement>();
                    labelLE.preferredWidth = 26f * sf;
                    labelLE.flexibleWidth = 0;

                    GameObject sliderObj = CreateDrawingSlider(sizeContainer.transform, 1f, 30f, 3f);
                    Slider sizeSlider = sliderObj.GetComponent<Slider>();

                    LayoutElement sliderLE = sliderObj.GetComponent<LayoutElement>();
                    if (sliderLE == null) sliderLE = sliderObj.AddComponent<LayoutElement>();
                    sliderLE.flexibleWidth = 1f;
                    sliderLE.preferredHeight = 16f * sf;

                    string capturedIdForSize = section.id;
                    sizeSlider.onValueChanged.AddListener((val) =>
                    {
                        if (_sectionUIMap.TryGetValue(capturedIdForSize, out var ui) && ui.SketchPad != null)
                            ui.SketchPad.SetBrushSize(val);
                    });
                }

                {
                    GameObject toggleObj = new GameObject("BrushEraserToggle");
                    toggleObj.transform.SetParent(toolRow.transform, false);

                    LayoutElement toggleLE = toggleObj.AddComponent<LayoutElement>();
                    toggleLE.preferredWidth = Mathf.Max(60f, 95f * sf);
                    toggleLE.preferredHeight = rowHeight;

                    Image toggleBg = toggleObj.AddComponent<Image>();
                    toggleBg.sprite = GetPillSprite();
                    toggleBg.type = Image.Type.Sliced;
                    toggleBg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
                    toggleBg.raycastTarget = true;

                    Outline toggleOutline = toggleObj.AddComponent<Outline>();
                    toggleOutline.effectColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
                    toggleOutline.effectDistance = new Vector2(1f, -1f);

                    Button toggleBtn = toggleObj.AddComponent<Button>();
                    toggleBtn.targetGraphic = toggleBg;
                    toggleBtn.transition = Selectable.Transition.None;

                    string capturedIdForToggle = section.id;
                    toggleBtn.onClick.AddListener(() => ToggleEraser(capturedIdForToggle));

                    GameObject brushIconObj = new GameObject("BrushIcon");
                    brushIconObj.transform.SetParent(toggleObj.transform, false);

                    RectTransform brushIconRect = brushIconObj.AddComponent<RectTransform>();
                    brushIconRect.anchorMin = new Vector2(0, 0.5f);
                    brushIconRect.anchorMax = new Vector2(0, 0.5f);
                    brushIconRect.pivot = new Vector2(0, 0.5f);
                    brushIconRect.anchoredPosition = new Vector2(8f * sf, 0);
                    brushIconRect.sizeDelta = new Vector2(16f * sf, 16f * sf);

                    brushIconImage = brushIconObj.AddComponent<Image>();
                    brushIconImage.sprite = RecipesUIManager.CreateBrushSprite();
                    brushIconImage.color = new Color(1f, 0.5f, 0.15f, 1f);
                    brushIconImage.raycastTarget = false;

                    GameObject modeLabelObj = new GameObject("ModeLabel");
                    modeLabelObj.transform.SetParent(toggleObj.transform, false);

                    RectTransform modeLabelRect = modeLabelObj.AddComponent<RectTransform>();
                    modeLabelRect.anchorMin = new Vector2(0, 0);
                    modeLabelRect.anchorMax = new Vector2(1, 1);
                    modeLabelRect.offsetMin = new Vector2(28f * sf, 0);
                    modeLabelRect.offsetMax = new Vector2(-28f * sf, 0);

                    toolModeLabel = modeLabelObj.AddComponent<Text>();
                    toolModeLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    toolModeLabel.text = "Brush";
                    toolModeLabel.fontSize = scaledFontSize;
                    toolModeLabel.color = new Color(1f, 1f, 1f, 0.9f);
                    toolModeLabel.alignment = TextAnchor.MiddleCenter;
                    toolModeLabel.raycastTarget = false;

                    GameObject eraserIconObj = new GameObject("EraserIcon");
                    eraserIconObj.transform.SetParent(toggleObj.transform, false);

                    RectTransform eraserIconRect = eraserIconObj.AddComponent<RectTransform>();
                    eraserIconRect.anchorMin = new Vector2(1, 0.5f);
                    eraserIconRect.anchorMax = new Vector2(1, 0.5f);
                    eraserIconRect.pivot = new Vector2(1, 0.5f);
                    eraserIconRect.anchoredPosition = new Vector2(-8f * sf, 0);
                    eraserIconRect.sizeDelta = new Vector2(16f * sf, 16f * sf);

                    eraserIconImage = eraserIconObj.AddComponent<Image>();
                    eraserIconImage.sprite = RecipesUIManager.CreateEraserSprite();
                    eraserIconImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
                    eraserIconImage.raycastTarget = false;

                    eraserButtonImage = toggleBg;
                }

                GameObject actionRow = new GameObject("ActionRow");
                actionRow.transform.SetParent(drawingToolbar.transform, false);

                HorizontalLayoutGroup actionRowLayout = actionRow.AddComponent<HorizontalLayoutGroup>();
                actionRowLayout.childControlWidth = false;
                actionRowLayout.childControlHeight = true;
                actionRowLayout.childForceExpandWidth = false;
                actionRowLayout.childForceExpandHeight = false;
                actionRowLayout.spacing = Mathf.Max(3f, 4f * sf);
                actionRowLayout.padding = new RectOffset(Mathf.Max(2, Mathf.RoundToInt(2 * sf)), Mathf.Max(2, Mathf.RoundToInt(2 * sf)), 0, 0);
                actionRowLayout.childAlignment = TextAnchor.MiddleLeft;

                LayoutElement actionRowLE = actionRow.AddComponent<LayoutElement>();
                actionRowLE.preferredHeight = rowHeight;
                actionRowLE.minHeight = rowHeight;
                actionRowLE.flexibleHeight = 0f;

                GameObject actionRow2 = null;
                if (compactToolbar)
                {
                    actionRow2 = new GameObject("ActionRow2");
                    actionRow2.transform.SetParent(drawingToolbar.transform, false);

                    HorizontalLayoutGroup actionRow2Layout = actionRow2.AddComponent<HorizontalLayoutGroup>();
                    actionRow2Layout.childControlWidth = false;
                    actionRow2Layout.childControlHeight = true;
                    actionRow2Layout.childForceExpandWidth = false;
                    actionRow2Layout.childForceExpandHeight = false;
                    actionRow2Layout.spacing = Mathf.Max(3f, 4f * sf);
                    actionRow2Layout.padding = new RectOffset(Mathf.Max(2, Mathf.RoundToInt(2 * sf)), Mathf.Max(2, Mathf.RoundToInt(2 * sf)), 0, 0);
                    actionRow2Layout.childAlignment = TextAnchor.MiddleLeft;

                    LayoutElement actionRow2LE = actionRow2.AddComponent<LayoutElement>();
                    actionRow2LE.preferredHeight = rowHeight;
                    actionRow2LE.minHeight = rowHeight;
                    actionRow2LE.flexibleHeight = 0f;
                }
                Transform cancelSaveParent = compactToolbar ? actionRow2.transform : actionRow.transform;

                {
                    GameObject bgCheckObj = new GameObject("BgCheckbox");
                    bgCheckObj.transform.SetParent(actionRow.transform, false);

                    LayoutElement bgCheckLE = bgCheckObj.AddComponent<LayoutElement>();
                    bgCheckLE.preferredWidth = Mathf.Max(22f, 30f * sf);
                    bgCheckLE.preferredHeight = rowHeight;

                    Image bgCheckRaycast = bgCheckObj.AddComponent<Image>();
                    bgCheckRaycast.color = new Color(0, 0, 0, 0);
                    bgCheckRaycast.raycastTarget = true;

                    Button bgCheckBtn = bgCheckObj.AddComponent<Button>();
                    bgCheckBtn.targetGraphic = bgCheckRaycast;
                    bgCheckBtn.transition = Selectable.Transition.None;

                    string capturedIdForBg = section.id;
                    bgCheckBtn.onClick.AddListener(() => ToggleDrawingBackground(capturedIdForBg));

                    GameObject checkIconObj = new GameObject("CheckIcon");
                    checkIconObj.transform.SetParent(bgCheckObj.transform, false);

                    RectTransform checkIconRect = checkIconObj.AddComponent<RectTransform>();
                    checkIconRect.anchorMin = new Vector2(0, 0.5f);
                    checkIconRect.anchorMax = new Vector2(0, 0.5f);
                    checkIconRect.pivot = new Vector2(0, 0.5f);
                    checkIconRect.anchoredPosition = new Vector2(2f * sf, 0);
                    checkIconRect.sizeDelta = new Vector2(14f * sf, 14f * sf);

                    bgCheckboxImage = checkIconObj.AddComponent<Image>();
                    bgCheckboxImage.sprite = RecipesUIManager.CreateCheckboxSprite(section.drawingShowBg);
                    bgCheckboxImage.color = section.drawingShowBg
                        ? new Color(1f, 0.5f, 0.15f, 0.9f)
                        : new Color(0.6f, 0.6f, 0.6f, 0.7f);
                    bgCheckboxImage.raycastTarget = false;

                    GameObject bgLabelObj = new GameObject("Label");
                    bgLabelObj.transform.SetParent(bgCheckObj.transform, false);

                    RectTransform bgLabelRect = bgLabelObj.AddComponent<RectTransform>();
                    bgLabelRect.anchorMin = new Vector2(0, 0);
                    bgLabelRect.anchorMax = new Vector2(1, 1);
                    bgLabelRect.offsetMin = new Vector2(18f * sf, 0);
                    bgLabelRect.offsetMax = new Vector2(0, 0);

                    Text bgLabel = bgLabelObj.AddComponent<Text>();
                    bgLabel.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    bgLabel.text = "BG";
                    bgLabel.fontSize = scaledFontSize;
                    bgLabel.color = new Color(0.7f, 0.7f, 0.7f, 0.8f);
                    bgLabel.alignment = TextAnchor.MiddleLeft;
                    bgLabel.raycastTarget = false;
                }

                {
                    Image undoBg = CreateToolbarPillButton(
                        actionRow.transform, "UndoBtn", "Undo", Mathf.Max(36f, 46f * sf), rowHeight,
                        new Color(0.55f, 0.55f, 0.55f, 0.85f),
                        out undoButton);
                    undoButton.interactable = false;
                    undoBg.color = new Color(0.35f, 0.35f, 0.35f, 0.4f);
                    string capturedIdForUndo = section.id;
                    undoButton.onClick.AddListener(() =>
                    {
                        if (_sectionUIMap.TryGetValue(capturedIdForUndo, out var ui) && ui.SketchPad != null)
                            ui.SketchPad.Undo();
                    });
                }

                {
                    CreateToolbarPillButton(
                        actionRow.transform, "ClearBtn", "Clear", Mathf.Max(36f, 46f * sf), rowHeight,
                        new Color(0.55f, 0.55f, 0.55f, 0.85f),
                        out Button clearBtn);
                    string capturedIdForClear = section.id;
                    clearBtn.onClick.AddListener(() =>
                    {
                        if (_sectionUIMap.TryGetValue(capturedIdForClear, out var ui) && ui.SketchPad != null)
                            ui.SketchPad.ClearAll();
                    });
                }

                {
                    CreateToolbarPillButton(
                        cancelSaveParent, "CancelBtn", "Cancel", Mathf.Max(40f, 52f * sf), rowHeight,
                        new Color(0.6f, 0.25f, 0.2f, 0.85f),
                        out Button cancelBtn);
                    string capturedIdForCancel = section.id;
                    cancelBtn.onClick.AddListener(() =>
                    {
                        if (_sectionUIMap.TryGetValue(capturedIdForCancel, out var ui) && ui.SketchPad != null)
                        {
                            ui.SketchPad.Revert();
                            SaveAndDeactivateDrawing(capturedIdForCancel);
                        }
                    });
                }

                {
                    CreateToolbarPillButton(
                        cancelSaveParent, "SaveBtn", "Save", Mathf.Max(36f, 46f * sf), rowHeight,
                        new Color(0.3f, 0.6f, 0.3f, 0.85f),
                        out Button saveBtn);
                    string capturedIdForSave = section.id;
                    saveBtn.onClick.AddListener(() => SaveAndDeactivateDrawing(capturedIdForSave));
                }

                drawingToolbar.SetActive(false);

                int canvasWidth = 512;
                int canvasHeight = section.drawingHeight > 0 ? section.drawingHeight : 288;

                GameObject canvasWrapper = new GameObject("DrawingCanvasWrapper");
                canvasWrapper.transform.SetParent(textBoxContainer.transform, false);
                drawingCanvasWrapper = canvasWrapper;

                Image wrapperBg = canvasWrapper.AddComponent<Image>();
                wrapperBg.color = new Color(0, 0, 0, 0);
                wrapperBg.raycastTarget = true;

                DrawingHeightClamp clamp = canvasWrapper.AddComponent<DrawingHeightClamp>();
                clamp.maxScreenFraction = 0.3f;
                clamp.aspectRatio = (float)canvasWidth / canvasHeight;

                LayoutElement wrapperLE = canvasWrapper.AddComponent<LayoutElement>();
                wrapperLE.flexibleWidth = 1f;

                Button canvasClickBtn = canvasWrapper.AddComponent<Button>();
                canvasClickBtn.targetGraphic = wrapperBg;
                canvasClickBtn.transition = Selectable.Transition.None;
                string capturedIdForActivate = section.id;
                canvasClickBtn.onClick.AddListener(() => ActivateDrawing(capturedIdForActivate));

                GameObject drawingBgObj = new GameObject("DrawingBg");
                drawingBgObj.transform.SetParent(canvasWrapper.transform, false);

                RectTransform drawingBgRect = drawingBgObj.AddComponent<RectTransform>();
                drawingBgRect.anchorMin = new Vector2(0.5f, 0.5f);
                drawingBgRect.anchorMax = new Vector2(0.5f, 0.5f);
                drawingBgRect.pivot = new Vector2(0.5f, 0.5f);

                AspectRatioFitter drawingBgArf = drawingBgObj.AddComponent<AspectRatioFitter>();
                drawingBgArf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                drawingBgArf.aspectRatio = (float)canvasWidth / canvasHeight;

                drawingWrapperBg = drawingBgObj.AddComponent<Image>();
                drawingWrapperBg.color = section.drawingShowBg
                    ? new Color(0.12f, 0.12f, 0.12f, 1f)
                    : new Color(0, 0, 0, 0);
                drawingWrapperBg.raycastTarget = false;

                GameObject canvasObj = new GameObject("DrawingCanvas");
                canvasObj.transform.SetParent(canvasWrapper.transform, false);

                RectTransform canvasRect = canvasObj.AddComponent<RectTransform>();
                canvasRect.anchorMin = new Vector2(0.5f, 0.5f);
                canvasRect.anchorMax = new Vector2(0.5f, 0.5f);
                canvasRect.pivot = new Vector2(0.5f, 0.5f);

                AspectRatioFitter canvasArf = canvasObj.AddComponent<AspectRatioFitter>();
                canvasArf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                canvasArf.aspectRatio = (float)canvasWidth / canvasHeight;

                drawingCanvas = canvasObj.AddComponent<RawImage>();
                drawingCanvas.color = Color.white;
                drawingCanvas.raycastTarget = true;

                CreateDrawingBorderFrame(canvasObj.transform,
                    new Color(0.4f, 0.4f, 0.4f, 0.5f), 1f);

                drawingEditHint = new GameObject("EditHint");
                drawingEditHint.transform.SetParent(canvasWrapper.transform, false);

                RectTransform hintRect = drawingEditHint.AddComponent<RectTransform>();
                hintRect.anchorMin = new Vector2(0.5f, 0.5f);
                hintRect.anchorMax = new Vector2(0.5f, 0.5f);
                hintRect.pivot = new Vector2(0.5f, 0.5f);
                hintRect.anchoredPosition = Vector2.zero;
                hintRect.sizeDelta = new Vector2(170f, 28f);

                Image hintBg = drawingEditHint.AddComponent<Image>();
                hintBg.color = new Color(0.04f, 0.04f, 0.04f, 0.85f);
                hintBg.raycastTarget = false;

                GameObject hintTextObj = new GameObject("HintText");
                hintTextObj.transform.SetParent(drawingEditHint.transform, false);

                RectTransform hintTextRect = hintTextObj.AddComponent<RectTransform>();
                hintTextRect.anchorMin = Vector2.zero;
                hintTextRect.anchorMax = Vector2.one;
                hintTextRect.offsetMin = new Vector2(6, 0);
                hintTextRect.offsetMax = new Vector2(-6, 0);

                Text hintText = hintTextObj.AddComponent<Text>();
                hintText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                hintText.text = "click drawing to edit";
                hintText.fontSize = 13;
                hintText.fontStyle = FontStyle.Italic;
                hintText.color = new Color(0.8f, 0.8f, 0.8f, 0.9f);
                hintText.alignment = TextAnchor.MiddleCenter;
                hintText.raycastTarget = false;
                drawingEditHint.SetActive(false);

                sketchPad = canvasObj.AddComponent<RecipesSketchPad>();
                sketchPad.InputEnabled = false;

                if (!string.IsNullOrEmpty(section.drawingPngBase64))
                {
                    sketchPad.LoadFromPng(section.drawingPngBase64);
                }
                else
                {
                    sketchPad.Initialize(canvasWidth, canvasHeight);
                }

                Button capturedUndoBtn = undoButton;
                Image capturedUndoBg = undoButton.GetComponent<Image>();
                sketchPad.OnStrokeCountChanged += () =>
                {
                    if (capturedUndoBtn != null)
                    {
                        capturedUndoBtn.interactable = sketchPad.HasStrokes;
                        if (capturedUndoBg != null)
                        {
                            capturedUndoBg.color = sketchPad.HasStrokes
                                ? new Color(0.55f, 0.55f, 0.55f, 0.85f)
                                : new Color(0.35f, 0.35f, 0.35f, 0.4f);
                        }
                    }
                };
            }
            else
            {
                contentField = _uiManager.CreateInputField(textBoxContainer.transform, "ContentField", RecipesUIManager.ExpandedFontSize, true);
                contentField.text = section.content;
                _uiManager.SetPlaceholder(contentField, "<Enter Notes Here>");
                contentField.onValueChanged.AddListener((value) => {
                    OnSectionContentChanged(section.id, value);
                    contentField.textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
                    RebuildLayoutHierarchy(contentField.GetComponent<RectTransform>());
                });
                contentField.onValidateInput = BlockTab;

                displayObject = new GameObject("DisplayText");
                displayObject.transform.SetParent(textBoxContainer.transform, false);

                RectTransform displayRect = displayObject.AddComponent<RectTransform>();
                displayRect.anchorMin = new Vector2(0, 1);
                displayRect.anchorMax = new Vector2(1, 1);
                displayRect.pivot = new Vector2(0.5f, 1);
                displayRect.anchoredPosition = new Vector2(0, -5);
                displayRect.sizeDelta = new Vector2(-10, 800);

                displayText = displayObject.AddComponent<Text>();
                displayText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                displayText.fontSize = RecipesUIManager.ExpandedFontSize;
                displayText.color = Color.white;
                displayText.alignment = TextAnchor.UpperLeft;
                displayText.horizontalOverflow = HorizontalWrapMode.Wrap;
                displayText.verticalOverflow = VerticalWrapMode.Overflow;
                displayText.supportRichText = true;
                displayText.raycastTarget = true;

                ContentSizeFitter displayFitter = displayObject.AddComponent<ContentSizeFitter>();
                displayFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                EventTrigger trigger = displayObject.AddComponent<EventTrigger>();
                EventTrigger.Entry pointerClick = new EventTrigger.Entry();
                pointerClick.eventID = EventTriggerType.PointerClick;
                string capturedSectionId = section.id;
                pointerClick.callback.AddListener((data) => { OnTextClicked(capturedSectionId, (PointerEventData)data); });
                trigger.triggers.Add(pointerClick);

                displayObject.SetActive(false);
                contentField.gameObject.SetActive(true);
            }

            GameObject dragHandle = RecipesDragHandle.Create(
                sectionObj.transform,
                section.id,
                OnSectionDragStart,
                OnSectionDrag,
                OnSectionDragEnd,
                OnDragHandleDoubleClick
            );

            bool isPublic = section.isPublic;
            bool isOwnedByLocal = section.ownerId == GetLocalClientId();

            GameObject ownerTagObject = null;
            Text ownerTagText = null;
            Image badgeBackground = null;
            Image presenceDot = null;
            Image presenceGlow = null;
            Outline badgeOutline = null;
            Shadow badgeShadow = null;
            if (isPublic && !string.IsNullOrEmpty(section.ownerDisplayName))
            {
                int badgeFontSize = RecipesUIManager.PeekFontSize + _fontSizeOffset;
                int badgeHeight = badgeFontSize + 8;

                ownerTagObject = new GameObject("OwnerBadgeRow");
                ownerTagObject.transform.SetParent(textBoxContainer.transform, false);
                ownerTagObject.transform.SetAsFirstSibling();

                RectTransform rowRect = ownerTagObject.AddComponent<RectTransform>();
                rowRect.sizeDelta = new Vector2(0, badgeHeight);

                LayoutElement rowLE = ownerTagObject.AddComponent<LayoutElement>();
                rowLE.preferredHeight = badgeHeight;
                rowLE.minHeight = badgeHeight;

                GameObject badgePill = new GameObject("BadgePill");
                badgePill.transform.SetParent(ownerTagObject.transform, false);

                RectTransform pillRect = badgePill.AddComponent<RectTransform>();
                pillRect.anchorMin = new Vector2(0, 0);
                pillRect.anchorMax = new Vector2(0, 1);
                pillRect.pivot = new Vector2(0, 0.5f);
                pillRect.anchoredPosition = Vector2.zero;

                LayoutElement pillLE = badgePill.AddComponent<LayoutElement>();
                pillLE.ignoreLayout = true;

                badgeBackground = badgePill.AddComponent<Image>();
                badgeBackground.sprite = GetPillSprite();
                badgeBackground.type = Image.Type.Sliced;
                badgeBackground.raycastTarget = false;

                badgeOutline = badgePill.AddComponent<Outline>();
                badgeOutline.effectDistance = new Vector2(1f, -1f);
                badgeOutline.enabled = false;

                badgeShadow = badgePill.AddComponent<Shadow>();
                badgeShadow.effectColor = new Color(0, 0, 0, 0.5f);
                badgeShadow.effectDistance = new Vector2(2f, -2f);
                badgeShadow.enabled = false;

                Color accentColor = GetAccentColorForPlayer(section.ownerId);

                var badgeColors = GetBadgeColors(accentColor);
                Color bgColor = badgeColors.background;
                bgColor.a = 0f;
                badgeBackground.color = bgColor;
                badgeOutline.effectColor = badgeColors.border;

                float xCursor = 8f;

                {
                    int dotSize = Mathf.Max(8, badgeFontSize - 2);
                    float glowSize = dotSize * 2.2f;

                    GameObject dotObj = new GameObject("PresenceDot");
                    dotObj.transform.SetParent(ownerTagObject.transform, false);

                    RectTransform dotRect = dotObj.AddComponent<RectTransform>();
                    dotRect.anchorMin = new Vector2(0, 0.5f);
                    dotRect.anchorMax = new Vector2(0, 0.5f);
                    dotRect.pivot = new Vector2(0, 0.5f);
                    dotRect.anchoredPosition = new Vector2(xCursor, 0);
                    dotRect.sizeDelta = new Vector2(dotSize, dotSize);

                    LayoutElement dotLE = dotObj.AddComponent<LayoutElement>();
                    dotLE.ignoreLayout = true;

                    GameObject glowObj = new GameObject("Glow");
                    glowObj.transform.SetParent(dotObj.transform, false);

                    RectTransform glowRect = glowObj.AddComponent<RectTransform>();
                    glowRect.anchorMin = new Vector2(0.5f, 0.5f);
                    glowRect.anchorMax = new Vector2(0.5f, 0.5f);
                    glowRect.pivot = new Vector2(0.5f, 0.5f);
                    glowRect.anchoredPosition = Vector2.zero;
                    glowRect.sizeDelta = new Vector2(glowSize, glowSize);

                    presenceGlow = glowObj.AddComponent<Image>();
                    presenceGlow.sprite = GetGlowSprite();
                    presenceGlow.raycastTarget = false;

                    GameObject coreObj = new GameObject("Core");
                    coreObj.transform.SetParent(dotObj.transform, false);

                    RectTransform coreRect = coreObj.AddComponent<RectTransform>();
                    coreRect.anchorMin = new Vector2(0, 0);
                    coreRect.anchorMax = new Vector2(1, 1);
                    coreRect.offsetMin = Vector2.zero;
                    coreRect.offsetMax = Vector2.zero;

                    presenceDot = coreObj.AddComponent<Image>();
                    presenceDot.sprite = GetSolidDotSprite();
                    presenceDot.raycastTarget = false;

                    bool isOnline = isOwnedByLocal || (BeefsRecipesPlugin.Instance?.ClientSyncManager
                        ?.IsPlayerOnline(section.ownerId) ?? false);
                    ApplyPresenceDotState(presenceDot, presenceGlow, isOnline);

                    xCursor += dotSize + 4f;
                }

                string badgeLabel = isOwnedByLocal
                    ? "shared by you"
                    : $"shared by {section.ownerDisplayName}";

                GameObject badgeTextObj = new GameObject("BadgeText");
                badgeTextObj.transform.SetParent(badgePill.transform, false);

                ownerTagText = badgeTextObj.AddComponent<Text>();
                ownerTagText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                ownerTagText.fontSize = badgeFontSize;
                ownerTagText.alignment = TextAnchor.MiddleLeft;
                ownerTagText.fontStyle = FontStyle.Italic;
                ownerTagText.raycastTarget = false;
                ownerTagText.horizontalOverflow = HorizontalWrapMode.Overflow;
                ownerTagText.verticalOverflow = VerticalWrapMode.Overflow;
                ownerTagText.text = badgeLabel;
                ownerTagText.color = badgeColors.text;

                float textWidth = ownerTagText.preferredWidth;

                RectTransform textRect = badgeTextObj.GetComponent<RectTransform>();
                textRect.anchorMin = new Vector2(0, 0);
                textRect.anchorMax = new Vector2(0, 1);
                textRect.pivot = new Vector2(0, 0.5f);
                textRect.anchoredPosition = new Vector2(xCursor, 0);
                textRect.sizeDelta = new Vector2(textWidth + 2f, 0);

                xCursor += textWidth + 10f;
                pillRect.sizeDelta = new Vector2(xCursor, 0);
            }

            RecipeSectionUI sectionUI = new RecipeSectionUI
            {
                SectionObject = sectionObj,
                RectTransform = sectionObj.GetComponent<RectTransform>(),
                TitleField = titleField,
                TitleDisplayObject = titleDisplayObject,
                TitleDisplayText = titleDisplayText,
                ContentField = contentField,
                DisplayObject = displayObject,
                DisplayText = displayText,
                CollapsedPreviewObject = collapsedPreviewObject,
                CollapsedPreviewText = collapsedPreviewText,
                DragHandle = dragHandle,
                SectionId = section.id,
                TextBoxLayout = textBoxLayout,
                OwnerTagObject = ownerTagObject,
                OwnerTagText = ownerTagText,
                BadgeBackground = badgeBackground,
                PresenceDot = presenceDot,
                PresenceGlow = presenceGlow,
                BadgeOutline = badgeOutline,
                BadgeShadow = badgeShadow,
                IsPublic = isPublic,
                IsOwnedByLocal = isOwnedByLocal,
                IsDrawing = section.isDrawing,
                DrawingCanvasWrapper = drawingCanvasWrapper,
                DrawingWrapperBg = drawingWrapperBg,
                DrawingEditHint = drawingEditHint,
                DrawingCanvas = drawingCanvas,
                SketchPad = sketchPad,
                DrawingToolbar = drawingToolbar,
                UndoButton = undoButton,
                EraserButtonImage = eraserButtonImage,
                BrushColorSwatch = brushColorSwatch,
                BrushIconImage = brushIconImage,
                EraserIconImage = eraserIconImage,
                ToolModeLabel = toolModeLabel,
                BgCheckboxImage = bgCheckboxImage,
                TextBoxVerticalLayout = textBoxVerticalLayout,
            };

            bool canEdit = !isPublic || isOwnedByLocal;
            titleField.interactable = _panelManager.IsEditing && canEdit;
            if (contentField != null)
                contentField.interactable = _panelManager.IsEditing && canEdit;

            return sectionUI;
        }

        private void OnCollapsedSectionClicked(string sectionId)
        {
            var section = FindSection(sectionId);
            if (section != null && section.isCollapsed)
            {
                section.isCollapsed = false;
                CollapseSection(sectionId);
            }
        }

        private void HandleRightClick()
        {
            if (RecipesContextMenu.IsOpen)
            {
                RecipesContextMenu.Dismiss();
                return;
            }

            Vector2 mousePos = Input.mousePosition;

            if (IsMouseOverDivider(mousePos))
            {
                ShowDividerContextMenu(mousePos);
                return;
            }

            string sectionId = FindSectionAtScreenPosition(mousePos);
            if (sectionId == null) return;

            var section = FindSection(sectionId);
            if (section == null) return;
            if (!_sectionUIMap.TryGetValue(sectionId, out var sectionUI)) return;

            bool isEditing = _panelManager.IsEditing;
            bool isMultiplayer = BeefsRecipesPlugin.RuntimeContext.IsMultiplayer;
            bool isPublic = section.isPublic;
            bool isOwnedByLocal = section.ownerId == GetLocalClientId();
            bool isHostAdmin = BeefsRecipesPlugin.RuntimeContext.IsHostAdmin;
            var syncManager = BeefsRecipesPlugin.Instance?.ClientSyncManager;

            var items = new List<RecipesContextMenu.MenuItem>();

            items.Add(RecipesContextMenu.MenuItem.Action(
                section.isCollapsed ? "Expand" : "Collapse",
                () => { section.isCollapsed = !section.isCollapsed; CollapseSection(sectionId); }));

            if (!section.isDrawing)
            {
                items.Add(RecipesContextMenu.MenuItem.Action("Copy as markdown", () =>
                    CopySectionAsMarkdown(section)));
            }

            if (!isPublic)
            {
                if (isEditing)
                {
                    items.Add(RecipesContextMenu.MenuItem.Action("Delete section", () =>
                        DeleteSection(sectionId), tint: new Color(0.9f, 0.3f, 0.3f)));
                }
                if (isEditing)
                {
                    int insertAfter = _personalSections.FindIndex(s => s.id == sectionId);
                    int insertIdx = insertAfter >= 0 ? insertAfter + 1 : _personalSections.Count;
                    items.Add(RecipesContextMenu.MenuItem.Action("Add note section", () =>
                        InsertSectionAt(insertIdx), tint: new Color(0.2f, 0.8f, 0.2f)));
                    items.Add(RecipesContextMenu.MenuItem.Action("Add drawing section", () =>
                        InsertDrawingSectionAfter(sectionId), tint: new Color(0.3f, 0.5f, 0.9f)));
                    items.Add(RecipesContextMenu.MenuItem.Separator());
                    items.Add(RecipesContextMenu.MenuItem.Action("Title color...", () =>
                        ShowColorPicker(mousePos, sectionId, ColorTarget.Title)));
                    if (!section.isDrawing)
                    {
                        items.Add(RecipesContextMenu.MenuItem.Action("Content color...", () =>
                            ShowColorPicker(mousePos, sectionId, ColorTarget.Content)));
                    }
                }
                if (isMultiplayer && syncManager != null)
                {
                    items.Add(RecipesContextMenu.MenuItem.Separator());
                    items.Add(RecipesContextMenu.MenuItem.Action("Share to server", () =>
                    {
                        _personalSections.Remove(section);
                        syncManager.ShareSection(section);
                    }));
                }
            }
            else if (isOwnedByLocal)
            {
                if (isEditing)
                {
                    items.Add(RecipesContextMenu.MenuItem.Separator());
                    items.Add(RecipesContextMenu.MenuItem.Action("Title color...", () =>
                        ShowColorPicker(mousePos, sectionId, ColorTarget.Title)));
                    items.Add(RecipesContextMenu.MenuItem.Action("Content color...", () =>
                        ShowColorPicker(mousePos, sectionId, ColorTarget.Content)));
                }
                items.Add(RecipesContextMenu.MenuItem.Separator());
                bool hasOverride = !string.IsNullOrEmpty(syncManager?.GetAccentColorOverride());
                items.Add(RecipesContextMenu.MenuItem.Action(
                    hasOverride ? "Change badge color..." : "Badge color (suit)...",
                    () => ShowAccentColorPicker(mousePos)));
                if (hasOverride)
                {
                    items.Add(RecipesContextMenu.MenuItem.Action("Reset badge to suit color", () =>
                    {
                        syncManager.SetAccentColorOverride(null);
                        RefreshAccentColors();
                    }));
                }
                if (syncManager != null)
                {
                    items.Add(RecipesContextMenu.MenuItem.Action("Unshare", () =>
                    {
                        var preSection = _publicSections.Find(s => s.id == sectionId);
                        if (preSection != null && !_personalSections.Exists(s => s.id == sectionId))
                            _personalSections.Add(preSection);
                        syncManager.UnshareSection(sectionId);
                    }));
                }
            }
            else
            {
                items.Add(RecipesContextMenu.MenuItem.Separator());
                bool isHidden = _hiddenSectionIds.Contains(sectionId);
                items.Add(RecipesContextMenu.MenuItem.Action(
                    isHidden ? "Unhide this note" : "Hide this note",
                    () =>
                    {
                        if (isHidden)
                        { _hiddenSectionIds.Remove(sectionId); syncManager?.UnhideSection(sectionId); }
                        else
                        { _hiddenSectionIds.Add(sectionId); syncManager?.HideSection(sectionId); }
                        RebuildUI();
                    }));
                if (!isHostAdmin && syncManager != null)
                {
                    bool hasVoted = syncManager.HasVotedOn(sectionId);
                    items.Add(RecipesContextMenu.MenuItem.Action(
                        hasVoted ? "Retract removal vote" : "Vote to remove",
                        () =>
                        {
                            if (hasVoted) syncManager.RedactVote(sectionId);
                            else syncManager.VoteToRemove(sectionId);
                        }));
                }
                if (isHostAdmin && syncManager != null)
                {
                    items.Add(RecipesContextMenu.MenuItem.Separator());
                    items.Add(RecipesContextMenu.MenuItem.Action("Delete (admin)", () =>
                        syncManager.DeletePublicSection(sectionId)));

                    string ownerName = section.ownerDisplayName ?? section.ownerId.ToString();
                    ulong ownerId = section.ownerId;

                    items.Add(RecipesContextMenu.MenuItem.Action($"Kick player ({ownerName})", () =>
                    {
                        ShowConfirmMenu(mousePos, $"Kick {ownerName}?", () =>
                        {
                            BeefsRecipesPlugin.Instance?.ServerNoteManager?
                                .KickPlayer(ownerId, "Kicked by admin");
                        });
                    }));

                    items.Add(RecipesContextMenu.MenuItem.Action($"Ban player ({ownerName})", () =>
                    {
                        ShowConfirmMenu(mousePos, $"Ban {ownerName}? This is permanent.", () =>
                        {
                            BeefsRecipesPlugin.Instance?.ServerNoteManager?
                                .BanPlayer(ownerId, ownerName, "Banned by admin",
                                    Assets.Scripts.Networking.NetworkManager.LocalClientId);
                        });
                    }));
                }
            }

            items.Add(RecipesContextMenu.MenuItem.Separator());
            items.Add(RecipesContextMenu.MenuItem.Action("Copy all notes", () =>
                CopyAllNotesAsMarkdown()));
            items.Add(RecipesContextMenu.MenuItem.Action(
                _panelManager.IsFullscreen ? "Exit fullscreen" : "Fullscreen",
                () => _panelManager.ToggleFullscreen()));
            items.Add(RecipesContextMenu.MenuItem.Action("Settings...", () =>
                _panelManager.OpenSettings()));

            RecipesContextMenu.Show(mousePos, items);
        }

        private void ShowConfirmMenu(Vector2 mousePos, string prompt, Action onConfirm)
        {
            var items = new List<RecipesContextMenu.MenuItem>();
            items.Add(RecipesContextMenu.MenuItem.Action($"\u26a0 {prompt}", null, false));
            items.Add(RecipesContextMenu.MenuItem.Separator());
            items.Add(RecipesContextMenu.MenuItem.Action("Confirm", onConfirm));
            items.Add(RecipesContextMenu.MenuItem.Action("Cancel", () => { }));
            RecipesContextMenu.Show(mousePos, items);
        }

        private void ShowDividerContextMenu(Vector2 mousePos)
        {
            var syncManager = BeefsRecipesPlugin.Instance?.ClientSyncManager;
            var items = new List<RecipesContextMenu.MenuItem>();
            ulong localId = GetLocalClientId();

            int hiddenCount = 0;
            int unhidableCount = 0;
            foreach (var section in _publicSections)
            {
                if (_hiddenSectionIds.Contains(section.id))
                    hiddenCount++;
                else if (section.ownerId != localId)
                    unhidableCount++;
            }

            if (unhidableCount > 0)
            {
                items.Add(RecipesContextMenu.MenuItem.Action("Hide all shared notes", () =>
                {
                    foreach (var section in _publicSections)
                    {
                        if (section.ownerId != localId && !_hiddenSectionIds.Contains(section.id))
                        {
                            _hiddenSectionIds.Add(section.id);
                            syncManager?.HideSection(section.id);
                        }
                    }
                    RebuildUI();
                }));
            }

            if (hiddenCount > 0)
            {
                items.Add(RecipesContextMenu.MenuItem.Action($"Unhide all ({hiddenCount})", () =>
                {
                    foreach (string id in new List<string>(_hiddenSectionIds))
                    {
                        _hiddenSectionIds.Remove(id);
                        syncManager?.UnhideSection(id);
                    }
                    RebuildUI();
                }));
            }

            items.Add(RecipesContextMenu.MenuItem.Separator());
            items.Add(RecipesContextMenu.MenuItem.Action("Settings...", () =>
                _panelManager.OpenSettings()));

            RecipesContextMenu.Show(mousePos, items);
        }

        private bool IsMouseOverDivider(Vector2 screenPosition)
        {
            if (_dividerObject == null || !_dividerObject.activeSelf) return false;
            if (EventSystem.current == null) return false;

            var pointerData = new PointerEventData(EventSystem.current) { position = screenPosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (var result in results)
            {
                Transform current = result.gameObject.transform;
                while (current != null)
                {
                    if (current.gameObject == _dividerObject) return true;
                    current = current.parent;
                }
            }
            return false;
        }

        public string FindSectionAtScreenPosition(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return null;

            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            foreach (var result in results)
            {
                Transform current = result.gameObject.transform;
                while (current != null)
                {
                    foreach (var kvp in _sectionUIMap)
                    {
                        if (kvp.Value.SectionObject == current.gameObject)
                            return kvp.Key;
                    }
                    current = current.parent;
                }
            }

            return null;
        }

        private void ApplyState()
        {
            _stateDirty = false;
            bool isPeekMode = _panelManager.IsPeekMode();
            bool showButtons = _panelManager.IsExpandedMode();
            bool showDragHandles = showButtons;
            bool isNarrow = _panelManager.IsTransitioning();

            foreach (var kvp in _sectionUIMap)
            {
                var sectionData = FindSection(kvp.Value.SectionId);
                bool isPublicSection = sectionData?.isPublic ?? kvp.Value.IsPublic;
                bool isOwnedByLocal = sectionData != null
                    ? sectionData.ownerId == GetLocalClientId()
                    : kvp.Value.IsOwnedByLocal;
                bool canEdit = !isPublicSection || isOwnedByLocal;
                bool isHostAdmin = BeefsRecipesPlugin.RuntimeContext.IsHostAdmin;

                if (kvp.Value.DragHandle != null)
                {
                    kvp.Value.DragHandle.SetActive(showDragHandles && canEdit);

                    var handleRect = kvp.Value.DragHandle.GetComponent<RectTransform>();
                    if (handleRect != null)
                    {
                        float w = BeefsRecipesPlugin.DragBarWidth.Value;
                        handleRect.anchoredPosition = new Vector2(-w, 0);
                        handleRect.sizeDelta = new Vector2(w, 0);
                    }
                }

                if (kvp.Value.OwnerTagObject != null)
                {
                    kvp.Value.OwnerTagObject.SetActive(isPublicSection);
                    if (kvp.Value.OwnerTagText != null)
                    {
                        int badgeFontSize = RecipesUIManager.PeekFontSize + _fontSizeOffset;
                        int badgeHeight = badgeFontSize + 8;
                        kvp.Value.OwnerTagText.fontSize = badgeFontSize;

                        var wrapperLE = kvp.Value.OwnerTagObject.GetComponent<LayoutElement>();
                        if (wrapperLE != null)
                        {
                            wrapperLE.preferredHeight = badgeHeight;
                            wrapperLE.minHeight = badgeHeight;
                        }
                        var wrapperRt = kvp.Value.OwnerTagObject.GetComponent<RectTransform>();
                        if (wrapperRt != null)
                            wrapperRt.sizeDelta = new Vector2(wrapperRt.sizeDelta.x, badgeHeight);

                        if (kvp.Value.BadgeBackground != null)
                        {
                            Color bgColor = kvp.Value.BadgeBackground.color;
                            bgColor.a = showButtons ? 0.9f : 0f;
                            kvp.Value.BadgeBackground.color = bgColor;
                        }

                        if (kvp.Value.BadgeOutline != null)
                            kvp.Value.BadgeOutline.enabled = showButtons;
                        if (kvp.Value.BadgeShadow != null)
                            kvp.Value.BadgeShadow.enabled = showButtons;

                        float xCursor = 8f;
                        if (kvp.Value.PresenceDot != null)
                        {
                            int dotSize = Mathf.Max(8, badgeFontSize - 2);
                            float glowSize = dotSize * 2.2f;

                            var containerRt = kvp.Value.PresenceDot.transform.parent
                                ?.GetComponent<RectTransform>();
                            if (containerRt != null)
                            {
                                containerRt.anchoredPosition = new Vector2(xCursor, 0);
                                containerRt.sizeDelta = new Vector2(dotSize, dotSize);
                            }

                            if (kvp.Value.PresenceGlow != null)
                            {
                                var glowRt = kvp.Value.PresenceGlow.GetComponent<RectTransform>();
                                if (glowRt != null)
                                    glowRt.sizeDelta = new Vector2(glowSize, glowSize);
                            }

                            xCursor += dotSize + 4f;
                        }

                        float textWidth = kvp.Value.OwnerTagText.preferredWidth;
                        var textRt = kvp.Value.OwnerTagText.GetComponent<RectTransform>();
                        if (textRt != null)
                        {
                            textRt.anchoredPosition = new Vector2(xCursor, 0);
                            textRt.sizeDelta = new Vector2(textWidth + 2f, 0);
                        }
                        xCursor += textWidth + 10f;

                        if (kvp.Value.BadgeBackground != null)
                        {
                            var pillRt = kvp.Value.BadgeBackground.GetComponent<RectTransform>();
                            if (pillRt != null)
                                pillRt.sizeDelta = new Vector2(xCursor, 0);
                        }
                    }
                }

                kvp.Value.TextBoxLayout.flexibleWidth = 1f;

                var section = FindSection(kvp.Value.SectionId);
                if (section != null)
                {
                    if (section.isCollapsed)
                    {
                        continue;
                    }

                    if (kvp.Value.IsDrawing)
                    {
                        kvp.Value.TitleField.gameObject.SetActive(!isPeekMode);
                        kvp.Value.TitleDisplayObject.SetActive(isPeekMode);

                        if (isPeekMode)
                        {
                            kvp.Value.TitleDisplayText.fontSize = RecipesUIManager.TitleFontSize + _fontSizeOffset;
                            if (!string.IsNullOrWhiteSpace(section.title))
                            {
                                kvp.Value.TitleDisplayText.supportRichText = true;
                                kvp.Value.TitleDisplayText.text = "<b>" + section.title + "</b>";
                            }
                            else
                            {
                                kvp.Value.TitleDisplayText.text = "";
                            }
                        }
                        else
                        {
                            kvp.Value.TitleField.textComponent.fontSize = RecipesUIManager.TitleFontSize + _fontSizeOffset;
                            if (kvp.Value.TitleField.text != section.title)
                                kvp.Value.TitleField.text = section.title;
                        }

                        bool isActiveDrawing = _activeDrawingSectionId == kvp.Value.SectionId;
                        bool toolbarVisible = showButtons && canEdit && isActiveDrawing;
                        if (kvp.Value.DrawingToolbar != null)
                            kvp.Value.DrawingToolbar.SetActive(toolbarVisible);

                        if (kvp.Value.TextBoxVerticalLayout != null)
                            kvp.Value.TextBoxVerticalLayout.spacing = toolbarVisible ? 1f : 4f;

                        if (kvp.Value.DrawingCanvasWrapper != null)
                            kvp.Value.DrawingCanvasWrapper.SetActive(true);

                        if (kvp.Value.DrawingWrapperBg != null)
                        {
                            var sec = FindSection(kvp.Value.SectionId);
                            if (sec != null)
                            {
                                kvp.Value.DrawingWrapperBg.color = sec.drawingShowBg
                                    ? new Color(0.12f, 0.12f, 0.12f, 1f)
                                    : new Color(0, 0, 0, 0);
                            }
                        }

                        if (kvp.Value.DrawingEditHint != null)
                            kvp.Value.DrawingEditHint.SetActive(showButtons && canEdit && !isActiveDrawing);

                        if (kvp.Value.SketchPad != null)
                            kvp.Value.SketchPad.InputEnabled = showButtons && canEdit && isActiveDrawing;
                    }
                    else if (isPeekMode)
                    {
                        kvp.Value.TitleField.gameObject.SetActive(false);
                        kvp.Value.TitleDisplayObject.SetActive(true);
                        if (kvp.Value.ContentField != null) kvp.Value.ContentField.gameObject.SetActive(false);
                        if (kvp.Value.DisplayObject != null) kvp.Value.DisplayObject.SetActive(true);

                        kvp.Value.TitleDisplayText.fontSize = RecipesUIManager.TitleFontSize + _fontSizeOffset;
                        if (kvp.Value.DisplayText != null)
                            kvp.Value.DisplayText.fontSize = RecipesUIManager.ExpandedFontSize + _fontSizeOffset;

                        if (!string.IsNullOrWhiteSpace(section.title))
                        {
                            kvp.Value.TitleDisplayText.supportRichText = true;
                            kvp.Value.TitleDisplayText.text = "<b>" + section.title + "</b>";
                        }
                        else
                        {
                            kvp.Value.TitleDisplayText.text = "";
                        }

                        if (kvp.Value.DisplayText != null)
                        {
                            if (!string.IsNullOrWhiteSpace(section.content))
                            {
                                List<NotesMarkdownConverter.CheckboxInfo> checkboxes;
                                string richText = NotesMarkdownConverter.MarkdownToUGUI(section.content, out checkboxes, _fontSizeOffset);
                                kvp.Value.DisplayText.text = richText;
                                _sectionCheckboxes[kvp.Value.SectionId] = checkboxes;
                            }
                            else
                            {
                                kvp.Value.DisplayText.text = "";
                            }
                        }
                    }
                    else
                    {
                        kvp.Value.TitleField.gameObject.SetActive(true);
                        kvp.Value.TitleDisplayObject.SetActive(false);
                        if (kvp.Value.ContentField != null) kvp.Value.ContentField.gameObject.SetActive(true);
                        if (kvp.Value.DisplayObject != null) kvp.Value.DisplayObject.SetActive(false);

                        kvp.Value.TitleField.textComponent.fontSize = RecipesUIManager.TitleFontSize + _fontSizeOffset;
                        if (kvp.Value.ContentField?.textComponent != null)
                            kvp.Value.ContentField.textComponent.fontSize = RecipesUIManager.ExpandedFontSize + _fontSizeOffset;

                        if (kvp.Value.TitleField.text != section.title)
                        {
                            kvp.Value.TitleField.text = section.title;
                        }
                        if (kvp.Value.ContentField != null && kvp.Value.ContentField.text != section.content)
                        {
                            kvp.Value.ContentField.text = section.content;
                        }
                    }
                }

                bool showText = !isNarrow;
                if (kvp.Value.TitleField.textComponent != null)
                {
                    kvp.Value.TitleField.textComponent.enabled = showText;
                }
                if (kvp.Value.ContentField?.textComponent != null)
                {
                    kvp.Value.ContentField.textComponent.enabled = showText;
                }

                if (kvp.Value.TitleField.placeholder != null)
                {
                    kvp.Value.TitleField.placeholder.gameObject.SetActive(!isPeekMode && showText);
                    Text placeholderText = kvp.Value.TitleField.placeholder.GetComponent<Text>();
                    if (placeholderText != null)
                    {
                        placeholderText.fontSize = RecipesUIManager.TitleFontSize + _fontSizeOffset;
                    }
                }
                if (kvp.Value.ContentField?.placeholder != null)
                {
                    kvp.Value.ContentField.placeholder.gameObject.SetActive(!isPeekMode && showText);
                    Text placeholderText = kvp.Value.ContentField.placeholder.GetComponent<Text>();
                    if (placeholderText != null)
                    {
                        placeholderText.fontSize = RecipesUIManager.ExpandedFontSize + _fontSizeOffset;
                    }
                }

                bool hasContent = !string.IsNullOrWhiteSpace(kvp.Value.TitleField.text) ||
                                  (kvp.Value.ContentField != null && !string.IsNullOrWhiteSpace(kvp.Value.ContentField.text)) ||
                                  kvp.Value.IsDrawing;
                kvp.Value.SectionObject.SetActive(!isPeekMode || hasContent);
            }

            if (_dividerObject != null)
            {
                _dividerObject.SetActive(_publicSections.Count > 0 || BeefsRecipesPlugin.RuntimeContext.IsMultiplayer);

                if (_dividerLabelText != null)
                    _dividerLabelText.fontSize = RecipesUIManager.PeekFontSize + _fontSizeOffset;
                if (_showHiddenButtonText != null)
                    _showHiddenButtonText.fontSize = Mathf.Max(10, RecipesUIManager.PeekFontSize + _fontSizeOffset - 2);
                if (_dividerHintText != null)
                    _dividerHintText.fontSize = Mathf.Max(10, RecipesUIManager.PeekFontSize + _fontSizeOffset - 2);
            }

            foreach (var gap in _gapObjects)
            {
                if (gap != null) gap.SetActive(showButtons);
            }

            Canvas.ForceUpdateCanvases();
        }

        private void OnSectionTitleChanged(string sectionId, string newTitle)
        {
            var section = FindSection(sectionId);
            if (section != null)
            {
                section.title = newTitle;

                if (section.isPublic)
                {
                    _modifiedPublicSectionIds.Add(sectionId);
                }

                if (_sectionUIMap.TryGetValue(sectionId, out var sectionUI))
                {
                    bool isPeekMode = _panelManager.IsPeekMode();
                    bool hasContent = !string.IsNullOrWhiteSpace(newTitle) ||
                                      sectionUI.IsDrawing ||
                                      (sectionUI.ContentField != null && !string.IsNullOrWhiteSpace(sectionUI.ContentField.text));
                    sectionUI.SectionObject.SetActive(!isPeekMode || hasContent);
                }
            }
        }

        private void OnSectionContentChanged(string sectionId, string newContent)
        {
            var section = FindSection(sectionId);
            if (section != null)
            {
                section.content = newContent;

                if (section.isPublic)
                {
                    _modifiedPublicSectionIds.Add(sectionId);
                }

                if (_sectionUIMap.TryGetValue(sectionId, out var sectionUI))
                {
                    bool isPeekMode = _panelManager.IsPeekMode();
                    bool hasContent = !string.IsNullOrWhiteSpace(sectionUI.TitleField.text) ||
                                      !string.IsNullOrWhiteSpace(newContent);
                    sectionUI.SectionObject.SetActive(!isPeekMode || hasContent);
                }
            }
        }

        private void DeleteSection(string sectionId)
        {
            var section = FindSection(sectionId);
            if (section == null) return;

            if (section.isPublic)
            {
                var syncManager = BeefsRecipesPlugin.Instance?.ClientSyncManager;
                if (syncManager == null) return;

                ulong localId = GetLocalClientId();
                if (section.ownerId == localId)
                {
                    if (!_personalSections.Exists(s => s.id == sectionId))
                        _personalSections.Add(section);
                    syncManager.UnshareSection(sectionId);
                }
                else if (BeefsRecipesPlugin.RuntimeContext.IsHostAdmin)
                {
                    syncManager.DeletePublicSection(sectionId);
                    RebuildUI();
                }
            }
            else
            {
                _personalSections.RemoveAll(s => s.id == sectionId);
                RebuildUI();
            }
        }

        private void CopySectionAsMarkdown(BeefsRecipesPlugin.RecipeSection section)
        {
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrWhiteSpace(section.title))
            {
                sb.AppendLine($"# {section.title}");
                sb.AppendLine();
            }
            if (!string.IsNullOrWhiteSpace(section.content))
            {
                sb.Append(section.content);
            }
            GUIUtility.systemCopyBuffer = sb.ToString().TrimEnd();
        }

        private void CopyAllNotesAsMarkdown()
        {
            var sb = new System.Text.StringBuilder();
            bool first = true;
            foreach (var section in _personalSections)
            {
                if (string.IsNullOrWhiteSpace(section.title) && string.IsNullOrWhiteSpace(section.content))
                    continue;

                if (!first)
                {
                    sb.AppendLine();
                    sb.AppendLine();
                }
                first = false;

                if (!string.IsNullOrWhiteSpace(section.title))
                {
                    sb.AppendLine($"# {section.title}");
                    sb.AppendLine();
                }
                if (!string.IsNullOrWhiteSpace(section.content))
                {
                    sb.Append(section.content);
                }
            }
            GUIUtility.systemCopyBuffer = sb.ToString().TrimEnd();
        }

        private void HandleTabNavigation(bool forward = true)
        {
            if (_allInputFields.Count == 0) return;

            int nextIndex;
            if (_currentFocusedIndex == -1)
            {
                nextIndex = 0;
            }
            else if (forward)
            {
                nextIndex = (_currentFocusedIndex + 1) % _allInputFields.Count;
            }
            else
            {
                nextIndex = (_currentFocusedIndex - 1 + _allInputFields.Count) % _allInputFields.Count;
            }

            _currentFocusedIndex = nextIndex;

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            InputField targetField = _allInputFields[nextIndex];
            if (targetField != null)
            {
                targetField.ActivateInputField();
                targetField.MoveTextEnd(false);

                if (BeefsRecipesPlugin.Instance != null)
                {
                    BeefsRecipesPlugin.Instance.StartCoroutine(DeselectText(targetField));
                }
            }
        }

        private System.Collections.IEnumerator DeselectText(InputField field)
        {
            yield return new WaitForEndOfFrame();

            if (field != null && field.isFocused)
            {
                field.MoveTextEnd(false);

                int textLength = field.text.Length;
                field.caretPosition = textLength;
                field.selectionAnchorPosition = textLength;
                field.selectionFocusPosition = textLength;

                field.ForceLabelUpdate();
            }
        }

        private char BlockTab(string text, int charIndex, char addedChar)
        {
            if (addedChar == '\t')
            {
                return '\0';
            }
            return addedChar;
        }

        private char BlockTabAndNewline(string text, int charIndex, char addedChar)
        {
            if (addedChar == '\t' || addedChar == '\n' || addedChar == '\r')
            {
                return '\0';
            }
            return addedChar;
        }

        private void RebuildLayoutHierarchy(RectTransform startTransform)
        {
            if (startTransform == null) return;

            RectTransform current = startTransform;
            while (current != null && current.gameObject != _uiManager.ContentObject)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(current);
                current = current.parent as RectTransform;
            }

            if (current != null && current.gameObject == _uiManager.ContentObject)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(current);
            }
        }

        public void UpdateSectionInputs()
        {
            bool isEditing = _panelManager.IsEditing;

            foreach (var kvp in _sectionUIMap)
            {
                bool canEdit = !kvp.Value.IsPublic || kvp.Value.IsOwnedByLocal;
                kvp.Value.TitleField.interactable = isEditing && canEdit;
                if (kvp.Value.ContentField != null)
                    kvp.Value.ContentField.interactable = isEditing && canEdit;
            }

            if (isEditing && _personalSections.Count > 0 && _sectionUIMap.Count > 0)
            {
                var firstSection = _personalSections[0];
                if (_sectionUIMap.TryGetValue(firstSection.id, out var sectionUI))
                {
                    if (sectionUI.ContentField != null)
                    {
                        sectionUI.ContentField.ActivateInputField();
                        _currentFocusedIndex = 1;
                    }
                    else
                    {
                        sectionUI.TitleField.ActivateInputField();
                        _currentFocusedIndex = 0;
                    }
                }
            }
        }

        public void OnStateChanged()
        {
            ApplyState();
        }

        public void OnFullscreenChanged()
        {
            RebuildUI();
            _uiManager.ScrollPosition = 1f;
        }

        public List<(string id, string label)> GetHiddenSectionInfo()
        {
            var result = new List<(string, string)>();
            foreach (var section in _publicSections)
            {
                if (!_hiddenSectionIds.Contains(section.id)) continue;

                string label = !string.IsNullOrWhiteSpace(section.title)
                    ? section.title
                    : (!string.IsNullOrWhiteSpace(section.content)
                        ? (section.content.Length > 20 ? section.content.Substring(0, 20) + "..." : section.content)
                        : "(empty note)");

                if (!string.IsNullOrEmpty(section.ownerDisplayName))
                    label += $" - {section.ownerDisplayName}";

                result.Add((section.id, label));
            }
            return result;
        }

        public void UnhideSectionById(string sectionId)
        {
            if (_hiddenSectionIds.Remove(sectionId))
            {
                BeefsRecipesPlugin.Instance?.ClientSyncManager?.UnhideSection(sectionId);
                RebuildUI();
            }
        }

        public int FontSizeOffset
        {
            get => _fontSizeOffset;
            set
            {
                _fontSizeOffset = Mathf.Clamp(value, MinFontOffset, MaxFontOffset);
                _stateDirty = true;
            }
        }

        public void MarkStateDirty()
        {
            _stateDirty = true;
        }

        public void OnEditModeExited()
        {
            SaveAndDeactivateDrawing(_activeDrawingSectionId);

            foreach (var kvp in _sectionUIMap)
            {
                if (kvp.Value.IsDrawing && kvp.Value.SketchPad != null && kvp.Value.SketchPad.IsDirty)
                {
                    var section = FindSection(kvp.Value.SectionId);
                    if (section != null)
                    {
                        section.drawingPngBase64 = kvp.Value.SketchPad.SaveToPng();
                        BeefsRecipesPlugin.Log.LogInfo(
                            $"Rasterized drawing for section {section.id.Substring(0, 8)}...");

                        if (section.isPublic)
                            _modifiedPublicSectionIds.Add(section.id);
                    }
                }
            }

            FlushModifiedPublicSections(true);

            if (HasSessionKey)
            {
                SavePersonalNotes();
            }
        }

        private void FlushModifiedPublicSections(bool finalSave)
        {
            if (!finalSave)
            {
                foreach (var kvp in _sectionUIMap)
                {
                    if (kvp.Value.IsDrawing && kvp.Value.IsPublic && kvp.Value.IsOwnedByLocal
                        && kvp.Value.SketchPad != null && kvp.Value.SketchPad.IsDirty)
                    {
                        var section = FindSection(kvp.Value.SectionId);
                        if (section != null)
                        {
                            section.drawingPngBase64 = kvp.Value.SketchPad.SnapshotToPng();
                            _modifiedPublicSectionIds.Add(section.id);
                        }
                    }
                }
            }

            if (_modifiedPublicSectionIds.Count == 0) return;
            if (BeefsRecipesPlugin.Instance?.ClientSyncManager == null) return;

            var modifiedSections = new List<BeefsRecipesPlugin.RecipeSection>();
            foreach (string id in _modifiedPublicSectionIds)
            {
                var section = FindSection(id);
                if (section != null && section.isPublic)
                {
                    modifiedSections.Add(section);
                }
            }

            if (modifiedSections.Count > 0)
            {
                BeefsRecipesPlugin.Instance.ClientSyncManager
                    .PushPublicSectionUpdates(modifiedSections);
            }

            _modifiedPublicSectionIds.Clear();
            _lastPublicPushTime = Time.unscaledTime;
        }

        private void OnShowHiddenToggled()
        {
            _showHiddenNotes = !_showHiddenNotes;
            if (_showHiddenButtonText != null)
            {
                _showHiddenButtonText.text = _showHiddenNotes ? "Hide Hidden" : "Show Hidden";
            }
            RebuildUI();
        }

        private static ulong GetLocalClientId()
        {
            try
            {
                return Assets.Scripts.Networking.NetworkManager.LocalClientId;
            }
            catch
            {
                return 0;
            }
        }

        private static int GetLogicalLineFromLocalPoint(Text text, Vector2 localPoint)
        {
            TextGenerator gen = text.cachedTextGenerator;
            if (gen == null || gen.lineCount == 0)
                return -1;

            var lines = gen.lines;

            int visualLine = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                float lineTop = lines[i].topY;
                float lineBottom = lineTop - lines[i].height;

                if (localPoint.y <= lineTop && localPoint.y > lineBottom)
                {
                    visualLine = i;
                    break;
                }
            }

            if (visualLine < 0) return -1;

            int targetVisibleIdx = lines[visualLine].startCharIdx;
            string richText = text.text;
            int logicalLine = 0;
            int visibleCount = 0;
            bool inTag = false;

            for (int i = 0; i < richText.Length && visibleCount < targetVisibleIdx; i++)
            {
                char c = richText[i];
                if (c == '<') { inTag = true; continue; }
                if (inTag) { if (c == '>') inTag = false; continue; }
                if (c == '\n') logicalLine++;
                visibleCount++;
            }

            return logicalLine;
        }

        public bool IsCheckboxHere(GameObject displayTextObject, Vector2 screenPosition)
        {
            foreach (var kvp in _sectionUIMap)
            {
                if (kvp.Value.DisplayText != null && kvp.Value.DisplayText.gameObject == displayTextObject)
                {
                    if (!_sectionCheckboxes.ContainsKey(kvp.Value.SectionId))
                        return false;

                    var section = FindSection(kvp.Value.SectionId);
                    if (section != null && section.isPublic && section.ownerId != GetLocalClientId())
                        return false;

                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        kvp.Value.DisplayText.rectTransform,
                        screenPosition,
                        null,
                        out Vector2 localPoint);

                    int lineClicked = GetLogicalLineFromLocalPoint(kvp.Value.DisplayText, localPoint);

                    var checkbox = _sectionCheckboxes[kvp.Value.SectionId].Find(cb => cb.LineNumber == lineClicked);
                    return checkbox != null;
                }
            }
            return false;
        }

        private void OnSectionDragStart(string sectionId, Vector2 position)
        {
            _draggedSectionId = sectionId;

            if (_sectionUIMap.TryGetValue(sectionId, out var sectionUI))
            {
                var canvasGroup = sectionUI.SectionObject.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                    canvasGroup = sectionUI.SectionObject.AddComponent<CanvasGroup>();
                canvasGroup.alpha = 0.5f;
            }

            _dragPlaceholder = new GameObject("DragPlaceholder");
            _dragPlaceholder.transform.SetParent(_uiManager.ContentObject.transform, false);

            RectTransform placeholderRect = _dragPlaceholder.AddComponent<RectTransform>();
            placeholderRect.anchorMin = new Vector2(0, 1);
            placeholderRect.anchorMax = new Vector2(1, 1);
            placeholderRect.pivot = new Vector2(0.5f, 1);
            placeholderRect.sizeDelta = new Vector2(0, 3);

            LayoutElement layoutIgnore = _dragPlaceholder.AddComponent<LayoutElement>();
            layoutIgnore.ignoreLayout = true;

            Image placeholderImage = _dragPlaceholder.AddComponent<Image>();
            placeholderImage.color = new Color(0.3f, 0.6f, 1f, 0.8f);
            placeholderImage.raycastTarget = false;

            var sec = FindSection(sectionId);
            bool canShare = sec != null && !sec.isPublic &&
                (_dividerObject != null || _uiManager.IsFullscreenLayout);
            if (canShare)
            {
                Transform indicatorParent = _uiManager.IsFullscreenLayout
                    ? _uiManager.RightColumnRoot.transform
                    : _uiManager.ContentObject.transform;

                _dragShareIndicator = CreateDashedFrame(
                    indicatorParent,
                    new Color(1f, 0.6f, 0.08f, 0.7f), 2f, 10f, 6f);
                _dragShareIndicator.SetActive(false);
                _lastDashSize = Vector2.zero;
            }
        }

        private void OnSectionDrag(string sectionId, Vector2 screenPosition)
        {
            bool belowDivider = IsDropBelowDivider(screenPosition);
            var section = FindSection(sectionId);
            bool isPublic = section != null && section.isPublic;

            if (belowDivider && _dragShareIndicator != null && !isPublic)
            {
                if (_dragPlaceholder != null)
                    _dragPlaceholder.SetActive(false);

                UpdateShareIndicatorPosition();
                _dragShareIndicator.SetActive(true);
                _dragTargetIndex = -1;
            }
            else if (belowDivider && isPublic)
            {
                if (_dragPlaceholder != null)
                    _dragPlaceholder.SetActive(false);
                if (_dragShareIndicator != null)
                    _dragShareIndicator.SetActive(false);
                _dragTargetIndex = -1;
            }
            else
            {
                if (_dragShareIndicator != null)
                    _dragShareIndicator.SetActive(false);

                if (_dragPlaceholder != null)
                    _dragPlaceholder.SetActive(true);

                int targetIndex = CalculateDropIndex(screenPosition);

                if (targetIndex != _dragTargetIndex)
                {
                    _dragTargetIndex = targetIndex;

                    if (_dragPlaceholder != null)
                    {
                        UpdatePlaceholderPosition(targetIndex);
                    }
                }
            }
        }

        private void UpdatePlaceholderPosition(int targetIndex)
        {
            if (_dragPlaceholder == null) return;

            RectTransform placeholderRect = _dragPlaceholder.GetComponent<RectTransform>();
            if (placeholderRect == null) return;

            if (targetIndex >= _personalSections.Count)
            {
                if (_personalSections.Count > 0)
                {
                    var lastSection = _personalSections[_personalSections.Count - 1];
                    if (_sectionUIMap.TryGetValue(lastSection.id, out var lastSectionUI))
                    {
                        Vector3[] corners = new Vector3[4];
                        lastSectionUI.RectTransform.GetWorldCorners(corners);
                        float bottomY = corners[0].y;

                        Vector2 localPoint;
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            _uiManager.ContentRect,
                            new Vector2(Screen.width / 2, bottomY),
                            null,
                            out localPoint);

                        placeholderRect.anchoredPosition = new Vector2(0, localPoint.y);
                    }
                }
            }
            else if (targetIndex >= 0 && targetIndex < _personalSections.Count)
            {
                var targetSection = _personalSections[targetIndex];
                if (_sectionUIMap.TryGetValue(targetSection.id, out var targetSectionUI))
                {
                    Vector3[] corners = new Vector3[4];
                    targetSectionUI.RectTransform.GetWorldCorners(corners);
                    float topY = corners[1].y;

                    Vector2 localPoint;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _uiManager.ContentRect,
                        new Vector2(Screen.width / 2, topY),
                        null,
                        out localPoint);

                    placeholderRect.anchoredPosition = new Vector2(0, localPoint.y);
                }
            }
        }

        private void UpdateShareIndicatorPosition()
        {
            if (_dragShareIndicator == null) return;

            RectTransform indicatorRect = _dragShareIndicator.GetComponent<RectTransform>();
            if (indicatorRect == null) return;

            if (_uiManager.IsFullscreenLayout)
            {
                RectTransform colRt = _uiManager.RightColumnRoot.GetComponent<RectTransform>();
                if (colRt == null) return;

                float width = colRt.rect.width;
                float height = colRt.rect.height;

                indicatorRect.anchoredPosition = new Vector2(0, 0);
                indicatorRect.sizeDelta = new Vector2(width, height);

                RebuildDashes(_dragShareIndicator, width, height,
                    new Color(1f, 0.6f, 0.08f, 0.7f), 2f, 10f, 6f);
            }
            else
            {
                if (_dividerObject == null) return;

                RectTransform dividerRect = _dividerObject.GetComponent<RectTransform>();
                Vector3[] divCorners = new Vector3[4];
                dividerRect.GetWorldCorners(divCorners);
                float topWorldY = divCorners[1].y;
                float bottomWorldY = divCorners[0].y;

                if (_publicSections.Count > 0)
                {
                    var lastPublic = _publicSections[_publicSections.Count - 1];
                    if (_sectionUIMap.TryGetValue(lastPublic.id, out var lastUI))
                    {
                        Vector3[] secCorners = new Vector3[4];
                        lastUI.RectTransform.GetWorldCorners(secCorners);
                        bottomWorldY = secCorners[0].y;
                    }
                }

                Vector2 topLocal, bottomLocal;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _uiManager.ContentRect, new Vector2(0, topWorldY), null, out topLocal);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _uiManager.ContentRect, new Vector2(0, bottomWorldY), null, out bottomLocal);

                float height = Mathf.Max(topLocal.y - bottomLocal.y, 60f);
                float width = _uiManager.ContentRect.rect.width;

                indicatorRect.anchoredPosition = new Vector2(0, topLocal.y);
                indicatorRect.sizeDelta = new Vector2(width, height);

                RebuildDashes(_dragShareIndicator, width, height,
                    new Color(1f, 0.6f, 0.08f, 0.7f), 2f, 10f, 6f);
            }
        }

        private void RebuildDashes(GameObject frame, float width, float height,
            Color color, float thickness, float dashLen, float gapLen)
        {
            Vector2 newSize = new Vector2(width, height);
            if (newSize == _lastDashSize)
                return;
            _lastDashSize = newSize;

            Transform dashContainer = frame.transform.Find("DashContainer");
            if (dashContainer == null)
            {
                GameObject dcObj = new GameObject("DashContainer");
                dcObj.transform.SetParent(frame.transform, false);
                RectTransform dcRect = dcObj.AddComponent<RectTransform>();
                dcRect.anchorMin = Vector2.zero;
                dcRect.anchorMax = Vector2.one;
                dcRect.offsetMin = Vector2.zero;
                dcRect.offsetMax = Vector2.zero;
                dashContainer = dcObj.transform;
            }

            for (int i = dashContainer.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(dashContainer.GetChild(i).gameObject);

            float stride = dashLen + gapLen;
            float inset = 2f;

            for (float x = inset; x < width - inset; x += stride)
            {
                float len = Mathf.Min(dashLen, width - inset - x);
                CreateDash(dashContainer, color, x, -thickness * 0.5f, len, thickness, true);
            }
            for (float x = inset; x < width - inset; x += stride)
            {
                float len = Mathf.Min(dashLen, width - inset - x);
                CreateDash(dashContainer, color, x, -height + thickness * 0.5f, len, thickness, true);
            }
            for (float y = inset; y < height - inset; y += stride)
            {
                float len = Mathf.Min(dashLen, height - inset - y);
                CreateDash(dashContainer, color, thickness * 0.5f, -y, thickness, len, false);
            }
            for (float y = inset; y < height - inset; y += stride)
            {
                float len = Mathf.Min(dashLen, height - inset - y);
                CreateDash(dashContainer, color, width - thickness * 0.5f, -y, thickness, len, false);
            }
        }

        private static void CreateDash(Transform parent, Color color,
            float x, float y, float w, float h, bool horizontal)
        {
            GameObject dash = new GameObject("Dash");
            dash.transform.SetParent(parent, false);

            RectTransform rt = dash.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = horizontal ? new Vector2(0, 0.5f) : new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);

            Image img = dash.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        private static GameObject CreateDashedFrame(Transform parent, Color color,
            float thickness, float dashLength, float gapLength)
        {
            GameObject frame = new GameObject("DragShareIndicator");
            frame.transform.SetParent(parent, false);

            RectTransform frameRect = frame.AddComponent<RectTransform>();
            frameRect.anchorMin = new Vector2(0, 1);
            frameRect.anchorMax = new Vector2(0, 1);
            frameRect.pivot = new Vector2(0, 1);

            LayoutElement le = frame.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(frame.transform, false);

            RectTransform fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.6f, 0.35f, 0.05f, 0.25f);
            fillImage.raycastTarget = false;

            GameObject hintBox = new GameObject("HintBox");
            hintBox.transform.SetParent(frame.transform, false);

            RectTransform hintBoxRect = hintBox.AddComponent<RectTransform>();
            hintBoxRect.anchorMin = new Vector2(0.5f, 0.5f);
            hintBoxRect.anchorMax = new Vector2(0.5f, 0.5f);
            hintBoxRect.pivot = new Vector2(0.5f, 0.5f);
            hintBoxRect.anchoredPosition = Vector2.zero;
            hintBoxRect.sizeDelta = new Vector2(200f, 80f);

            Image hintBoxBg = hintBox.AddComponent<Image>();
            hintBoxBg.color = new Color(0.04f, 0.04f, 0.04f, 0.85f);
            hintBoxBg.raycastTarget = false;

            GameObject textObj = new GameObject("HintText");
            textObj.transform.SetParent(hintBox.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8, 4);
            textRect.offsetMax = new Vector2(-8, -4);

            Text hintText = textObj.AddComponent<Text>();
            hintText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            hintText.text = "DROP NOTE\nHERE\nTO SHARE";
            hintText.fontSize = 18;
            hintText.fontStyle = FontStyle.Bold;
            hintText.color = new Color(1f, 0.7f, 0.2f, 0.85f);
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.raycastTarget = false;

            return frame;
        }

        private void OnSectionDragEnd(string sectionId, Vector2 position)
        {
            if (_sectionUIMap.TryGetValue(sectionId, out var sectionUI))
            {
                var canvasGroup = sectionUI.SectionObject.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                    canvasGroup.alpha = 1f;
            }

            if (_dragPlaceholder != null)
            {
                UnityEngine.Object.Destroy(_dragPlaceholder);
                _dragPlaceholder = null;
            }

            if (_dragShareIndicator != null)
            {
                UnityEngine.Object.Destroy(_dragShareIndicator);
                _dragShareIndicator = null;
            }

            var section = FindSection(sectionId);
            if (section == null)
            {
                _draggedSectionId = null;
                _dragTargetIndex = -1;
                return;
            }

            var syncManager = BeefsRecipesPlugin.Instance?.ClientSyncManager;
            bool isDropBelowDivider = IsDropBelowDivider(position);
            bool isDropAboveDivider = !isDropBelowDivider;

            if (!section.isPublic && isDropBelowDivider && syncManager != null)
            {
                _personalSections.Remove(section);
                syncManager.ShareSection(section);
            }
            else if (section.isPublic && section.ownerId == GetLocalClientId() && isDropAboveDivider && syncManager != null)
            {
                if (!_personalSections.Exists(s => s.id == sectionId))
                    _personalSections.Add(section);
                syncManager.UnshareSection(sectionId);
                RebuildUI();
            }
            else if (!section.isPublic && _dragTargetIndex >= 0)
            {
                int oldIndex = _personalSections.FindIndex(s => s.id == sectionId);
                if (oldIndex >= 0 && oldIndex != _dragTargetIndex)
                {
                    _personalSections.RemoveAt(oldIndex);

                    int insertIndex = _dragTargetIndex;
                    if (oldIndex < _dragTargetIndex)
                        insertIndex--;

                    insertIndex = Mathf.Clamp(insertIndex, 0, _personalSections.Count);
                    _personalSections.Insert(insertIndex, section);
                    RebuildUI();
                }
            }

            _draggedSectionId = null;
            _dragTargetIndex = -1;
        }

        private bool IsDropBelowDivider(Vector2 screenPosition)
        {
            if (_uiManager.IsFullscreenLayout && _uiManager.RightColumnRoot != null)
            {
                RectTransform rightCol = _uiManager.RightColumnRoot.GetComponent<RectTransform>();
                if (rightCol == null) return false;

                Vector3[] corners = new Vector3[4];
                rightCol.GetWorldCorners(corners);
                return screenPosition.x >= corners[0].x;
            }

            if (_dividerObject == null || !_dividerObject.activeSelf)
                return false;

            RectTransform dividerRect = _dividerObject.GetComponent<RectTransform>();
            if (dividerRect == null) return false;

            Vector3[] divCorners = new Vector3[4];
            dividerRect.GetWorldCorners(divCorners);
            float dividerCenterY = (divCorners[0].y + divCorners[1].y) / 2f;

            return screenPosition.y < dividerCenterY;
        }

        private int CalculateDropIndex(Vector2 screenPosition)
        {
            int closestIndex = 0;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < _personalSections.Count; i++)
            {
                var section = _personalSections[i];
                if (_sectionUIMap.TryGetValue(section.id, out var sectionUI))
                {
                    Vector3[] corners = new Vector3[4];
                    sectionUI.RectTransform.GetWorldCorners(corners);

                    float centerY = (corners[0].y + corners[1].y) / 2f;
                    float distance = Mathf.Abs(screenPosition.y - centerY);

                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestIndex = i;

                        if (screenPosition.y > centerY)
                            closestIndex = i;
                        else
                            closestIndex = i + 1;
                    }
                }
            }

            return Mathf.Clamp(closestIndex, 0, _personalSections.Count);
        }

        private void OnTextClicked(string sectionId, PointerEventData eventData)
        {
            if (!_sectionCheckboxes.ContainsKey(sectionId)) return;

            var section = FindSection(sectionId);
            if (section != null && section.isPublic && section.ownerId != GetLocalClientId())
                return;

            var sectionUI = _sectionUIMap[sectionId];
            Text displayText = sectionUI.DisplayText;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                displayText.rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint);

            int lineClicked = GetLogicalLineFromLocalPoint(displayText, localPoint);

            var checkbox = _sectionCheckboxes[sectionId].Find(cb => cb.LineNumber == lineClicked);
            if (checkbox != null)
            {

                if (section != null)
                {
                    section.content = NotesMarkdownConverter.UpdateCheckbox(section.content, lineClicked, !checkbox.IsChecked);
                    ApplyState();

                    if (section.isPublic && section.ownerId == GetLocalClientId())
                    {
                        BeefsRecipesPlugin.Instance?.ClientSyncManager?
                            .PushPublicSectionUpdates(new List<BeefsRecipesPlugin.RecipeSection> { section });
                    }
                }
            }
        }

        private void ShowColorPicker(Vector2 screenPosition, string sectionId, ColorTarget target)
        {
            var section = FindSection(sectionId);
            if (section == null) return;

            Color current = Color.white;
            string hex = target == ColorTarget.Title ? section.titleColorHex : section.contentColorHex;
            if (!string.IsNullOrEmpty(hex)) ColorUtility.TryParseHtmlString(hex, out current);

            RecipesColorPicker.Show(screenPosition, current, (selectedColor) =>
            {
                string selectedHex = selectedColor == Color.clear ? null : "#" + ColorUtility.ToHtmlStringRGB(selectedColor);

                if (target == ColorTarget.Title) section.titleColorHex = selectedHex;
                else                             section.contentColorHex = selectedHex;

                ApplyColor(sectionId);
                ApplyState();
            });
        }

        private void ShowAccentColorPicker(Vector2 screenPosition)
        {
            var syncManager = BeefsRecipesPlugin.Instance?.ClientSyncManager;
            if (syncManager == null) return;

            Color current = Color.white;
            string currentHex = syncManager.GetPlayerColor(GetLocalClientId());
            if (!string.IsNullOrEmpty(currentHex))
                ColorUtility.TryParseHtmlString(currentHex, out current);

            RecipesColorPicker.Show(screenPosition, current, (selectedColor) =>
            {
                string hex = selectedColor == Color.clear
                    ? null
                    : "#" + ColorUtility.ToHtmlStringRGB(selectedColor);

                syncManager.SetAccentColorOverride(hex);
                RefreshAccentColors();
            });
        }

        public void RefreshAccentColors()
        {
            _suitColorResolved = false;
            _lastLocalAccentColor = Color.clear;

            var syncManager = BeefsRecipesPlugin.Instance?.ClientSyncManager;

            foreach (var kvp in _sectionUIMap)
            {
                if (!kvp.Value.IsPublic) continue;

                var section = FindSection(kvp.Value.SectionId);
                if (section == null) continue;

                if (kvp.Value.OwnerTagText != null)
                {
                    Color accentColor = GetAccentColorForPlayer(section.ownerId);

                    var badgeColors = GetBadgeColors(accentColor);
                    kvp.Value.OwnerTagText.color = badgeColors.text;

                    if (kvp.Value.BadgeBackground != null)
                    {
                        float currentAlpha = kvp.Value.BadgeBackground.color.a;
                        Color newBg = badgeColors.background;
                        newBg.a = currentAlpha;
                        kvp.Value.BadgeBackground.color = newBg;
                    }

                    if (kvp.Value.BadgeOutline != null)
                        kvp.Value.BadgeOutline.effectColor = badgeColors.border;
                }
            }
        }

        public void RefreshPresenceIndicators()
        {
            var syncManager = BeefsRecipesPlugin.Instance?.ClientSyncManager;
            if (syncManager == null) return;

            foreach (var kvp in _sectionUIMap)
            {
                if (!kvp.Value.IsPublic) continue;
                if (kvp.Value.PresenceDot == null) continue;

                var section = FindSection(kvp.Value.SectionId);
                if (section == null) continue;

                bool isOnline = kvp.Value.IsOwnedByLocal
                    || syncManager.IsPlayerOnline(section.ownerId);
                ApplyPresenceDotState(kvp.Value.PresenceDot, kvp.Value.PresenceGlow, isOnline);
            }
        }

        private static Sprite _glowSprite;
        private static Sprite _solidDotSprite;
        private static Sprite _pillSprite;

        private static Color GetAccentColorForPlayer(ulong ownerId)
        {
            ulong localId = GetLocalClientId();

            if (ownerId == localId)
            {
                var syncManager = BeefsRecipesPlugin.Instance?.ClientSyncManager;
                if (syncManager != null)
                {
                    string overrideHex = syncManager.GetAccentColorOverride();
                    if (!string.IsNullOrEmpty(overrideHex))
                    {
                        Color parsed;
                        if (ColorUtility.TryParseHtmlString(overrideHex, out parsed))
                            return parsed;
                    }
                }

                try
                {
                    var human = Assets.Scripts.Objects.Entities.Human.LocalHuman;
                    if (human?.Suit != null)
                    {
                        var suitThing = human.Suit.AsThing;
                        if (suitThing?.CustomColor != null)
                        {
                            Color c = suitThing.CustomColor.Color;
                            if (!(c.r >= 0.9f && c.g >= 0.9f && c.b >= 0.9f))
                                return c;
                        }
                    }
                }
                catch { }
            }
            else
            {
                var syncManager = BeefsRecipesPlugin.Instance?.ClientSyncManager;
                if (syncManager != null)
                {
                    string hex = syncManager.GetPlayerColor(ownerId);
                    if (!string.IsNullOrEmpty(hex))
                    {
                        Color parsed;
                        if (ColorUtility.TryParseHtmlString(hex, out parsed))
                            return parsed;
                    }
                }
            }

            return new Color(0.5f, 0.8f, 1f, 1f);
        }

        private static (Color background, Color text, Color border) GetBadgeColors(Color accent)
        {
            Color.RGBToHSV(accent, out float h, out float s, out float v);

            Color bg;
            Color border;
            if (v < 0.3f)
            {
                bg = Color.HSVToRGB(h, Mathf.Min(s, 0.2f), 0.80f);
                border = Color.HSVToRGB(h, Mathf.Min(s, 0.3f), 0.6f);
            }
            else
            {
                bg = Color.HSVToRGB(h, Mathf.Min(s, 0.7f), 0.12f);
                border = Color.HSVToRGB(h, Mathf.Min(s, 0.6f), 0.35f);
            }
            bg.a = 0.9f;
            border.a = 0.8f;

            Color text = accent;
            text.a = 0.95f;

            return (bg, text, border);
        }

        private static Sprite GetPillSprite()
        {
            if (_pillSprite != null) return _pillSprite;

            const int size = 32;
            const int radius = 10;
            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = 0, dy = 0;
                    bool inCorner = false;

                    if (x < radius && y < radius)
                    { dx = radius - x - 0.5f; dy = radius - y - 0.5f; inCorner = true; }
                    else if (x >= size - radius && y < radius)
                    { dx = x - (size - radius) + 0.5f; dy = radius - y - 0.5f; inCorner = true; }
                    else if (x < radius && y >= size - radius)
                    { dx = radius - x - 0.5f; dy = y - (size - radius) + 0.5f; inCorner = true; }
                    else if (x >= size - radius && y >= size - radius)
                    { dx = x - (size - radius) + 0.5f; dy = y - (size - radius) + 0.5f; inCorner = true; }

                    float alpha;
                    if (inCorner)
                    {
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        alpha = dist <= radius ? 1f : 0f;
                        if (dist > radius - 1f && dist <= radius)
                            alpha = 1f - (dist - (radius - 1f));
                    }
                    else
                    {
                        alpha = 1f;
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();

            _pillSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius)
            );

            return _pillSprite;
        }

        private static Sprite GetGlowSprite()
        {
            if (_glowSprite != null) return _glowSprite;

            const int size = 32;
            float center = size / 2f;
            float radius = size / 2f;

            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - center + 0.5f) * (x - center + 0.5f) +
                                            (y - center + 0.5f) * (y - center + 0.5f));
                    float normalized = dist / radius;

                    float alpha;
                    if (normalized <= 0.35f)
                        alpha = 1f;
                    else if (normalized <= 1f)
                        alpha = 1f - Mathf.SmoothStep(0f, 1f, (normalized - 0.35f) / 0.65f);
                    else
                        alpha = 0f;

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            _glowSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
            return _glowSprite;
        }

        private static Sprite GetSolidDotSprite()
        {
            if (_solidDotSprite != null) return _solidDotSprite;

            const int size = 16;
            float center = size / 2f;
            float radius = size / 2f;

            Texture2D tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Mathf.Sqrt((x - center + 0.5f) * (x - center + 0.5f) +
                                            (y - center + 0.5f) * (y - center + 0.5f));
                    float alpha = dist <= radius ? 1f : 0f;
                    if (dist > radius - 1f && dist <= radius)
                        alpha = 1f - (dist - (radius - 1f));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            _solidDotSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
            return _solidDotSprite;
        }

        private static void ApplyPresenceDotState(Image dot, Image glow, bool isOnline)
        {
            if (dot == null) return;

            if (isOnline)
            {
                dot.sprite = GetSolidDotSprite();
                dot.color = new Color(0.37f, 0.91f, 0.48f, 1f);

                if (glow != null)
                {
                    glow.gameObject.SetActive(true);
                    glow.sprite = GetGlowSprite();
                    glow.color = new Color(0.37f, 0.91f, 0.48f, 0.6f);
                }
            }
            else
            {
                dot.sprite = GetSolidDotSprite();
                dot.color = new Color(0.6f, 0.27f, 0.27f, 0.7f);

                if (glow != null)
                {
                    glow.gameObject.SetActive(false);
                }
            }
        }

        private void ApplyColor(string sectionId)
        {
            var section = FindSection(sectionId);
            if (section == null || !_sectionUIMap.TryGetValue(sectionId, out var ui)) return;

            Color titleColor = Color.white;
            Color contentColor = Color.white;

            if (!string.IsNullOrEmpty(section.titleColorHex))
                ColorUtility.TryParseHtmlString(section.titleColorHex, out titleColor);

            if (!string.IsNullOrEmpty(section.contentColorHex))
                ColorUtility.TryParseHtmlString(section.contentColorHex, out contentColor);

            if (ui.TitleField?.textComponent)   ui.TitleField.textComponent.color   = titleColor;
            if (ui.ContentField?.textComponent) ui.ContentField.textComponent.color = contentColor;

            if (ui.TitleDisplayText)    ui.TitleDisplayText.color    = titleColor;
            if (ui.DisplayText)         ui.DisplayText.color         = contentColor;
            if (ui.CollapsedPreviewText) ui.CollapsedPreviewText.color = contentColor;
        }

        private void OnDragHandleDoubleClick(string sectionId)
        {
            var section = FindSection(sectionId);
            if (section == null) return;

            section.isCollapsed = !section.isCollapsed;
            CollapseSection(sectionId);
        }

        private void CollapseSection(string sectionId)
        {
            var section = FindSection(sectionId);
            if (section == null || !_sectionUIMap.TryGetValue(sectionId, out var sectionUI)) return;

            RecipesDragHandle recipesDragHandle = sectionUI.DragHandle?.GetComponent<RecipesDragHandle>();
            if (recipesDragHandle != null)
            {
                recipesDragHandle.SetCollapsed(section.isCollapsed);
            }

            if (section.isCollapsed)
            {
                sectionUI.TitleField.gameObject.SetActive(false);
                sectionUI.TitleDisplayObject.SetActive(false);
                if (sectionUI.ContentField != null) sectionUI.ContentField.gameObject.SetActive(false);
                if (sectionUI.DisplayObject != null) sectionUI.DisplayObject.SetActive(false);
                if (sectionUI.DrawingToolbar != null) sectionUI.DrawingToolbar.SetActive(false);
                if (sectionUI.DrawingCanvasWrapper != null) sectionUI.DrawingCanvasWrapper.SetActive(false);
                if (sectionUI.DrawingEditHint != null) sectionUI.DrawingEditHint.SetActive(false);
                sectionUI.CollapsedPreviewObject.SetActive(true);

                string previewText = section.isDrawing ? "[Drawing]" : section.content;
                if (previewText != null && previewText.Length > 15)
                {
                    previewText = previewText.Substring(0, 15) + "...";
                }

                string displayText = "";
                if (!string.IsNullOrWhiteSpace(section.title))
                {
                    displayText = section.title;
                }
                if (!string.IsNullOrWhiteSpace(previewText))
                {
                    if (!string.IsNullOrWhiteSpace(displayText))
                        displayText += "\n";
                    displayText += previewText;
                }

                sectionUI.CollapsedPreviewText.text = displayText;

                if (section.isDrawing)
                {
                    if (sectionUI.DrawingCanvasWrapper != null)
                    {
                        var drawingLE = sectionUI.DrawingCanvasWrapper.GetComponent<LayoutElement>();
                        if (drawingLE != null)
                        {
                            drawingLE.preferredHeight = 0;
                            drawingLE.minHeight = 0;
                        }
                    }

                    if (sectionUI.TextBoxLayout != null)
                    {
                        sectionUI.TextBoxLayout.preferredHeight = -1;
                    }
                }

                LayoutRebuilder.ForceRebuildLayoutImmediate(sectionUI.RectTransform);
            }
            else
            {
                sectionUI.CollapsedPreviewObject.SetActive(false);
                if (_uiManager.IsFullscreenLayout)
                    RebuildUI();
                else
                    ApplyState();
            }
        }

        private static BeefsRecipesPlugin.RecipeSection CreateDrawingSection()
        {
            return new BeefsRecipesPlugin.RecipeSection
            {
                id = Guid.NewGuid().ToString(),
                title = "",
                content = "",
                titleColorHex = null,
                contentColorHex = null,
                isCollapsed = false,
                isPublic = false,
                ownerId = 0,
                isDrawing = true,
                drawingPngBase64 = null,
                drawingHeight = 288,
                drawingShowBg = false
            };
        }

        private void UpdateDrawingCanvasHeightCap()
        {
        }

        private void ActivateDrawing(string sectionId)
        {
            if (!_panelManager.IsEditing) return;
            if (_activeDrawingSectionId == sectionId) return;

            if (_activeDrawingSectionId != null)
            {
                SaveAndDeactivateDrawing(_activeDrawingSectionId);
            }

            _activeDrawingSectionId = sectionId;

            if (_sectionUIMap.TryGetValue(sectionId, out var ui) && ui.SketchPad != null)
            {
                ui.SketchPad.SetupBrushCursor();
            }

            _stateDirty = true;
            _layoutRebuildFrames = 3;
        }

        private void SaveAndDeactivateDrawing(string sectionId)
        {
            if (string.IsNullOrEmpty(sectionId)) return;

            if (_sectionUIMap.TryGetValue(sectionId, out var ui) && ui.IsDrawing && ui.SketchPad != null)
            {
                if (ui.SketchPad.IsDirty)
                {
                    var section = FindSection(sectionId);
                    if (section != null)
                    {
                        section.drawingPngBase64 = ui.SketchPad.SaveToPng();
                        BeefsRecipesPlugin.Log.LogInfo(
                            $"Rasterized drawing for section {section.id.Substring(0, 8)}...");

                        if (section.isPublic)
                            _modifiedPublicSectionIds.Add(section.id);
                    }
                }
            }

            if (_activeDrawingSectionId == sectionId)
            {
                _activeDrawingSectionId = null;
                _stateDirty = true;
                _layoutRebuildFrames = 3;
            }
        }

        private void SetDrawingActiveFraction(string sectionId, float fraction)
        {
            if (!_sectionUIMap.TryGetValue(sectionId, out var ui)) return;
            if (ui.DrawingCanvasWrapper == null) return;

            var clamp = ui.DrawingCanvasWrapper.GetComponent<DrawingHeightClamp>();
            if (clamp != null)
                clamp.activeMaxFraction = fraction;
        }

        private void ToggleDrawingBackground(string sectionId)
        {
            var section = FindSection(sectionId);
            if (section == null || !section.isDrawing) return;

            section.drawingShowBg = !section.drawingShowBg;
            _stateDirty = true;

            RecipeSectionUI ui;
            if (_sectionUIMap.TryGetValue(sectionId, out ui))
            {
                if (ui.BgCheckboxImage != null)
                {
                    ui.BgCheckboxImage.sprite = RecipesUIManager.CreateCheckboxSprite(section.drawingShowBg);
                    ui.BgCheckboxImage.color = section.drawingShowBg
                        ? new Color(1f, 0.5f, 0.15f, 0.9f)
                        : new Color(0.6f, 0.6f, 0.6f, 0.7f);
                }

                if (ui.DrawingWrapperBg != null)
                {
                    ui.DrawingWrapperBg.color = section.drawingShowBg
                        ? new Color(0.12f, 0.12f, 0.12f, 1f)
                        : new Color(0, 0, 0, 0);
                }
            }
        }

        private static GameObject CreateDrawingBorderFrame(Transform parent, Color color, float thickness)
        {
            GameObject frame = new GameObject("DrawingBorderFrame");
            frame.transform.SetParent(parent, false);

            RectTransform frameRect = frame.AddComponent<RectTransform>();
            frameRect.anchorMin = Vector2.zero;
            frameRect.anchorMax = Vector2.one;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;

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

        private void InsertDrawingSectionAfter(string afterSectionId)
        {
            int index = _personalSections.FindIndex(s => s.id == afterSectionId);
            if (index < 0) index = _personalSections.Count - 1;

            var newSection = CreateDrawingSection();
            _personalSections.Insert(index + 1, newSection);
            RebuildUI();
            _layoutRebuildFrames = 3;
        }

        private void ShowDrawingColorPicker(string sectionId)
        {
            if (!_sectionUIMap.TryGetValue(sectionId, out var ui)) return;
            if (ui.SketchPad == null) return;

            Color currentColor = ui.SketchPad.GetBrushColor();
            Vector2 mousePos = Input.mousePosition;

            RecipesColorPicker.Show(mousePos, currentColor, (selectedColor) =>
            {
                if (_sectionUIMap.TryGetValue(sectionId, out var uiInner) && uiInner.SketchPad != null)
                {
                    uiInner.SketchPad.SetBrushColor(selectedColor);
                    uiInner.SketchPad.SetEraser(false);

                    if (uiInner.BrushColorSwatch != null)
                        uiInner.BrushColorSwatch.color = selectedColor;

                    if (uiInner.EraserButtonImage != null)
                        uiInner.EraserButtonImage.color = new Color(0.25f, 0.25f, 0.25f, 0.8f);
                }
            });
        }

        private void ToggleEraser(string sectionId)
        {
            if (!_sectionUIMap.TryGetValue(sectionId, out var ui)) return;
            if (ui.SketchPad == null) return;

            bool newState = !ui.SketchPad.IsEraser;
            ui.SketchPad.SetEraser(newState);

            Color activeColor = new Color(1f, 0.5f, 0.15f, 1f);
            Color inactiveColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);

            if (ui.BrushIconImage != null)
                ui.BrushIconImage.color = newState ? inactiveColor : activeColor;

            if (ui.EraserIconImage != null)
                ui.EraserIconImage.color = newState ? activeColor : inactiveColor;

            if (ui.ToolModeLabel != null)
                ui.ToolModeLabel.text = newState ? "Eraser" : "Brush";
        }

        private Image CreateToolbarPillButton(
            Transform parent, string name, string label, float width, float height,
            Color bgColor, out Button button)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            LayoutElement btnLE = btnObj.AddComponent<LayoutElement>();
            btnLE.preferredWidth = width;
            btnLE.preferredHeight = height;

            Image bg = btnObj.AddComponent<Image>();
            bg.sprite = GetPillSprite();
            bg.type = Image.Type.Sliced;
            bg.color = bgColor;
            bg.raycastTarget = true;

            Outline outline = btnObj.AddComponent<Outline>();
            outline.effectColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
            outline.effectDistance = new Vector2(1f, -1f);

            button = btnObj.AddComponent<Button>();
            button.targetGraphic = bg;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = bgColor;
            colors.highlightedColor = new Color(
                Mathf.Min(bgColor.r + 0.1f, 1f),
                Mathf.Min(bgColor.g + 0.1f, 1f),
                Mathf.Min(bgColor.b + 0.1f, 1f),
                Mathf.Min(bgColor.a + 0.1f, 1f));
            colors.pressedColor = new Color(
                Mathf.Min(bgColor.r + 0.2f, 1f),
                Mathf.Min(bgColor.g + 0.2f, 1f),
                Mathf.Min(bgColor.b + 0.2f, 1f),
                1f);
            colors.disabledColor = new Color(0.15f, 0.15f, 0.15f, 0.4f);
            colors.fadeDuration = 0.05f;
            button.colors = colors;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = label;
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(height * 0.5f));
            text.color = new Color(1f, 1f, 1f, 0.9f);
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            return bg;
        }

        private static GameObject CreateDrawingSlider(Transform parent, float min, float max, float initial)
        {
            GameObject sliderObj = new GameObject("BrushSizeSlider");
            sliderObj.transform.SetParent(parent, false);

            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(0, 18);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = initial;
            slider.wholeNumbers = true;

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
            handleRect.sizeDelta = new Vector2(10, 0);

            Image handleImage = handleObj.AddComponent<Image>();
            handleImage.color = new Color(1f, 0.5f, 0.15f, 1f);

            slider.targetGraphic = handleImage;
            slider.handleRect = handleRect;

            return sliderObj;
        }
    }
}