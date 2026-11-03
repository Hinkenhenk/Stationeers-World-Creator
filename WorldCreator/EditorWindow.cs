using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace WorldCreator
{
    internal static class EditorWindow
    {
        internal static Font? NativeFont { get; set; }
        private static GameObject? _root;
        private static CanvasGroup? _menuCanvasGroup;
        private static bool _menuWasInteractable;
        private static GraphicRaycaster[] _menuRaycasters = new GraphicRaycaster[0];
        private static bool[] _raycastersWereEnabled = new bool[0];
        private static EventSystem? _eventSystem;
        private static bool _navigationWasEnabled;
        private static GameObject? _previousSelection;

        internal static void Toggle(Canvas canvas)
        {
            if (_root != null)
            {
                _root.SetActive(true);
            }
            else if (canvas == null)
            {
                Debug.LogError("[World Creator] Could not find the main-menu canvas.");
                return;
            }
            else
            {
                _root = new GameObject("WorldCreatorEditorWindow");
                _root.transform.SetParent(canvas.transform, false);
                _root.AddComponent<WorldCreatorWindow>();
            }
            _menuCanvasGroup = canvas.GetComponent<CanvasGroup>();
            if (_menuCanvasGroup == null) _menuCanvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            _menuWasInteractable = _menuCanvasGroup.interactable;
            _menuCanvasGroup.interactable = false;
            _menuRaycasters = canvas.GetComponentsInChildren<GraphicRaycaster>(true);
            _raycastersWereEnabled = new bool[_menuRaycasters.Length];
            for (var i = 0; i < _menuRaycasters.Length; i++)
            {
                _raycastersWereEnabled[i] = _menuRaycasters[i].enabled;
                _menuRaycasters[i].enabled = false;
            }
            _eventSystem = EventSystem.current;
            if (_eventSystem != null)
            {
                _navigationWasEnabled = _eventSystem.sendNavigationEvents;
                _previousSelection = _eventSystem.currentSelectedGameObject;
                _eventSystem.sendNavigationEvents = false;
                _eventSystem.SetSelectedGameObject(null);
            }
        }

        internal static void Hide()
        {
            if (_root != null) _root.SetActive(false);
            if (_eventSystem != null)
            {
                _eventSystem.sendNavigationEvents = _navigationWasEnabled;
                if (_previousSelection != null) _eventSystem.SetSelectedGameObject(_previousSelection);
            }
            _eventSystem = null;
            _previousSelection = null;
            if (_menuCanvasGroup != null) _menuCanvasGroup.interactable = _menuWasInteractable;
            for (var i = 0; i < _menuRaycasters.Length; i++)
                if (_menuRaycasters[i] != null) _menuRaycasters[i].enabled = _raycastersWereEnabled[i];
            _menuRaycasters = new GraphicRaycaster[0];
            _raycastersWereEnabled = new bool[0];
        }
    }
}
