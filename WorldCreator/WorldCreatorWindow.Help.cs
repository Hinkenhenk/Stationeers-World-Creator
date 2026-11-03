using UnityEngine;

namespace WorldCreator
{
    public sealed partial class WorldCreatorWindow
    {
        private bool _helpOpen;
        private bool _pendingHelpClose;
        private int _helpPage;
        private int _helpTopic;
        private int? _pendingHelpTopic;
        private Vector2 _helpScroll;
        private GUIStyle? _helpBodyStyle;

        private void ApplyHelpNavigation()
        {
            // Change the help tree only on Layout, keeping IMGUI's event passes identical.
            if (_pendingHelpClose)
            {
                _helpOpen = false;
                _pendingHelpClose = false;
            }
            if (!_pendingHelpTopic.HasValue) return;
            if (!_helpOpen) _helpPage = _page;
            _helpTopic = _pendingHelpTopic.Value;
            _pendingHelpTopic = null;
            _helpScroll = Vector2.zero;
            _helpOpen = true;
            CloseNativePopup();
            GUI.FocusControl(null);
        }

        private void DrawHelpOverlay()
        {
            var width = Mathf.Min(1100f, Screen.width - 32f);
            var height = Mathf.Min(1000f, Screen.height - 32f);
            var bounds = new Rect((Screen.width - width) / 2f, (Screen.height - height) / 2f, width, height);
            GUI.ModalWindow(904524, bounds, DrawHelpPage, GUIContent.none, _dialogStyle!);
        }

        private void DrawHelpPage(int windowId)
        {
            if (_helpBodyStyle == null)
                _helpBodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, alignment = TextAnchor.UpperLeft, stretchWidth = true };
            GUILayout.Label("HELP · " + (_helpTopic < 0 ? "CREATE A WORLD FROM SCRATCH" : Pages[_helpPage].ToUpperInvariant()), _titleStyle!);
            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            if (NativeButton("This page: " + Pages[_helpPage], _helpTopic >= 0 ? _primaryButtonStyle! : _buttonStyle!, GUILayout.Height(36))) _pendingHelpTopic = _helpPage;
            GUILayout.Space(10);
            if (NativeButton("Create a world from scratch", _helpTopic < 0 ? _primaryButtonStyle! : _buttonStyle!, GUILayout.Height(36))) _pendingHelpTopic = -1;
            GUILayout.EndHorizontal();
            GUILayout.Space(CardSpacing);
            _helpScroll = GUILayout.BeginScrollView(_helpScroll, false, true, GUILayout.ExpandHeight(true));
            var sections = _helpTopic < 0 ? EditorHelp.GettingStarted : EditorHelp.Pages[_helpPage];
            foreach (var section in sections)
            {
                GUILayout.BeginVertical(_cardStyle!);
                GUILayout.Label(section.Title, _sectionStyle!);
                GUILayout.Space(6);
                GUILayout.Label(section.Body, _helpBodyStyle);
                GUILayout.EndVertical();
                GUILayout.Space(CardSpacing);
            }
            GUILayout.EndScrollView();
            GUILayout.Space(12);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Escape also returns to your unchanged editor page.", _mutedStyle!);
            GUILayout.FlexibleSpace();
            if (NativeButton("Close help", _buttonStyle!, GUILayout.Width(160), GUILayout.Height(36))) _pendingHelpClose = true;
            GUILayout.EndHorizontal();
        }
    }
}
