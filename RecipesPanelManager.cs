using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Assets.Scripts;

namespace BeefsRecipes
{
    public class RecipesPanelManager
    {
        public enum PanelState
        {
            Hidden,
            Peeking,
            PeekLocked,
            Expanded,
            Fullscreen
        }

        private readonly RecipesUIManager _uiManager;
        private RecipesContentManager _contentManager;

        private PanelState _currentState = PanelState.Hidden;
        private PanelState _targetState = PanelState.Hidden;
        private float _lastPanelClickTime = 0f;
        private const float DoubleClickThreshold = 0.3f;

        public bool IsEditing { get; private set; }
        private float _currentWidth = RecipesUIManager.CollapsedWidth;
        private float _currentHeight = 600f;
        private bool _isHoveringPanel = false;
        public bool IsHoveringPanel => _isHoveringPanel;
        private bool _isHoveringEdge = false;
        private bool _hoverBlockActive = false;

        private bool _hasPendingClick = false;
        private float _pendingClickTime = 0f;
        private System.Action _pendingClickAction = null;

        private bool _isDraggingResize = false;
        private bool _isDraggingTop = false;
        private Vector2 _dragStartMousePos;
        private float _dragStartHeight;

        private bool   _isDraggingSlide = false;
        private float  _slideDragStartMouseY;
        private float  _slideDragStartOffsetY;
        private float  _currentYOffset = 0f;
        private float  _targetYOffset  = 0f;
        private const  float SlideMargin = 20f;

        private RecipesSettingsPanel _settingsPanel;
        public bool IsSettingsOpen => _settingsPanel != null && _settingsPanel.IsOpen;

        private RecipesUserGuide _userGuide;
        public bool IsGuideOpen => _userGuide != null && _userGuide.IsOpen;

        private PanelState _stateBeforeFullscreen = PanelState.PeekLocked;
        private float _heightBeforeFullscreen = 600f;
        private float _yOffsetBeforeFullscreen = 0f;
        private bool _wasEditingBeforeFullscreen = false;
        private bool _enteredFullscreenForGuide = false;

        public float GetYOffset() => _targetYOffset;

        public void SetYOffset(float y)
        {
            _targetYOffset = ClampYOffsetToKeepHandlesVisible(y);
            _currentYOffset = _targetYOffset;
        }

        public PanelState CurrentState => _currentState;
        public bool IsVisible => _targetState != PanelState.Hidden;

        private (float topBound, float bottomBound) GetHandleBounds()
        {
            float halfScreen = Screen.height * 0.5f;
            float topBound    =  halfScreen - SlideMargin;
            float bottomBound = -halfScreen + SlideMargin;
            return (topBound, bottomBound);
        }

        public RecipesPanelManager(RecipesUIManager uiManager)
        {
            this._uiManager = uiManager;
        }

        public void SetContentManager(RecipesContentManager contentManager)
        {
            this._contentManager = contentManager;
        }

        public void SetSettingsPanel(RecipesSettingsPanel settingsPanel)
        {
            _settingsPanel = settingsPanel;
        }

        public void SetUserGuide(RecipesUserGuide userGuide)
        {
            _userGuide = userGuide;
        }

        private void ToggleGuide()
        {
            if (_userGuide == null) return;

            if (_userGuide.IsOpen)
            {
                _userGuide.Hide();
                if (_enteredFullscreenForGuide)
                {
                    _enteredFullscreenForGuide = false;
                    ExitFullscreen();
                }
            }
            else
            {
                _settingsPanel?.Hide();
                if (!IsFullscreen)
                {
                    _enteredFullscreenForGuide = true;
                    EnterFullscreen();
                }
                _userGuide.Show();
            }
        }

        public void CloseGuide()
        {
            if (_userGuide != null && _userGuide.IsOpen)
            {
                _userGuide.Hide();
                if (_enteredFullscreenForGuide)
                {
                    _enteredFullscreenForGuide = false;
                    ExitFullscreen();
                }
            }
        }

        private void ToggleSettings()
        {
            if (_settingsPanel == null) return;
            CloseGuide();

            if (_settingsPanel.IsOpen)
            {
                _settingsPanel.Hide();
            }
            else
            {
                _settingsPanel.Show(_contentManager);
            }
        }

        public void CloseSettings()
        {
            _settingsPanel?.Hide();
        }

        public void OpenSettings()
        {
            if (_settingsPanel == null) return;
            if (!_settingsPanel.IsOpen)
                _settingsPanel.Show(_contentManager);
        }

        public void Update()
        {
            if (IsEditing)
            {
                if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }
            else if (_isHoveringPanel && (_targetState == PanelState.PeekLocked || _targetState == PanelState.Expanded))
            {
                if (Cursor.lockState != CursorLockMode.None || !Cursor.visible)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
            }

            if (!IsEditing)
            {
                bool shouldBlock = _isHoveringPanel &&
                    (_targetState == PanelState.PeekLocked
                     || _targetState == PanelState.Expanded
                     || _targetState == PanelState.Fullscreen);

                if (shouldBlock && !_hoverBlockActive)
                {
                    _hoverBlockActive = true;
                    if (CursorManager.Instance != null)
                        CursorManager.Instance.BlockCursorRaycast = true;
                }
                else if (!shouldBlock && _hoverBlockActive)
                {
                    _hoverBlockActive = false;
                    if (CursorManager.Instance != null)
                        CursorManager.Instance.BlockCursorRaycast = false;
                }
            }

            HandleEscape();
            HandleResizeDragging();
            HandleSlideDragging();
            HandleMouse();
            HandlePendingClick();
            UpdateTransitions();
            UpdateAppearance();
        }

        private void HandleEscape()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (RecipesContextMenu.IsOpen)
                    return;

                if (IsSettingsOpen)
                {
                    CloseSettings();
                    return;
                }

                if (IsGuideOpen)
                {
                    CloseGuide();
                    return;
                }

                if (_currentState == PanelState.Fullscreen || _targetState == PanelState.Fullscreen)
                {
                    if (IsEditing)
                    {
                        SetEditingMode(false);
                    }
                    else
                    {
                        ExitFullscreen();
                    }
                    return;
                }

                if (IsEditing)
                {
                    SetEditingMode(false);
                    SetTargetState(PanelState.PeekLocked);
                }
            }
        }

        private void HandleResizeDragging()
        {
            if (_isDraggingResize)
            {
                if (Input.GetMouseButton(0))
                {
                    Vector2 mouseDelta = (Vector2)Input.mousePosition - _dragStartMousePos;
                    float heightDelta = _isDraggingTop ? mouseDelta.y : -mouseDelta.y;

                    float newHeight = Mathf.Clamp(
                        _dragStartHeight + heightDelta,
                        RecipesUIManager.MinPanelHeight,
                        Screen.height * RecipesUIManager.MaxPanelHeightPercent
                    );

                    _currentHeight = newHeight;
                    _targetYOffset = ClampYOffsetToKeepHandlesVisible(_targetYOffset);
                }
                else
                {
                    _isDraggingResize = false;
                    _uiManager.SetAnyHandleHovered(false);
                }
            }
        }

        private void HandleMouse()
        {
            if (EventSystem.current == null) return;

            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointerData, results);

            bool mouseOverPanel = false;
            bool mouseOverEdge = false;
            bool mouseOverResizeHandle = false;
            bool mouseOverSlide = false;
            bool mouseOverGear = false;
            bool mouseOverFullscreen = false;
            bool mouseOverHelp = false;

            foreach (var result in results)
            {
                if (result.gameObject == _uiManager.EdgeButtonObject)
                {
                    mouseOverEdge = true;
                }
                if (result.gameObject == _uiManager.TopResizeHandle || result.gameObject == _uiManager.BottomResizeHandle)
                {
                    mouseOverResizeHandle = true;
                }
                if (result.gameObject == _uiManager.SlideButtonObject)
                {
                    mouseOverSlide = true;
                }
                if (result.gameObject == _uiManager.GearButtonObject)
                {
                    mouseOverGear = true;
                }
                if (result.gameObject == _uiManager.FullscreenButtonObject)
                {
                    mouseOverFullscreen = true;
                }
                if (result.gameObject == _uiManager.HelpButtonObject)
                {
                    mouseOverHelp = true;
                }

                Transform checkTransform = result.gameObject.transform;
                while (checkTransform != null)
                {
                    if (checkTransform.gameObject == _uiManager.PanelObject)
                    {
                        mouseOverPanel = true;
                    }
                    if (checkTransform.gameObject == _uiManager.EdgeButtonObject)
                    {
                        mouseOverEdge = true;
                    }
                    checkTransform = checkTransform.parent;
                }
            }

            _isHoveringEdge = mouseOverEdge;

            bool hoveringRightEdge = Input.mousePosition.x >= Screen.width - BeefsRecipesPlugin.HoverZoneWidth.Value;
            bool includeEdgeHover = hoveringRightEdge &&
                (_targetState == PanelState.Hidden || _targetState == PanelState.Peeking);
            _isHoveringPanel = mouseOverPanel || mouseOverEdge || mouseOverSlide || mouseOverGear || mouseOverFullscreen || mouseOverHelp || includeEdgeHover;

            if (_currentState == PanelState.Fullscreen || _targetState == PanelState.Fullscreen)
            {
                _uiManager.SetAnyHandleHovered(false);
                if (Input.GetMouseButtonDown(0))
                {
                    HandleClick(mouseOverEdge, mouseOverPanel, mouseOverResizeHandle, hoveringRightEdge, results, mouseOverSlide, mouseOverGear, mouseOverFullscreen, mouseOverHelp);
                }
                return;
            }

            _uiManager.SetAnyHandleHovered(mouseOverResizeHandle && (_targetState == PanelState.Expanded || _targetState == PanelState.PeekLocked));

            if (hoveringRightEdge && _targetState == PanelState.Hidden)
            {
                SetTargetState(PanelState.Peeking);
            }
            else if (!hoveringRightEdge && !mouseOverPanel && _targetState == PanelState.Peeking)
            {
                SetTargetState(PanelState.Hidden);
            }

            if (Input.GetMouseButtonDown(0))
            {
                HandleClick(mouseOverEdge, mouseOverPanel, mouseOverResizeHandle, hoveringRightEdge, results, mouseOverSlide, mouseOverGear, mouseOverFullscreen, mouseOverHelp);
            }
        }

        private void HandlePendingClick()
        {
            if (_hasPendingClick && Time.unscaledTime - _pendingClickTime > DoubleClickThreshold)
            {
                _pendingClickAction?.Invoke();
                _hasPendingClick = false;
                _pendingClickAction = null;
            }
        }

        private void HandleClick(bool mouseOverEdge, bool mouseOverPanel, bool mouseOverResizeHandle, bool hoveringRightEdge, List<RaycastResult> results, bool mouseOverSlide, bool mouseOverGear, bool mouseOverFullscreen, bool mouseOverHelp)
        {
            float timeSinceLastClick = Time.unscaledTime - _lastPanelClickTime;
            bool isDoubleClick = timeSinceLastClick <= DoubleClickThreshold;
            _lastPanelClickTime = Time.unscaledTime;

            if (mouseOverHelp)
            {
                ToggleGuide();
                return;
            }

            if (mouseOverFullscreen)
            {
                ToggleFullscreen();
                return;
            }

            if (mouseOverGear)
            {
                ToggleSettings();
                return;
            }

            if (isDoubleClick && mouseOverPanel && !IsClickingInteractiveElement(results))
            {
                _hasPendingClick = false;
                _pendingClickAction = null;

                ToggleEditMode();
                return;
            }

            if (mouseOverResizeHandle && (_targetState == PanelState.Expanded || _targetState == PanelState.PeekLocked))
            {
                _isDraggingResize = true;
                _isDraggingTop = results[0].gameObject == _uiManager.TopResizeHandle;
                _dragStartMousePos = Input.mousePosition;
                _dragStartHeight = _currentHeight;
                return;
            }

            if (mouseOverEdge)
            {
                if (_targetState == PanelState.Expanded)
                {
                    SetEditingMode(false);
                    SetTargetState(PanelState.PeekLocked);
                }
                else
                {
                    SetTargetState(PanelState.Expanded);
                    SetEditingMode(true);
                }
                return;
            }

            bool clickingRightEdge = Input.mousePosition.x >= Screen.width - BeefsRecipesPlugin.HoverZoneWidth.Value;
            if (clickingRightEdge && !mouseOverEdge)
            {
                if (_targetState == PanelState.Hidden)
                {
                    SetTargetState(PanelState.Peeking);
                }
                else if (_targetState == PanelState.Peeking)
                {
                    SetTargetState(PanelState.PeekLocked);
                }
                else if (_targetState == PanelState.PeekLocked)
                {
                    SetTargetState(PanelState.Hidden);
                }
                return;
            }

            if (mouseOverPanel && _targetState == PanelState.Peeking)
            {
                SetTargetState(PanelState.PeekLocked);
                return;
            }

            if (mouseOverSlide && (_targetState == PanelState.Expanded || _targetState == PanelState.PeekLocked))
            {
                _isDraggingSlide = true;
                _slideDragStartMouseY = ((Vector2)Input.mousePosition).y;
                _slideDragStartOffsetY = _targetYOffset;
                return;
            }

            if (mouseOverPanel && _targetState == PanelState.PeekLocked)
            {
                if (!IsClickingCheckbox(Input.mousePosition, results) && !IsClickingInteractiveElement(results))
                {
                    bool isOverContent = _contentManager?.FindSectionAtScreenPosition(Input.mousePosition) != null;
                    if (!isOverContent)
                    {
                        _hasPendingClick = true;
                        _pendingClickTime = Time.unscaledTime;
                        _pendingClickAction = () => SetTargetState(PanelState.Hidden);
                    }
                }
                return;
            }
        }

        private bool IsClickingInteractiveElement(List<RaycastResult> results)
        {
            foreach (var result in results)
            {
                if (result.gameObject.GetComponent<InputField>() != null)
                    return true;

                var button = result.gameObject.GetComponent<Button>();
                if (button != null && result.gameObject != _uiManager.PanelObject)
                    return true;

                if (result.gameObject == _uiManager.GearButtonObject)
                    return true;

                if (result.gameObject == _uiManager.FullscreenButtonObject)
                    return true;

                if (result.gameObject == _uiManager.HelpButtonObject)
                    return true;

                if (result.gameObject.name == "DisplayText")
                {
                    if (_contentManager != null && _contentManager.IsCheckboxHere(result.gameObject, Input.mousePosition))
                        return true;
                }
            }
            return false;
        }

        private void ToggleEditMode()
        {
            if (_targetState == PanelState.Fullscreen)
            {
                SetEditingMode(!IsEditing);
                _contentManager?.OnStateChanged();
            }
            else if (_targetState == PanelState.Expanded)
            {
                SetEditingMode(false);
                SetTargetState(PanelState.PeekLocked);
            }
            else
            {
                SetTargetState(PanelState.Expanded);
                SetEditingMode(true);
            }
        }

        private void UpdateTransitions()
        {
            float targetWidth = GetTargetWidth();
            _currentYOffset = _targetYOffset;
            _currentWidth = Mathf.Lerp(_currentWidth, targetWidth, Time.unscaledDeltaTime * RecipesUIManager.TransitionSpeed);

            if (_targetState == PanelState.Fullscreen)
            {
                _currentHeight = Screen.height * 0.85f;
                _currentYOffset = 0f;
            }

            if (_uiManager.PanelRect != null)
            {
                _uiManager.PanelRect.sizeDelta = new Vector2(_currentWidth, _currentHeight);
                var pos = _uiManager.PanelRect.anchoredPosition;
                pos.y = _currentYOffset;
                _uiManager.PanelRect.anchoredPosition = pos;
            }

            bool isNowSettled = Mathf.Abs(_currentWidth - targetWidth) <= 1f;

            if (isNowSettled && _currentState != _targetState)
            {
                _currentState = _targetState;
                _contentManager?.OnStateChanged();
            }
        }

        private void UpdateAppearance()
        {
            bool isFullscreen  = _currentState == PanelState.Fullscreen;
            bool isExpanded    = _currentState == PanelState.Expanded || isFullscreen;
            bool isPeekLocked  = _currentState == PanelState.PeekLocked;
            bool isEditing     = IsEditing;
            bool shouldShow    = _isHoveringPanel && _currentState != PanelState.Hidden;

            _uiManager.UpdateChevronAndHandleVisibility(
                shouldShow && !isFullscreen, isExpanded, isPeekLocked, isEditing, _isHoveringEdge);

            _uiManager.SetSidebarControlsVisible(!isFullscreen);

            if (isFullscreen)
            {
                _uiManager.GearButtonObject.SetActive(true);
            }

            if (!isExpanded && !isPeekLocked)
            {
                _uiManager.SetAnyHandleHovered(false);
            }

            float targetAlpha = _currentState switch
            {
                PanelState.Peeking    => 0.7f,
                PanelState.PeekLocked => 0.7f,
                PanelState.Expanded   => 1.0f,
                PanelState.Fullscreen => 1.0f,
                _ => 1.0f
            };
            _uiManager.UpdateBackgroundTransparency(targetAlpha);

            _uiManager.SetFullscreenButtonVisible(
                _currentState != PanelState.Hidden && _currentState != PanelState.Peeking);
            _uiManager.SetHelpButtonVisible(
                _currentState != PanelState.Hidden && _currentState != PanelState.Peeking);
            _uiManager.SetResizeHandlesVisible(
                _currentState != PanelState.Hidden && _currentState != PanelState.Peeking
                && _currentState != PanelState.Fullscreen);
        }

        private float GetTargetWidth()
        {
            return _targetState switch
            {
                PanelState.Hidden => RecipesUIManager.CollapsedWidth,
                PanelState.Peeking => Screen.width * RecipesUIManager.PeekWidthPercent,
                PanelState.PeekLocked => Screen.width * RecipesUIManager.PeekWidthPercent,
                PanelState.Expanded => Screen.width * RecipesUIManager.ExpandedWidthPercent,
                PanelState.Fullscreen => Screen.width * 0.8f,
                _ => RecipesUIManager.CollapsedWidth
            };
        }

        public bool IsFullscreen => _currentState == PanelState.Fullscreen || _targetState == PanelState.Fullscreen;

        public void EnterFullscreen()
        {
            if (IsFullscreen) return;
            CloseSettings();
            CloseGuide();

            _stateBeforeFullscreen = _targetState == PanelState.Hidden ? PanelState.PeekLocked : _targetState;
            _heightBeforeFullscreen = _currentHeight;
            _yOffsetBeforeFullscreen = _targetYOffset;
            _wasEditingBeforeFullscreen = IsEditing;

            _targetYOffset = 0f;
            _currentYOffset = 0f;
            _currentHeight = Screen.height * 0.85f;
            _currentWidth = Screen.width * 0.8f;
            SetTargetState(PanelState.Fullscreen);

            SetEditingMode(true);

            _uiManager.SetFullscreenPanelAnchoring(true);
            _uiManager.ShowFullscreenBackdrop(true);

            _uiManager.SetFullscreenLayout(true);
            _contentManager?.OnFullscreenChanged();
        }

        public void ExitFullscreen()
        {
            if (!IsFullscreen) return;

            CloseSettings();
            _userGuide?.Hide();
            _enteredFullscreenForGuide = false;

            _uiManager.ShowFullscreenBackdrop(false);
            _uiManager.SetFullscreenPanelAnchoring(false);

            _uiManager.SetFullscreenLayout(false);

            _currentHeight = _heightBeforeFullscreen;
            _targetYOffset = _yOffsetBeforeFullscreen;
            _currentYOffset = _yOffsetBeforeFullscreen;

            if (_wasEditingBeforeFullscreen && _stateBeforeFullscreen == PanelState.Expanded)
            {
                SetTargetState(PanelState.Expanded);
            }
            else
            {
                SetEditingMode(false);
                SetTargetState(_stateBeforeFullscreen);
            }

            _contentManager?.OnFullscreenChanged();
        }

        public void ToggleFullscreen()
        {
            if (IsFullscreen)
                ExitFullscreen();
            else
                EnterFullscreen();
        }

        public void SetTargetState(PanelState newState)
        {
            if (_targetState == newState) return;

            _targetState = newState;

            if (newState == PanelState.Hidden || newState == PanelState.Peeking)
            {
                CloseSettings();
                CloseGuide();
            }
        }

        public void SetEditingMode(bool isEditing)
        {
            IsEditing = isEditing;

            _hoverBlockActive = false;

            if (isEditing)
            {
                KeyManager.SetInputState("BeefsRecipesPanel_Typing", KeyInputState.Typing);

                if (CursorManager.Instance != null)
                {
                    CursorManager.Instance.BlockCursorRaycast = true;
                }

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                KeyManager.RemoveInputState("BeefsRecipesPanel_Typing");

                if (CursorManager.Instance != null)
                {
                    CursorManager.Instance.BlockCursorRaycast = false;
                }

                if (!WorldManager.IsGamePaused)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }

                _contentManager?.OnEditModeExited();
            }

            _contentManager?.UpdateSectionInputs();
        }

        public bool IsPeekMode()
        {
            if (_targetState == PanelState.Fullscreen)
                return !IsEditing;
            return _targetState == PanelState.Peeking || _targetState == PanelState.PeekLocked;
        }

        public bool IsPeekLocked()
        {
            return _targetState == PanelState.PeekLocked;
        }

        public bool IsExpandedMode()
        {
            if (_targetState == PanelState.Fullscreen)
                return IsEditing;
            return _targetState == PanelState.Expanded;
        }

        public bool IsTransitioning()
        {
            float widthPercent = _currentWidth / Screen.width;
            return widthPercent < RecipesUIManager.TextHideWidthThreshold;
        }

        public float GetCurrentWidthPercent()
        {
            return _currentWidth / Screen.width;
        }

        public void ResetToHidden()
        {
            if (IsFullscreen)
            {
                _uiManager.ShowFullscreenBackdrop(false);
                _uiManager.SetFullscreenPanelAnchoring(false);
            }
            SetEditingMode(false);
            SetTargetState(PanelState.Hidden);
        }

        private bool IsClickingCheckbox(Vector2 mousePosition, List<RaycastResult> results)
        {
            if (_contentManager == null) return false;

            foreach (var result in results)
            {
                if (result.gameObject.name == "DisplayText")
                {
                    return _contentManager.IsCheckboxHere(result.gameObject, mousePosition);
                }
            }
            return false;
        }

        public float GetPanelHeight()
        {
            return _currentHeight;
        }

        public void SetPanelHeight(float height)
        {
            _currentHeight = Mathf.Clamp(
                height,
                RecipesUIManager.MinPanelHeight,
                Screen.height * RecipesUIManager.MaxPanelHeightPercent
            );
        }

        public string GetSavedPanelMode()
        {
            if (_targetState == PanelState.Fullscreen || _targetState == PanelState.Expanded)
            {
                return "PeekLocked";
            }
            else if (_targetState == PanelState.PeekLocked || _targetState == PanelState.Peeking)
            {
                return "PeekLocked";
            }
            else
            {
                return "Hidden";
            }
        }

        public void RestoreSavedPanelMode(string mode)
        {
            if (mode == "PeekLocked")
            {
                SetTargetState(PanelState.PeekLocked);
                _currentWidth = GetTargetWidth();
                if (_uiManager.PanelRect != null)
                {
                    _uiManager.PanelRect.sizeDelta = new Vector2(_currentWidth, _currentHeight);
                }
            }
            else
            {
                SetTargetState(PanelState.Hidden);
                _currentWidth = RecipesUIManager.CollapsedWidth;
                if (_uiManager.PanelRect != null)
                {
                    _uiManager.PanelRect.sizeDelta = new Vector2(_currentWidth, _currentHeight);
                }
            }
        }

        private float ClampYOffsetToKeepHandlesVisible(float desiredCenterY)
        {
            var (topBound, bottomBound) = GetHandleBounds();
            float halfPanel = _currentHeight * 0.5f;

            float topOfTopHandle       = desiredCenterY + halfPanel;
            float bottomOfBottomHandle = desiredCenterY - halfPanel;

            if (topOfTopHandle > topBound)
                desiredCenterY -= (topOfTopHandle - topBound);

            if (bottomOfBottomHandle < bottomBound)
                desiredCenterY += (bottomBound - bottomOfBottomHandle);

            return desiredCenterY;
        }

        private void HandleSlideDragging()
        {
            if (_isDraggingSlide)
            {
                if (Input.GetMouseButton(0))
                {
                    float mouseDeltaY = ((Vector2)Input.mousePosition).y - _slideDragStartMouseY;
                    float desired = _slideDragStartOffsetY + mouseDeltaY;
                    _targetYOffset = ClampYOffsetToKeepHandlesVisible(desired);
                }
                else
                {
                    _isDraggingSlide = false;
                }
            }
        }

        public void Cleanup()
        {
            if (_hoverBlockActive)
            {
                _hoverBlockActive = false;
                if (CursorManager.Instance != null)
                    CursorManager.Instance.BlockCursorRaycast = false;
            }

            if (IsEditing)
            {
                KeyManager.RemoveInputState("BeefsRecipesPanel_Typing");

                if (CursorManager.Instance != null)
                {
                    CursorManager.Instance.BlockCursorRaycast = false;
                }

                if (!WorldManager.IsGamePaused)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }
    }
}