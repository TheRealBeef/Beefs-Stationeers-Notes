using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BeefsRecipes
{
    public class RecipesSketchPad : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {

        private Texture2D _texture;
        private RawImage _rawImage;
        private RectTransform _rectTransform;
        private int _width;
        private int _height;

        private readonly List<BeefsRecipesPlugin.Stroke> _strokes = new List<BeefsRecipesPlugin.Stroke>();
        private BeefsRecipesPlugin.Stroke _currentStroke;
        private bool _isDirty;
        private bool _needsApply;

        private Color _brushColor = Color.white;
        private float _brushSize = 3f;
        private bool _isEraser;

        private int _cachedStampRadius = -1;
        private List<Vector2Int> _stampOffsets;

        private Color32[] _basePixels;

        private GameObject _cursorObject;
        private RectTransform _cursorRect;
        private Image _cursorImage;
        private static Sprite _cursorSprite;

        public bool InputEnabled { get; set; }

        public event Action OnStrokeCountChanged;

        public bool HasStrokes => _strokes.Count > 0;
        public bool IsDirty => _isDirty;

        public void Initialize(int width, int height)
        {
            _width = width;
            _height = height;

            _rawImage = GetComponent<RawImage>();
            _rectTransform = GetComponent<RectTransform>();

            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            _texture.filterMode = FilterMode.Bilinear;
            _texture.wrapMode = TextureWrapMode.Clamp;

            ClearTexture();
            _texture.Apply();

            _rawImage.texture = _texture;
            _strokes.Clear();
            _isDirty = false;
            _basePixels = null;
        }

        public void LoadFromPng(string base64)
        {
            if (string.IsNullOrEmpty(base64)) return;

            _rawImage = GetComponent<RawImage>();
            _rectTransform = GetComponent<RectTransform>();

            try
            {
                byte[] pngData = Convert.FromBase64String(base64);

                _texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                _texture.filterMode = FilterMode.Bilinear;
                _texture.wrapMode = TextureWrapMode.Clamp;

                if (_texture.LoadImage(pngData))
                {
                    _width = _texture.width;
                    _height = _texture.height;
                    _rawImage.texture = _texture;
                    _basePixels = _texture.GetPixels32();
                }
                else
                {
                    BeefsRecipesPlugin.Log.LogWarning("Failed to decode drawing PNG, creating blank canvas");
                    Initialize(_width > 0 ? _width : 512, _height > 0 ? _height : 256);
                }
            }
            catch (Exception ex)
            {
                BeefsRecipesPlugin.Log.LogError($"Error loading drawing PNG: {ex.Message}");
                Initialize(_width > 0 ? _width : 512, _height > 0 ? _height : 256);
            }

            _strokes.Clear();
            _isDirty = false;
        }

        public string SaveToPng()
        {
            if (_texture == null) return null;

            byte[] pngData = _texture.EncodeToPNG();
            string base64 = Convert.ToBase64String(pngData);

            _basePixels = _texture.GetPixels32();
            _strokes.Clear();
            _isDirty = false;
            OnStrokeCountChanged?.Invoke();

            return base64;
        }

        public string SnapshotToPng()
        {
            if (_texture == null) return null;

            byte[] pngData = _texture.EncodeToPNG();
            return Convert.ToBase64String(pngData);
        }

        public void SetBrushColor(Color color)
        {
            _brushColor = color;
        }

        public Color GetBrushColor() => _brushColor;

        public void SetBrushSize(float size)
        {
            _brushSize = Mathf.Clamp(size, 1f, 30f);
        }

        public float GetBrushSize() => _brushSize;

        public void SetEraser(bool enabled)
        {
            _isEraser = enabled;
        }

        public bool IsEraser => _isEraser;

        public void Undo()
        {
            if (_strokes.Count == 0) return;

            _strokes.RemoveAt(_strokes.Count - 1);
            RedrawAllStrokes();
            _isDirty = _strokes.Count > 0;
            OnStrokeCountChanged?.Invoke();
        }

        public void ClearAll()
        {
            _strokes.Clear();
            _basePixels = null;
            ClearTexture();
            _texture.Apply();
            _isDirty = false;
            OnStrokeCountChanged?.Invoke();
        }

        public void Revert()
        {
            _strokes.Clear();
            RestoreBaseOrClear();
            _texture.Apply();
            _isDirty = false;
            OnStrokeCountChanged?.Invoke();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!InputEnabled) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            Vector2 texCoord;
            if (!ScreenToTextureCoord(eventData.position, out texCoord)) return;

            _currentStroke = new BeefsRecipesPlugin.Stroke
            {
                points = new List<float> { texCoord.x, texCoord.y },
                colorHex = _isEraser ? "" : "#" + ColorUtility.ToHtmlStringRGBA(_brushColor),
                brushSize = _brushSize,
                isEraser = _isEraser
            };

            StampCircle(texCoord, GetStampColor(), _brushSize);
            _needsApply = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!InputEnabled || _currentStroke == null) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            Vector2 texCoord;
            if (!ScreenToTextureCoord(eventData.position, out texCoord)) return;

            int count = _currentStroke.points.Count;
            Vector2 prev = new Vector2(_currentStroke.points[count - 2], _currentStroke.points[count - 1]);

            _currentStroke.points.Add(texCoord.x);
            _currentStroke.points.Add(texCoord.y);

            DrawLineSegment(prev, texCoord, GetStampColor(), _brushSize);
            _needsApply = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_currentStroke == null) return;
            if (eventData.button != PointerEventData.InputButton.Left) return;

            if (_currentStroke.points.Count >= 2)
            {
                _strokes.Add(_currentStroke);
                _isDirty = true;
                OnStrokeCountChanged?.Invoke();
            }

            _currentStroke = null;
        }

        public void SetupBrushCursor()
        {
            if (_cursorObject != null) return;

            _cursorObject = new GameObject("BrushCursor");
            _cursorObject.transform.SetParent(transform, false);

            _cursorRect = _cursorObject.AddComponent<RectTransform>();
            _cursorRect.anchorMin = new Vector2(0, 0);
            _cursorRect.anchorMax = new Vector2(0, 0);
            _cursorRect.pivot = new Vector2(0.5f, 0.5f);

            _cursorImage = _cursorObject.AddComponent<Image>();
            _cursorImage.sprite = CreateCursorRingSprite();
            _cursorImage.color = Color.white;
            _cursorImage.raycastTarget = false;

            _cursorObject.SetActive(false);
        }

        private void UpdateBrushCursor()
        {
            if (_cursorObject == null || _rectTransform == null) return;

            if (!InputEnabled)
            {
                _cursorObject.SetActive(false);
                return;
            }

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rectTransform, Input.mousePosition, null, out localPoint))
            {
                _cursorObject.SetActive(false);
                return;
            }

            Rect rect = _rectTransform.rect;
            float u = (localPoint.x - rect.x) / rect.width;
            float v = (localPoint.y - rect.y) / rect.height;

            if (u < 0f || u > 1f || v < 0f || v > 1f)
            {
                _cursorObject.SetActive(false);
                return;
            }

            _cursorObject.SetActive(true);

            float pixelRadius = _brushSize;
            float screenDiameter = (pixelRadius * 2f / _width) * rect.width;
            screenDiameter = Mathf.Max(4f, screenDiameter);

            _cursorRect.sizeDelta = new Vector2(screenDiameter, screenDiameter);

            _cursorRect.anchoredPosition = new Vector2(
                localPoint.x - rect.x,
                localPoint.y - rect.y);

            Color ringColor = _isEraser
                ? new Color(1f, 0.4f, 0.4f, 0.7f)
                : new Color(_brushColor.r, _brushColor.g, _brushColor.b, 0.7f);
            _cursorImage.color = ringColor;
        }

        private static Sprite CreateCursorRingSprite()
        {
            if (_cursorSprite != null) return _cursorSprite;

            const int size = 64;
            float center = size / 2f;
            float outerR = size * 0.48f;
            float innerR = size * 0.38f;

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

                    float alpha = 0f;
                    if (dist >= innerR && dist <= outerR)
                    {
                        float edgeOuter = Mathf.Clamp01((outerR - dist) * 2f);
                        float edgeInner = Mathf.Clamp01((dist - innerR) * 2f);
                        alpha = Mathf.Min(edgeOuter, edgeInner);
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            _cursorSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 100f);
            return _cursorSprite;
        }

        private void LateUpdate()
        {
            if (_needsApply && _texture != null)
            {
                _texture.Apply();
                _needsApply = false;
            }
            UpdateBrushCursor();
        }

        private void OnDestroy()
        {
            if (_texture != null)
            {
                Destroy(_texture);
                _texture = null;
            }
        }

        private Color GetStampColor()
        {
            return _isEraser ? Color.clear : _brushColor;
        }

        private void StampCircle(Vector2 center, Color color, float radius)
        {
            EnsureStampCache(radius);

            int cx = Mathf.RoundToInt(center.x);
            int cy = Mathf.RoundToInt(center.y);

            foreach (var offset in _stampOffsets)
            {
                int px = cx + offset.x;
                int py = cy + offset.y;

                if (px >= 0 && px < _width && py >= 0 && py < _height)
                {
                    _texture.SetPixel(px, py, color);
                }
            }
        }

        private void DrawLineSegment(Vector2 from, Vector2 to, Color color, float radius)
        {
            float dist = Vector2.Distance(from, to);
            if (dist < 0.5f)
            {
                StampCircle(to, color, radius);
                return;
            }

            float step = Mathf.Max(0.5f, Mathf.Min(1f, radius * 0.3f));
            int steps = Mathf.CeilToInt(dist / step);

            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector2 point = Vector2.Lerp(from, to, t);
                StampCircle(point, color, radius);
            }
        }

        private void EnsureStampCache(float radius)
        {
            int r = Mathf.CeilToInt(radius);
            if (r == _cachedStampRadius && _stampOffsets != null) return;

            _cachedStampRadius = r;
            _stampOffsets = new List<Vector2Int>();

            float rSq = radius * radius;
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    if (dx * dx + dy * dy <= rSq)
                    {
                        _stampOffsets.Add(new Vector2Int(dx, dy));
                    }
                }
            }
        }

        private void RedrawAllStrokes()
        {
            RestoreBaseOrClear();

            foreach (var stroke in _strokes)
            {
                Color color;
                if (stroke.isEraser)
                {
                    color = Color.clear;
                }
                else
                {
                    if (!ColorUtility.TryParseHtmlString(stroke.colorHex, out color))
                        color = Color.white;
                }

                if (stroke.points.Count < 2) continue;

                Vector2 prev = new Vector2(stroke.points[0], stroke.points[1]);
                StampCircle(prev, color, stroke.brushSize);

                for (int i = 2; i < stroke.points.Count; i += 2)
                {
                    Vector2 current = new Vector2(stroke.points[i], stroke.points[i + 1]);
                    DrawLineSegment(prev, current, color, stroke.brushSize);
                    prev = current;
                }
            }

            _texture.Apply();
        }

        private void ClearTexture()
        {
            if (_texture == null) return;

            Color32[] clearPixels = new Color32[_width * _height];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < clearPixels.Length; i++)
                clearPixels[i] = clear;

            _texture.SetPixels32(clearPixels);
        }

        private void RestoreBaseOrClear()
        {
            if (_texture == null) return;

            if (_basePixels != null && _basePixels.Length == _width * _height)
            {
                _texture.SetPixels32(_basePixels);
            }
            else
            {
                ClearTexture();
            }
        }

        private bool ScreenToTextureCoord(Vector2 screenPos, out Vector2 texCoord)
        {
            texCoord = Vector2.zero;

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _rectTransform, screenPos, null, out localPoint))
                return false;

            Rect rect = _rectTransform.rect;

            float u = (localPoint.x - rect.x) / rect.width;
            float v = (localPoint.y - rect.y) / rect.height;

            if (u < 0f || u > 1f || v < 0f || v > 1f)
                return false;

            texCoord = new Vector2(u * _width, v * _height);
            return true;
        }
    }

    public class DrawingHeightClamp : MonoBehaviour
    {
        public float maxScreenFraction = 0.3f;
        public float aspectRatio = 16f / 9f;

        public float activeMaxFraction = 0f;

        private RectTransform _rt;
        private LayoutElement _le;

        private void LateUpdate()
        {
            if (_rt == null)
            {
                _rt = (RectTransform)transform;
                _le = GetComponent<LayoutElement>();
            }

            float width = _rt.rect.width;
            if (width <= 0f) return;

            float fraction = activeMaxFraction > 0f ? activeMaxFraction : maxScreenFraction;
            float idealH = width / aspectRatio;
            float maxH = Screen.height * fraction;
            float h = Mathf.Min(idealH, maxH);

            _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);

            if (_le != null)
                _le.preferredHeight = h;
        }
    }
}