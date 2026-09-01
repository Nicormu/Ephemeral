using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Add to a full-screen Image child of a UIPopupPanel (Raycast Target must be ON), placed
/// BEFORE the panel's actual content in the hierarchy so the content renders on top of it and
/// naturally blocks clicks in its own area. Clicking anywhere else on screen — i.e. actually
/// hitting this backdrop — closes the parent UIPopupPanel. Leave this child out of a popup's
/// hierarchy entirely if that popup shouldn't support click-outside-to-close.
/// </summary>
public class UIPopupBackdrop : MonoBehaviour, IPointerClickHandler
{
    private UIPopupPanel _panel;

    private void Awake()
    {
        _panel = GetComponentInParent<UIPopupPanel>();

        if (_panel == null)
            Debug.LogWarning($"[UIPopupBackdrop] '{name}' has no UIPopupPanel in its parent hierarchy — clicking it won't close anything.");
    }

    public void OnPointerClick(PointerEventData eventData) => _panel?.Hide();
}