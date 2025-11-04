using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BeefsRecipes
{
    public class RecipesDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public string itemId;
        public Action<string, Vector2> OnDragStart;
        public Action<string, Vector2> onDrag;
        public Action<string, Vector2> OnDragEnd;
        public Action<string> OnDoubleClick;

        private Image _image;
        private readonly Color _normalColor = new Color(0.4f, 0.6f, 0.8f, 0.4f);
        private readonly Color _hoverColor = new Color(0.5f, 0.7f, 0.9f, 0.7f);
        private readonly Color _collapsedColor = new Color(0.8f, 0.4f, 0.4f, 0.5f);
        private bool _isDragging = false;
        private bool _isCollapsed = false;

        private float _lastClickTime = 0f;
        private const float DoubleClickThreshold = 0.3f;

        private void Awake()
        {
            _image = GetComponent<Image>();
            if (_image != null)
            {
                _image.color = _normalColor;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_image != null && !_isDragging)
            {
                _image.color = _hoverColor;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_image != null && !_isDragging)
            {
                _image.color = _isCollapsed ? _collapsedColor : _normalColor;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            float timeSinceLastClick = Time.time - _lastClickTime;

            if (timeSinceLastClick <= DoubleClickThreshold)
            {
                OnDoubleClick?.Invoke(itemId);
            }

            _lastClickTime = Time.time;
        }

        public void SetCollapsed(bool collapsed)
        {
            _isCollapsed = collapsed;
            if (_image != null)
            {
                _image.color = collapsed ? _collapsedColor : _normalColor;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
            if (_image != null)
            {
                _image.color = _hoverColor;
            }
            OnDragStart?.Invoke(itemId, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            onDrag?.Invoke(itemId, eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
            if (_image != null)
            {
                _image.color = _normalColor;
            }
            OnDragEnd?.Invoke(itemId, eventData.position);
        }

        public static GameObject Create(Transform parent, string itemId, Action<string, Vector2> onDragStart, Action<string, Vector2> onDrag, Action<string, Vector2> onDragEnd, Action<string> onDoubleClick)
        {
            GameObject dragHandleObj = new GameObject("RecipesDragHandle");
            dragHandleObj.transform.SetParent(parent, false);

            RectTransform dragHandleRect = dragHandleObj.AddComponent<RectTransform>();
            dragHandleRect.anchorMin = new Vector2(0, 0);
            dragHandleRect.anchorMax = new Vector2(0, 1);
            dragHandleRect.pivot = new Vector2(1, 0.5f);
            dragHandleRect.anchoredPosition = new Vector2(-5, 0);
            dragHandleRect.sizeDelta = new Vector2(5, 0);

            LayoutElement layoutIgnore = dragHandleObj.AddComponent<LayoutElement>();
            layoutIgnore.ignoreLayout = true;

            Image dragHandleImage = dragHandleObj.AddComponent<Image>();
            dragHandleImage.color = new Color(0.4f, 0.6f, 0.8f, 0.4f);
            dragHandleImage.raycastTarget = true;

            RecipesDragHandle recipesDragHandler = dragHandleObj.AddComponent<RecipesDragHandle>();
            recipesDragHandler.itemId = itemId;
            recipesDragHandler.OnDragStart = onDragStart;
            recipesDragHandler.onDrag = onDrag;
            recipesDragHandler.OnDragEnd = onDragEnd;
            recipesDragHandler.OnDoubleClick = onDoubleClick;

            dragHandleObj.SetActive(false);

            return dragHandleObj;
        }
    }
}