using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BeefsRecipes
{
    public class RecipesContextMenu : MonoBehaviour
    {
        private static RecipesContextMenu _instance;
        private static Canvas _cachedCanvas;

        private GameObject _backdropObject;
        private GameObject _menuObject;

        public struct MenuItem
        {
            public string Label;
            public Action OnClick;
            public bool Enabled;
            public bool IsSeparator;
            public Color? TintColor;

            public static MenuItem Action(string label, Action onClick, bool enabled = true, Color? tint = null)
            {
                return new MenuItem { Label = label, OnClick = onClick, Enabled = enabled, TintColor = tint };
            }

            public static MenuItem Separator()
            {
                return new MenuItem { IsSeparator = true };
            }
        }

        private static float MenuWidth => 180f * RecipesUIManager.ScaleFactor;
        private static float ItemHeight => 28f * RecipesUIManager.ScaleFactor;
        private static float SeparatorHeight => 9f * RecipesUIManager.ScaleFactor;
        private static float Padding => 4f * RecipesUIManager.ScaleFactor;
        private static int FontSize => Mathf.RoundToInt(13 * RecipesUIManager.ScaleFactor);

        public static bool IsOpen => _instance != null;

        public static void Show(Vector2 screenPosition, List<MenuItem> items)
        {
            Dismiss();

            if (items == null || items.Count == 0) return;

            _cachedCanvas = BeefsRecipesPlugin.Instance?.UICanvas;
            if (_cachedCanvas == null) return;

            GameObject obj = new GameObject("RecipesContextMenu");
            obj.transform.SetParent(_cachedCanvas.transform, false);
            _instance = obj.AddComponent<RecipesContextMenu>();
            _instance.CreateMenu(screenPosition, items);
        }

        public static void Dismiss()
        {
            if (_instance != null)
            {
                if (_instance._backdropObject != null)
                    Destroy(_instance._backdropObject);
                if (_instance._menuObject != null)
                    Destroy(_instance._menuObject);
                Destroy(_instance.gameObject);
                _instance = null;
            }

            if (_cachedCanvas != null)
            {
                foreach (Transform child in _cachedCanvas.transform)
                {
                    if (child.name == "ContextMenuBackdrop" || child.name == "ContextMenu")
                        Destroy(child.gameObject);
                }
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Dismiss();
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void CreateMenu(Vector2 screenPosition, List<MenuItem> items)
        {
            Canvas canvas = _cachedCanvas;
            if (canvas == null) return;

            float totalHeight = Padding * 2;
            foreach (var item in items)
            {
                totalHeight += item.IsSeparator ? SeparatorHeight : ItemHeight;
            }

            _backdropObject = new GameObject("ContextMenuBackdrop");
            _backdropObject.transform.SetParent(canvas.transform, false);

            RectTransform backdropRect = _backdropObject.AddComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;

            Image backdropImage = _backdropObject.AddComponent<Image>();
            backdropImage.color = new Color(0, 0, 0, 0.01f);
            backdropImage.raycastTarget = true;

            Button backdropButton = _backdropObject.AddComponent<Button>();
            backdropButton.targetGraphic = backdropImage;
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.onClick.AddListener(Dismiss);

            _menuObject = new GameObject("ContextMenu");
            _menuObject.transform.SetParent(canvas.transform, false);

            RectTransform menuRect = _menuObject.AddComponent<RectTransform>();
            menuRect.sizeDelta = new Vector2(MenuWidth, totalHeight);
            menuRect.anchorMin = new Vector2(0, 0);
            menuRect.anchorMax = new Vector2(0, 0);
            menuRect.pivot = new Vector2(0, 1);

            float x = Mathf.Clamp(screenPosition.x, 4f, Screen.width - MenuWidth - 4f);
            float y = Mathf.Clamp(screenPosition.y, totalHeight + 4f, Screen.height - 4f);
            menuRect.anchoredPosition = new Vector2(x, y);

            Image menuBg = _menuObject.AddComponent<Image>();
            menuBg.color = new Color(0.1f, 0.1f, 0.1f, 0.96f);
            menuBg.raycastTarget = true;

            float yPos = -Padding;

            foreach (var item in items)
            {
                if (item.IsSeparator)
                {
                    CreateSeparator(_menuObject.transform, yPos);
                    yPos -= SeparatorHeight;
                }
                else
                {
                    CreateMenuItem(_menuObject.transform, yPos, item);
                    yPos -= ItemHeight;
                }
            }
        }

        private void CreateMenuItem(Transform parent, float yPos, MenuItem item)
        {
            GameObject itemObj = new GameObject($"Item_{item.Label}");
            itemObj.transform.SetParent(parent, false);

            RectTransform itemRect = itemObj.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 1);
            itemRect.anchorMax = new Vector2(1, 1);
            itemRect.pivot = new Vector2(0, 1);
            itemRect.anchoredPosition = new Vector2(0, yPos);
            itemRect.sizeDelta = new Vector2(0, ItemHeight);

            Image itemBg = itemObj.AddComponent<Image>();
            itemBg.raycastTarget = true;

            if (item.Enabled)
            {
                itemBg.color = new Color(0, 0, 0, 0);

                Button btn = itemObj.AddComponent<Button>();
                btn.targetGraphic = itemBg;
                btn.transition = Selectable.Transition.ColorTint;

                Color highlightBase = item.TintColor ?? new Color(1f, 0.39f, 0.08f);
                ColorBlock colors = btn.colors;
                colors.normalColor = new Color(0, 0, 0, 0);
                colors.highlightedColor = new Color(highlightBase.r, highlightBase.g, highlightBase.b, 0.3f);
                colors.pressedColor = new Color(highlightBase.r, highlightBase.g, highlightBase.b, 0.5f);
                colors.fadeDuration = 0.05f;
                btn.colors = colors;

                Action clickAction = item.OnClick;
                btn.onClick.AddListener(() =>
                {
                    Dismiss();
                    clickAction?.Invoke();
                });
            }
            else
            {
                itemBg.color = new Color(0, 0, 0, 0);
            }

            GameObject textObj = new GameObject("Label");
            textObj.transform.SetParent(itemObj.transform, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12, 0);
            textRect.offsetMax = new Vector2(-8, 0);

            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = item.Label;
            text.fontSize = FontSize;
            text.alignment = TextAnchor.MiddleLeft;
            text.raycastTarget = false;

            if (item.Enabled)
            {
                if (item.TintColor.HasValue)
                {
                    Color t = item.TintColor.Value;
                    text.color = new Color(
                        Mathf.Lerp(0.9f, t.r, 0.5f),
                        Mathf.Lerp(0.9f, t.g, 0.5f),
                        Mathf.Lerp(0.9f, t.b, 0.5f), 1f);
                }
                else
                {
                    text.color = new Color(0.9f, 0.9f, 0.9f, 1f);
                }
            }
            else
                text.color = new Color(0.4f, 0.4f, 0.4f, 0.6f);
        }

        private void CreateSeparator(Transform parent, float yPos)
        {
            GameObject sepObj = new GameObject("Separator");
            sepObj.transform.SetParent(parent, false);

            RectTransform sepRect = sepObj.AddComponent<RectTransform>();
            sepRect.anchorMin = new Vector2(0, 1);
            sepRect.anchorMax = new Vector2(1, 1);
            sepRect.pivot = new Vector2(0, 1);
            sepRect.anchoredPosition = new Vector2(0, yPos - SeparatorHeight * 0.5f);
            sepRect.sizeDelta = new Vector2(0, 1);

            GameObject lineObj = new GameObject("Line");
            lineObj.transform.SetParent(sepObj.transform, false);

            RectTransform lineRect = lineObj.AddComponent<RectTransform>();
            lineRect.anchorMin = Vector2.zero;
            lineRect.anchorMax = Vector2.one;
            lineRect.offsetMin = new Vector2(10, 0);
            lineRect.offsetMax = new Vector2(-10, 0);

            Image lineImage = lineObj.AddComponent<Image>();
            lineImage.color = new Color(1f, 1f, 1f, 0.1f);
            lineImage.raycastTarget = false;
        }
    }
}