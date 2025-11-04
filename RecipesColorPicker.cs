using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace BeefsRecipes
{
    public class RecipesColorPicker : MonoBehaviour
    {
        private static RecipesColorPicker _instance;
        private GameObject _menuObject;
        private Action<Color> _onColorSelected;

        private static readonly Color[] PresetColors = new Color[]
        {
            Color.white,
            new Color(1f, 0.35f, 0.35f),
            new Color(1f, 0.6f, 0.2f),
            new Color(1f, 0.9f, 0.2f),
            new Color(0.35f, 1f, 0.35f),
            new Color(0.2f, 1f, 0.8f),
            new Color(0.35f, 0.6f, 1f),
            new Color(0.6f, 0.4f, 1f),
            new Color(1f, 0.4f, 0.8f),
            new Color(1f, 0.7f, 0.3f)
        };

        public static void Show(Vector2 screenPosition, Color currentColor, Action<Color> onColorSelected)
        {
            if (_instance != null)
            {
                Destroy(_instance._menuObject);
                Destroy(_instance.gameObject);
                _instance = null;
            }

            GameObject menuObj = new GameObject("RecipesColorPicker");
            _instance = menuObj.AddComponent<RecipesColorPicker>();
            _instance._onColorSelected = onColorSelected;
            _instance.CreateMenu(screenPosition, currentColor);
        }

        private void CreateMenu(Vector2 screenPosition, Color currentColor)
        {
            Canvas[] canvases = FindObjectsOfType<Canvas>();
            Canvas canvas = null;

            foreach (Canvas c in canvases)
            {
                if (c.gameObject.name == "BeefsRecipesCanvas")
                {
                    canvas = c;
                    break;
                }
            }

            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }

            if (canvas == null) return;

            _menuObject = new GameObject("ColorMenu");
            _menuObject.transform.SetParent(canvas.transform, false);

            RectTransform menuRect = _menuObject.AddComponent<RectTransform>();

            float radius = 80f;
            float menuSize = radius * 2f + 40f;
            menuRect.sizeDelta = new Vector2(menuSize, menuSize);
            menuRect.anchorMin = new Vector2(0, 0);
            menuRect.anchorMax = new Vector2(0, 0);
            menuRect.pivot = new Vector2(0.5f, 0.5f);
            menuRect.anchoredPosition = screenPosition;

            Image bg = _menuObject.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

            CreateColorWheel(_menuObject.transform, currentColor, radius);
        }

        private void CreateColorWheel(Transform parent, Color currentColor, float radius)
        {
            int colorCount = PresetColors.Length;
            float angleStep = 360f / colorCount;

            for (int i = 0; i < colorCount; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector2 position = new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius
                );

                CreateColorButton(parent, PresetColors[i], position, Approximately(PresetColors[i], currentColor));
            }

            CreateColorButton(parent, Color.clear, Vector2.zero, currentColor == Color.clear, "Clear", 50f);
        }

        private void CreateColorButton(Transform parent, Color color, Vector2 position, bool isSelected, string label = "", float size = 35f)
        {
            GameObject buttonObj = new GameObject("ColorButton");
            buttonObj.transform.SetParent(parent, false);

            RectTransform buttonRect = buttonObj.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = position;
            buttonRect.sizeDelta = new Vector2(size, size);

            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = color == Color.clear ? new Color(0.3f, 0.3f, 0.3f) : color;

            if (isSelected)
            {
                GameObject outlineObj = new GameObject("Outline");
                outlineObj.transform.SetParent(buttonObj.transform, false);

                RectTransform outlineRect = outlineObj.AddComponent<RectTransform>();
                outlineRect.anchorMin = Vector2.zero;
                outlineRect.anchorMax = Vector2.one;
                outlineRect.offsetMin = new Vector2(-3, -3);
                outlineRect.offsetMax = new Vector2(3, 3);

                Image outline = outlineObj.AddComponent<Image>();
                outline.color = new Color(1f, 1f, 0f, 0.9f);
            }

            if (!string.IsNullOrEmpty(label))
            {
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(buttonObj.transform, false);

                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;

                Text text = textObj.AddComponent<Text>();
                text.text = label;
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                text.fontSize = 12;
                text.color = Color.white;
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;
            }

            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            button.onClick.AddListener(() => OnColorClicked(color));
        }

        private void OnColorClicked(Color color)
        {
            _onColorSelected?.Invoke(color);
            Hide();
        }

        private static bool Approximately(Color a, Color b, float variance = 0.015f)
        {
            return Mathf.Abs(a.r - b.r) < variance &&
                   Mathf.Abs(a.g - b.g) < variance &&
                   Mathf.Abs(a.b - b.b) < variance &&
                   Mathf.Abs(a.a - b.a) < variance;
        }

        public void Hide()
        {
            if (_menuObject != null)
            {
                Destroy(_menuObject);
            }
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
            _instance = null;
        }

        private void Update()
        {
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
            {
                if (EventSystem.current != null && _menuObject != null)
                {
                    PointerEventData pointerData = new PointerEventData(EventSystem.current)
                    {
                        position = Input.mousePosition
                    };

                    List<RaycastResult> results = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(pointerData, results);

                    bool clickedInMenu = false;
                    foreach (var result in results)
                    {
                        if (result.gameObject == _menuObject || result.gameObject.transform.IsChildOf(_menuObject.transform))
                        {
                            clickedInMenu = true;
                            break;
                        }
                    }

                    if (!clickedInMenu)
                    {
                        Hide();
                    }
                }
            }
        }
    }
}