using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace WorldCreator;

public sealed partial class WorldCreatorWindow
{
    private bool _confirmationPending;
    private GameObject? _confirmationBackdrop;
    private Texture2D? _confirmationTexture;

    /// <summary>Finishes the Back click before placing the game's confirmation over the editor.</summary>
    private void ConfirmLeaveEditor()
    {
        ConfirmEditorAction("Leave editor?", "Return to the World Creator start page?", "Leave",
            () => _pendingScreen = 0, "Keep editing");
    }

    /// <summary>Confirms deletion of a library copy without removing weather already added to this draft.</summary>
    private void ConfirmDeleteSavedWeather(string id, string name)
    {
        ConfirmEditorAction("Delete saved weather effect?", "Delete " + name + " from My Saved Weather?\n\nA copy already added to this world will remain.",
            "Delete", () =>
            {
                WeatherEffectLibrary.Delete(id);
                _status = "Deleted the saved effect. Effects already added to this project are unchanged.";
            });
    }

    /// <summary>Queues a native confirmation after the current editor frame finishes drawing.</summary>
    private void ConfirmEditorAction(string title, string message, string confirmLabel, Action confirm, string cancelLabel = "Cancel")
    {
        if (_confirmationPending || Assets.Scripts.UI.PromptPanel.Instance.IsActive)
            return;
        CloseNativePopup();
        GUI.FocusControl(null);
        GUIUtility.hotControl = 0;
        _confirmationPending = true;
        StartCoroutine(ShowEditorPrompt(title, message, confirmLabel, confirm, cancelLabel));
    }

    /// <summary>Keeps the rendered editor visible beneath the native dialog until the player chooses.</summary>
    private IEnumerator ShowEditorPrompt(string title, string message, string confirmLabel, Action confirm, string cancelLabel)
    {
        yield return new WaitForEndOfFrame();
        _confirmationPending = false;
        var prompt = Assets.Scripts.UI.PromptPanel.Instance;
        try
        {
            // IMGUI draws after the native canvas; use its completed frame as the modal backdrop.
            _confirmationTexture = ScreenCapture.CaptureScreenshotAsTexture();
            _confirmationBackdrop = new GameObject("WorldCreatorConfirmationBackdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            _confirmationBackdrop.layer = prompt.Blocker.gameObject.layer;
            var rect = (RectTransform)_confirmationBackdrop.transform;
            rect.SetParent(prompt.Blocker.transform.parent, false);
            rect.SetSiblingIndex(prompt.Blocker.transform.GetSiblingIndex());
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = _confirmationBackdrop.GetComponent<RawImage>();
            image.texture = _confirmationTexture;
            image.raycastTarget = false;
            prompt.CancelButton.gameObject.SetActive(true);
            prompt.ShowPrompt(title, message, confirmLabel,
                () => CloseEditorPrompt(confirm), cancelLabel, () => CloseEditorPrompt(null));
        }
        catch (Exception exception)
        {
            ClearConfirmationBackdrop();
            _status = "Could not show confirmation: " + exception.Message;
            Debug.LogError("[World Creator] Could not show confirmation: " + exception);
        }
    }

    /// <summary>Closes the native dialog before applying its confirmed action and reports action failures.</summary>
    private void CloseEditorPrompt(Action? confirm)
    {
        Assets.Scripts.UI.PromptPanel.Instance.DisablePromptPanel();
        ClearConfirmationBackdrop();
        try
        {
            confirm?.Invoke();
        }
        catch (Exception exception)
        {
            _status = exception.Message;
        }
    }

    /// <summary>Releases the temporary in-memory backdrop when the confirmation closes or the editor is destroyed.</summary>
    private void ClearConfirmationBackdrop()
    {
        if (_confirmationBackdrop != null)
        {
            _confirmationBackdrop.SetActive(false);
            Destroy(_confirmationBackdrop);
            _confirmationBackdrop = null;
        }
        if (_confirmationTexture != null)
        {
            Destroy(_confirmationTexture);
            _confirmationTexture = null;
        }
    }
}
