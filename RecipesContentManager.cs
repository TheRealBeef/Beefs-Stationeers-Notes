using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BeefsRecipes
{
    public class RecipesContentManager
    {
        private readonly RecipesUIManager _uiManager;
        private readonly RecipesPanelManager _panelManager;

        private List<BeefsRecipesPlugin.RecipeSection> _recipeSections = new List<BeefsRecipesPlugin.RecipeSection>();
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

        private string _draggedSectionId = null;
        private GameObject _dragPlaceholder = null;
        private int _dragTargetIndex = -1;

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
            public Button AddButton;
            public Button DeleteButton;
            public GameObject DragHandle;
            public string SectionId;
            public LayoutElement TextBoxLayout;
            public LayoutElement ButtonContainerLayout;
        }

        public RecipesContentManager(RecipesUIManager uiManager, RecipesPanelManager panelManager)
        {
            this._uiManager = uiManager;
            this._panelManager = panelManager;
        }

        public void Update()
        {

            if (_panelManager.IsEditing)
            {
                HandleColorPickerInput();

                if (Input.GetKeyDown(KeyCode.Tab))
                {
                    bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                    HandleTabNavigation(forward: !isShiftHeld);
                    Input.GetKeyDown(KeyCode.Tab);
                    if (Input.GetKey(KeyCode.Tab)) { }
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
                        Input.GetKeyDown(KeyCode.Return);
                        Input.GetKeyDown(KeyCode.KeypadEnter);
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

            if (_panelManager.IsEditing)
            {
                foreach (var kvp in _sectionUIMap)
                {
                    if (kvp.Value.ContentField != null && kvp.Value.ContentField.textComponent != null)
                    {
                        kvp.Value.ContentField.textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
                        RebuildLayoutHierarchy(kvp.Value.ContentField.GetComponent<RectTransform>());
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

            ApplyState();
        }

        public void LoadNotes(string worldName, string saveFileName)
        {
            _currentWorldName = worldName;
            _currentSaveId = saveFileName;

            if (string.IsNullOrEmpty(_currentWorldName) || string.IsNullOrEmpty(_currentSaveId)) return;

            try
            {
                var data = BeefsRecipesSaveManager.LoadNotesData(_currentWorldName, _currentSaveId);
                _recipeSections = data.sections;

                if (_recipeSections == null || _recipeSections.Count == 0)
                {
                    _recipeSections = new List<BeefsRecipesPlugin.RecipeSection>
                    {
                        new BeefsRecipesPlugin.RecipeSection
                        {
                            id = Guid.NewGuid().ToString(),
                            title = "",
                            content = "",
                            titleColorHex = null,
                            contentColorHex = null,
                            isCollapsed = false
                        }
                    };
                }

                _fontSizeOffset = data.fontSizeOffset;
                _panelManager.SetPanelHeight(data.panelHeight);
                _panelManager.RestoreSavedPanelMode(data.panelMode);
                _panelManager.SetYOffset(data.panelYOffset);

                RebuildUI();
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error loading notes: {ex.Message}");
                _recipeSections = new List<BeefsRecipesPlugin.RecipeSection>
                {
                    new BeefsRecipesPlugin.RecipeSection
                    {
                        id = Guid.NewGuid().ToString(),
                        title = "",
                        content = "",
                        titleColorHex = null,
                        contentColorHex = null,
                        isCollapsed = false
                    }
                };
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
                    _recipeSections,
                    _fontSizeOffset,
                    panelHeight,
                    panelYOffset,
                    panelMode
                );
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error saving notes to {saveFileName}: {ex.Message}");
            }
        }

        public void ClearNotes()
        {
            _recipeSections.Clear();
            RebuildUI();
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

            foreach (var section in _recipeSections)
            {
                var sectionUI = CreateNoteSection(section);
                _sectionUIMap[section.id] = sectionUI;
                _allInputFields.Add(sectionUI.TitleField);
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

            ApplyState();
            Canvas.ForceUpdateCanvases();
        }

        private RecipeSectionUI CreateNoteSection(BeefsRecipesPlugin.RecipeSection section)
        {
            GameObject sectionObj = new GameObject($"Section_{section.id}");
            sectionObj.transform.SetParent(_uiManager.ContentObject.transform, false);

            VerticalLayoutGroup sectionLayout = sectionObj.AddComponent<VerticalLayoutGroup>();
            sectionLayout.childControlHeight = false;
            sectionLayout.childControlWidth = true;
            sectionLayout.childForceExpandHeight = false;
            sectionLayout.childForceExpandWidth = true;
            sectionLayout.spacing = 5f;
            sectionLayout.padding = new RectOffset(5, 5, 5, 5);

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

            InputField contentField = _uiManager.CreateInputField(textBoxContainer.transform, "ContentField", RecipesUIManager.ExpandedFontSize, true);
            contentField.text = section.content;
            _uiManager.SetPlaceholder(contentField, "<Enter Notes Here>");
            contentField.onValueChanged.AddListener((value) => {
                OnSectionContentChanged(section.id, value);
                contentField.textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
                RebuildLayoutHierarchy(contentField.GetComponent<RectTransform>());
            });
            contentField.onValidateInput = BlockTab;

            GameObject displayObject = new GameObject("DisplayText");
            displayObject.transform.SetParent(textBoxContainer.transform, false);

            RectTransform displayRect = displayObject.AddComponent<RectTransform>();
            displayRect.anchorMin = new Vector2(0, 1);
            displayRect.anchorMax = new Vector2(1, 1);
            displayRect.pivot = new Vector2(0.5f, 1);
            displayRect.anchoredPosition = new Vector2(0, -5);
            displayRect.sizeDelta = new Vector2(-10, 800);

            Text displayText = displayObject.AddComponent<Text>();
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

            GameObject buttonContainer = new GameObject("ButtonContainer");
            buttonContainer.transform.SetParent(contentRow.transform, false);

            LayoutElement buttonContainerLayout = buttonContainer.AddComponent<LayoutElement>();
            buttonContainerLayout.minWidth = RecipesUIManager.ButtonSize * 2 + 5;
            buttonContainerLayout.preferredWidth = RecipesUIManager.ButtonSize * 2 + 5;
            buttonContainerLayout.flexibleWidth = 0f;

            HorizontalLayoutGroup buttonLayout = buttonContainer.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.childControlWidth = true;
            buttonLayout.childControlHeight = true;
            buttonLayout.childForceExpandWidth = false;
            buttonLayout.childForceExpandHeight = false;
            buttonLayout.spacing = 5f;
            buttonLayout.childAlignment = TextAnchor.UpperLeft;
            buttonLayout.padding = new RectOffset(0, 0, 0, 0);

            Button addButton = _uiManager.CreateButton(buttonContainer.transform, "+", new Color(0.2f, 0.6f, 0.2f), true, RecipesUIManager.ButtonSize);
            addButton.onClick.AddListener(() => AddNewSection(section.id));
            addButton.gameObject.SetActive(!string.IsNullOrWhiteSpace(section.content));

            Button deleteButton = _uiManager.CreateButton(buttonContainer.transform, "×", new Color(0.6f, 0.2f, 0.2f), true, RecipesUIManager.ButtonSize);
            deleteButton.onClick.AddListener(() => DeleteSection(section.id));
            deleteButton.gameObject.SetActive(_recipeSections.Count > 1);

            GameObject dragHandle = RecipesDragHandle.Create(
                sectionObj.transform,
                section.id,
                OnSectionDragStart,
                OnSectionDrag,
                OnSectionDragEnd,
                OnDragHandleDoubleClick
            );

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
                AddButton = addButton,
                DeleteButton = deleteButton,
                DragHandle = dragHandle,
                SectionId = section.id,
                TextBoxLayout = textBoxLayout,
                ButtonContainerLayout = buttonContainerLayout
            };

            titleField.interactable = _panelManager.IsEditing;
            contentField.interactable = _panelManager.IsEditing;

            return sectionUI;
        }

        private void OnCollapsedSectionClicked(string sectionId)
        {
            var section = _recipeSections.Find(s => s.id == sectionId);
            if (section != null && section.isCollapsed)
            {
                section.isCollapsed = false;
                CollapseSection(sectionId);
            }
        }

        private float _lastClickTime = 0f;
        private string _lastClickedFieldId = "";

        private void HandleColorPickerInput()
        {
            if (Input.GetMouseButtonDown(1) || (Input.GetMouseButtonDown(0) && Time.time - _lastClickTime < 0.3f && !string.IsNullOrEmpty(_lastClickedFieldId)))
            {
                if (EventSystem.current != null)
                {
                    GameObject selected = EventSystem.current.currentSelectedGameObject;
                    if (selected != null)
                    {
                        InputField field = selected.GetComponent<InputField>();
                        if (field != null)
                        {
                            foreach (var kvp in _sectionUIMap)
                            {
                                if (kvp.Value.TitleField == field)
                                {
                                    ShowColorPicker(Input.mousePosition, kvp.Value.SectionId, ColorTarget.Title);
                                    break;
                                }
                                if (kvp.Value.ContentField == field)
                                {
                                    ShowColorPicker(Input.mousePosition, kvp.Value.SectionId, ColorTarget.Content);
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
                {
                    InputField field = EventSystem.current.currentSelectedGameObject.GetComponent<InputField>();
                    if (field != null)
                    {
                        foreach (var kvp in _sectionUIMap)
                        {
                            if (kvp.Value.TitleField == field || kvp.Value.ContentField == field)
                            {
                                if (Time.time - _lastClickTime < 0.3f && _lastClickedFieldId == kvp.Value.SectionId)
                                {
                                    return;
                                }
                                _lastClickedFieldId = kvp.Value.SectionId;
                                _lastClickTime = Time.time;
                                break;
                            }
                        }
                    }
                }
            }
        }

        private void ApplyState()
        {
            bool isPeekMode = _panelManager.IsPeekMode();
            bool showButtons = _panelManager.IsExpandedMode();
            bool isNarrow = _panelManager.IsTransitioning();

            foreach (var kvp in _sectionUIMap)
            {
                kvp.Value.DeleteButton.gameObject.SetActive(showButtons && _recipeSections.Count > 1);
                kvp.Value.AddButton.gameObject.SetActive(showButtons && !string.IsNullOrWhiteSpace(kvp.Value.ContentField.text));

                if (kvp.Value.DragHandle != null)
                {
                    kvp.Value.DragHandle.SetActive(showButtons);
                }

                if (isPeekMode)
                {
                    kvp.Value.TextBoxLayout.flexibleWidth = 1f;
                    kvp.Value.ButtonContainerLayout.preferredWidth = 0f;
                    kvp.Value.ButtonContainerLayout.minWidth = 0f;
                }
                else
                {
                    kvp.Value.TextBoxLayout.flexibleWidth = 1f;
                    kvp.Value.ButtonContainerLayout.preferredWidth = RecipesUIManager.ButtonSize * 2 + 5;
                    kvp.Value.ButtonContainerLayout.minWidth = RecipesUIManager.ButtonSize * 2 + 5;
                }

                var section = _recipeSections.Find(s => s.id == kvp.Value.SectionId);
                if (section != null)
                {
                    if (section.isCollapsed)
                    {
                        continue;
                    }

                    if (isPeekMode)
                    {
                        kvp.Value.TitleField.gameObject.SetActive(false);
                        kvp.Value.TitleDisplayObject.SetActive(true);
                        kvp.Value.ContentField.gameObject.SetActive(false);
                        kvp.Value.DisplayObject.SetActive(true);

                        kvp.Value.TitleDisplayText.fontSize = RecipesUIManager.TitleFontSize + _fontSizeOffset;
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
                    else
                    {
                        kvp.Value.TitleField.gameObject.SetActive(true);
                        kvp.Value.TitleDisplayObject.SetActive(false);
                        kvp.Value.ContentField.gameObject.SetActive(true);
                        kvp.Value.DisplayObject.SetActive(false);

                        kvp.Value.TitleField.textComponent.fontSize = RecipesUIManager.TitleFontSize + _fontSizeOffset;
                        kvp.Value.ContentField.textComponent.fontSize = RecipesUIManager.ExpandedFontSize + _fontSizeOffset;

                        if (kvp.Value.TitleField.text != section.title)
                        {
                            kvp.Value.TitleField.text = section.title;
                        }
                        if (kvp.Value.ContentField.text != section.content)
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
                if (kvp.Value.ContentField.textComponent != null)
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
                if (kvp.Value.ContentField.placeholder != null)
                {
                    kvp.Value.ContentField.placeholder.gameObject.SetActive(!isPeekMode && showText);
                    Text placeholderText = kvp.Value.ContentField.placeholder.GetComponent<Text>();
                    if (placeholderText != null)
                    {
                        placeholderText.fontSize = RecipesUIManager.ExpandedFontSize + _fontSizeOffset;
                    }
                }

                bool hasContent = !string.IsNullOrWhiteSpace(kvp.Value.TitleField.text) ||
                                  !string.IsNullOrWhiteSpace(kvp.Value.ContentField.text);
                kvp.Value.SectionObject.SetActive(!isPeekMode || hasContent);
            }
        }

        private void OnSectionTitleChanged(string sectionId, string newTitle)
        {
            var section = _recipeSections.Find(s => s.id == sectionId);
            if (section != null)
            {
                section.title = newTitle;

                if (_sectionUIMap.TryGetValue(sectionId, out var sectionUI))
                {
                    bool isPeekMode = _panelManager.IsPeekMode();
                    bool hasContent = !string.IsNullOrWhiteSpace(newTitle) ||
                                      !string.IsNullOrWhiteSpace(sectionUI.ContentField.text);
                    sectionUI.SectionObject.SetActive(!isPeekMode || hasContent);
                }
            }
        }

        private void OnSectionContentChanged(string sectionId, string newContent)
        {
            var section = _recipeSections.Find(s => s.id == sectionId);
            if (section != null)
            {
                section.content = newContent;

                if (_sectionUIMap.TryGetValue(sectionId, out var sectionUI))
                {
                    bool showButtons = _panelManager.IsExpandedMode();
                    sectionUI.AddButton.gameObject.SetActive(showButtons && !string.IsNullOrWhiteSpace(newContent));

                    bool isPeekMode = _panelManager.IsPeekMode();
                    bool hasContent = !string.IsNullOrWhiteSpace(sectionUI.TitleField.text) ||
                                      !string.IsNullOrWhiteSpace(newContent);
                    sectionUI.SectionObject.SetActive(!isPeekMode || hasContent);
                }
            }
        }

        private void AddNewSection(string afterSectionId)
        {
            int insertIndex = _recipeSections.FindIndex(s => s.id == afterSectionId) + 1;

            BeefsRecipesPlugin.RecipeSection newSection = new BeefsRecipesPlugin.RecipeSection
            {
                id = Guid.NewGuid().ToString(),
                title = "",
                content = "",
                titleColorHex = null,
                contentColorHex = null,
                isCollapsed = false
            };

            _recipeSections.Insert(insertIndex, newSection);
            RebuildUI();
        }

        private void DeleteSection(string sectionId)
        {
            if (_recipeSections.Count <= 1) return;

            _recipeSections.RemoveAll(s => s.id == sectionId);
            RebuildUI();
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
                kvp.Value.TitleField.interactable = isEditing;
                kvp.Value.ContentField.interactable = isEditing;
            }

            if (isEditing && _recipeSections.Count > 0 && _sectionUIMap.Count > 0)
            {
                var firstSection = _recipeSections[0];
                if (_sectionUIMap.TryGetValue(firstSection.id, out var sectionUI))
                {
                    sectionUI.ContentField.ActivateInputField();
                    _currentFocusedIndex = 1;
                }
            }
        }

        public void OnStateChanged()
        {
            ApplyState();
        }

        public bool IsCheckboxHere(GameObject displayTextObject, Vector2 screenPosition)
        {
            foreach (var kvp in _sectionUIMap)
            {
                if (kvp.Value.DisplayText != null && kvp.Value.DisplayText.gameObject == displayTextObject)
                {
                    if (!_sectionCheckboxes.ContainsKey(kvp.Value.SectionId))
                        return false;

                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        kvp.Value.DisplayText.rectTransform,
                        screenPosition,
                        null,
                        out Vector2 localPoint);

                    int lineClicked = Mathf.FloorToInt(-localPoint.y / (kvp.Value.DisplayText.fontSize + 4));

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
        }

        private void OnSectionDrag(string sectionId, Vector2 screenPosition)
        {
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

        private void UpdatePlaceholderPosition(int targetIndex)
        {
            if (_dragPlaceholder == null) return;

            RectTransform placeholderRect = _dragPlaceholder.GetComponent<RectTransform>();
            if (placeholderRect == null) return;

            if (targetIndex >= _recipeSections.Count)
            {
                if (_recipeSections.Count > 0)
                {
                    var lastSection = _recipeSections[_recipeSections.Count - 1];
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
            else if (targetIndex >= 0 && targetIndex < _recipeSections.Count)
            {
                var targetSection = _recipeSections[targetIndex];
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

            if (_dragTargetIndex >= 0)
            {
                int oldIndex = _recipeSections.FindIndex(s => s.id == sectionId);
                if (oldIndex >= 0 && oldIndex != _dragTargetIndex)
                {
                    var section = _recipeSections[oldIndex];
                    _recipeSections.RemoveAt(oldIndex);

                    int insertIndex = _dragTargetIndex;
                    if (oldIndex < _dragTargetIndex)
                        insertIndex--;

                    _recipeSections.Insert(insertIndex, section);
                    RebuildUI();
                }
            }

            _draggedSectionId = null;
            _dragTargetIndex = -1;
        }

        private int CalculateDropIndex(Vector2 screenPosition)
        {
            int closestIndex = 0;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < _recipeSections.Count; i++)
            {
                var section = _recipeSections[i];
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

            return Mathf.Clamp(closestIndex, 0, _recipeSections.Count);
        }

        private void OnTextClicked(string sectionId, PointerEventData eventData)
        {
            if (!_sectionCheckboxes.ContainsKey(sectionId)) return;

            var sectionUI = _sectionUIMap[sectionId];
            Text displayText = sectionUI.DisplayText;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                displayText.rectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint);

            int lineClicked = Mathf.FloorToInt(-localPoint.y / (displayText.fontSize + 4));

            var checkbox = _sectionCheckboxes[sectionId].Find(cb => cb.LineNumber == lineClicked);
            if (checkbox != null)
            {

                var section = _recipeSections.Find(s => s.id == sectionId);
                if (section != null)
                {
                    section.content = NotesMarkdownConverter.UpdateCheckbox(section.content, lineClicked, !checkbox.IsChecked);
                    ApplyState();
                }
            }
        }

        private void ShowColorPicker(Vector2 screenPosition, string sectionId, ColorTarget target)
        {
            var section = _recipeSections.Find(s => s.id == sectionId);
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


        private void ApplyColor(string sectionId)
        {
            var section = _recipeSections.Find(s => s.id == sectionId);
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
            var section = _recipeSections.Find(s => s.id == sectionId);
            if (section == null) return;

            section.isCollapsed = !section.isCollapsed;
            CollapseSection(sectionId);
        }

        private void CollapseSection(string sectionId)
        {
            var section = _recipeSections.Find(s => s.id == sectionId);
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
                sectionUI.ContentField.gameObject.SetActive(false);
                sectionUI.DisplayObject.SetActive(false);
                sectionUI.AddButton.gameObject.SetActive(false);
                sectionUI.DeleteButton.gameObject.SetActive(false);
                sectionUI.CollapsedPreviewObject.SetActive(true);

                string previewText = section.content;
                if (previewText.Length > 15)
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
            }
            else
            {
                sectionUI.CollapsedPreviewObject.SetActive(false);
                ApplyState();
            }
        }
    }
}