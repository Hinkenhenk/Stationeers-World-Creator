using UnityEngine;
using UnityEngine.UI;

namespace WorldCreator;

internal sealed class MainMenuBannerLayout : MonoBehaviour
{
    private GridLayoutGroup _grid = null!;
    private RectTransform _smallButtons = null!;
    private RectTransform _parent = null!;
    private RectTransform _viewport = null!;
    private RectTransform _content = null!;
    private ScrollRect _scroll = null!;
    private readonly Vector3[] _corners = new Vector3[4];
    private float _topInset;
    private float _preferredHeight;
    private int _rows = -1;
    private Vector2 _lastSize;

    /// <summary>Preserves the native banner list inside a clipped, vertically scrollable viewport.</summary>
    internal void Initialize(GridLayoutGroup grid, RectTransform smallButtons)
    {
        _grid = grid;
        _content = (RectTransform)grid.transform;
        _smallButtons = smallButtons;
        _parent = (RectTransform)_content.parent;
        _preferredHeight = grid.cellSize.y;
        _content.GetWorldCorners(_corners);
        var topLeft = _parent.InverseTransformPoint(_corners[1]);
        var bottomRight = _parent.InverseTransformPoint(_corners[3]);
        _topInset = _parent.rect.yMax - topLeft.y;
        var viewportObject = new GameObject("WorldCreatorBannerViewport", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
        viewportObject.layer = gameObject.layer;
        _viewport = (RectTransform)viewportObject.transform;
        _viewport.SetParent(_parent, false);
        _viewport.SetSiblingIndex(_content.GetSiblingIndex());
        _viewport.anchorMin = new Vector2(0, 1);
        _viewport.anchorMax = Vector2.one;
        _viewport.pivot = new Vector2(0, 1);
        _viewport.offsetMin = new Vector2(topLeft.x - _parent.rect.xMin, -_topInset - _content.rect.height);
        _viewport.offsetMax = new Vector2(bottomRight.x - _parent.rect.xMax, -_topInset);
        _content.SetParent(_viewport, false);
        _content.anchorMin = new Vector2(0, 1);
        _content.anchorMax = Vector2.one;
        _content.pivot = new Vector2(0.5f, 1);
        _content.anchoredPosition = Vector2.zero;
        _content.sizeDelta = new Vector2(0, _content.rect.height);
        _scroll = viewportObject.GetComponent<ScrollRect>();
        _scroll.viewport = _viewport;
        _scroll.content = _content;
        _scroll.horizontal = false;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = 40;
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 1;
    }

    /// <summary>Reflows after resolution changes, Continue appearing or another mod adding menu buttons.</summary>
    private void LateUpdate()
    {
        if (_grid == null || _smallButtons == null || _viewport == null) return;
        _smallButtons.GetWorldCorners(_corners);
        var bottom = _parent.InverseTransformPoint(_corners[1]).y + 20;
        var available = Mathf.Max(40, _parent.rect.yMax - _topInset - bottom);
        var rows = 0;
        foreach (Transform child in _content)
        {
            var element = child.GetComponent<LayoutElement>();
            if (child.gameObject.activeSelf && (element == null || !element.ignoreLayout)) rows++;
        }
        var size = new Vector2(_viewport.rect.width, available);
        if (_rows == rows && (_lastSize - size).sqrMagnitude < 0.01f) return;
        _rows = rows;
        _lastSize = size;
        var layout = MenuBannerSizing.Fit(available, _preferredHeight, rows, _grid.padding.vertical, _grid.spacing.y);
        _viewport.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, available);
        _grid.cellSize = new Vector2(size.x - _grid.padding.horizontal, (float)layout.RowHeight);
        _content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, (float)layout.ContentHeight);
        _scroll.vertical = layout.ContentHeight > available + 0.5f;
        if (!_scroll.vertical) _content.anchoredPosition = Vector2.zero;
        LayoutRebuilder.MarkLayoutForRebuild(_content);
    }
}
