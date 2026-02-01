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
            Expanded
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
        private bool _isHoveringEdge = false;

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

        public float GetYOffset() => _targetYOffset;

        public void SetYOffset(float y)
        {
            _targetYOffset = ClampYOffsetToKeepHandlesVisible(y);
            _currentYOffset = _targetYOffset;
        }

        public PanelState CurrentState => _currentState;

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
            if (IsEditing && Input.GetKeyDown(KeyCode.Escape))
            {
                SetEditingMode(false);
                SetTargetState(PanelState.PeekLocked);
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
            _isHoveringPanel = mouseOverPanel || mouseOverEdge || mouseOverSlide || hoveringRightEdge;

            _uiManager.SetAnyHandleHovered(mouseOverResizeHandle && _currentState == PanelState.Expanded);

            if (hoveringRightEdge && _currentState == PanelState.Hidden)
            {
                SetTargetState(PanelState.Peeking);
            }
            else if (!hoveringRightEdge && !mouseOverPanel && _currentState == PanelState.Peeking)
            {
                SetTargetState(PanelState.Hidden);
            }

            if (Input.GetMouseButtonDown(0))
            {
                HandleClick(mouseOverEdge, mouseOverPanel, mouseOverResizeHandle, hoveringRightEdge, results, mouseOverSlide);
            }
        }

        private void HandlePendingClick()
        {
            if (_hasPendingClick && Time.time - _pendingClickTime > DoubleClickThreshold)
            {
                _pendingClickAction?.Invoke();
                _hasPendingClick = false;
                _pendingClickAction = null;
            }
        }

        private void HandleClick(bool mouseOverEdge, bool mouseOverPanel, bool mouseOverResizeHandle, bool hoveringRightEdge, List<RaycastResult> results, bool mouseOverSlide)
        {
            float timeSinceLastClick = Time.time - _lastPanelClickTime;
            bool isDoubleClick = timeSinceLastClick <= DoubleClickThreshold;
            _lastPanelClickTime = Time.time;

            if (isDoubleClick && mouseOverPanel && !IsClickingInteractiveElement(results))
            {
                _hasPendingClick = false;
                _pendingClickAction = null;

                ToggleEditMode();
                return;
            }

            if (mouseOverResizeHandle && _currentState == PanelState.Expanded)
            {
                _isDraggingResize = true;
                _isDraggingTop = results[0].gameObject == _uiManager.TopResizeHandle;
                _dragStartMousePos = Input.mousePosition;
                _dragStartHeight = _currentHeight;
                return;
            }

            if (mouseOverEdge)
            {
                if (_currentState == PanelState.Expanded)
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
                if (_currentState == PanelState.Hidden)
                {
                    SetTargetState(PanelState.Peeking);
                }
                else if (_currentState == PanelState.Peeking)
                {
                    SetTargetState(PanelState.PeekLocked);
                }
                else if (_currentState == PanelState.PeekLocked)
                {
                    SetTargetState(PanelState.Hidden);
                }
                return;
            }

            if (mouseOverPanel && _currentState == PanelState.Peeking)
            {
                SetTargetState(PanelState.PeekLocked);
                return;
            }

            if (mouseOverPanel && _currentState == PanelState.PeekLocked)
            {
                if (!IsClickingCheckbox(Input.mousePosition, results) && !IsClickingInteractiveElement(results))
                {
                    _hasPendingClick = true;
                    _pendingClickTime = Time.time;
                    _pendingClickAction = () => SetTargetState(PanelState.Hidden);
                }
                return;
            }

            if (mouseOverSlide && _currentState == PanelState.Expanded && IsEditing)
            {
                _isDraggingSlide = true;
                _slideDragStartMouseY = ((Vector2)Input.mousePosition).y;
                _slideDragStartOffsetY = _targetYOffset;
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
            if (_currentState == PanelState.Expanded)
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
            float oldWidth = _currentWidth;
            float oldY = _currentYOffset;
            _currentYOffset = _targetYOffset;
            _currentWidth = Mathf.Lerp(_currentWidth, targetWidth, Time.deltaTime * RecipesUIManager.TransitionSpeed);

            if (_uiManager.PanelRect != null)
            {
                _uiManager.PanelRect.sizeDelta = new Vector2(_currentWidth, _currentHeight);
                var pos = _uiManager.PanelRect.anchoredPosition;
                pos.y = _currentYOffset;
                _uiManager.PanelRect.anchoredPosition = pos;
            }

            bool wasTransitioning = Mathf.Abs(oldWidth - targetWidth) > 1f;
            bool isNowSettled = Mathf.Abs(_currentWidth - targetWidth) <= 1f;

            if (wasTransitioning && isNowSettled)
            {
                _contentManager?.OnStateChanged();
            }
        }

        private void UpdateAppearance()
        {
            bool isExpanded    = _currentState == PanelState.Expanded;
            bool isPeekLocked  = _currentState == PanelState.PeekLocked;
            bool isEditing     = IsEditing;
            bool shouldShow    = _isHoveringPanel && _currentState != PanelState.Hidden;

            _uiManager.UpdateChevronAndHandleVisibility(shouldShow, isExpanded, isPeekLocked, isEditing, _isHoveringEdge);

            if (!isExpanded)
            {
                _uiManager.SetAnyHandleHovered(false);
            }

            float targetAlpha = _currentState switch
            {
                PanelState.Peeking    => 0.7f,
                PanelState.PeekLocked => 0.7f,
                PanelState.Expanded   => 1.0f,
                _ => 1.0f
            };
            _uiManager.UpdateBackgroundTransparency(targetAlpha);
        }

        private float GetTargetWidth()
        {
            return _targetState switch
            {
                PanelState.Hidden => RecipesUIManager.CollapsedWidth,
                PanelState.Peeking => Screen.width * RecipesUIManager.PeekWidthPercent,
                PanelState.PeekLocked => Screen.width * RecipesUIManager.PeekWidthPercent,
                PanelState.Expanded => Screen.width * RecipesUIManager.ExpandedWidthPercent,
                _ => RecipesUIManager.CollapsedWidth
            };
        }

        public void SetTargetState(PanelState newState)
        {
            if (_targetState == newState) return;

            _targetState = newState;
            _currentState = newState;

            _contentManager?.OnStateChanged();
        }

        public void SetEditingMode(bool isEditing)
        {
            IsEditing = isEditing;

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

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            _contentManager?.UpdateSectionInputs();
        }

        public bool IsPeekMode()
        {
            return _currentState == PanelState.Peeking || _currentState == PanelState.PeekLocked;
        }

        public bool IsExpandedMode()
        {
            return _currentState == PanelState.Expanded;
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
            SetEditingMode(false);
            SetTargetState(PanelState.Hidden);
        }

        private bool IsClickingCheckbox(Vector2 mousePosition, List<RaycastResult> results)
        {
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
            if (_currentState == PanelState.Expanded)
            {
                return "PeekLocked";
            }
            else if (_currentState == PanelState.PeekLocked || _currentState == PanelState.Peeking)
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
            if (IsEditing)
            {
                KeyManager.RemoveInputState("BeefsRecipesPanel_Typing");

                if (CursorManager.Instance != null)
                {
                    CursorManager.Instance.BlockCursorRaycast = false;
                }

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}