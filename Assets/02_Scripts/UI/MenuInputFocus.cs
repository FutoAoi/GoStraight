using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Pointer menus start without focus; navigation restores the screen's preferred button.
public sealed class MenuInputFocus : MonoBehaviour
{
    private Selectable _preferred;
    private static bool _usingGamepad;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetMode() => _usingGamepad = false;

    public static void Request(GameObject owner, Selectable preferred)
    {
        if (!owner.TryGetComponent<MenuInputFocus>(out var focus)) focus = owner.AddComponent<MenuInputFocus>();
        focus._preferred = preferred;
        ObserveMode();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (_usingGamepad && preferred != null && preferred.IsActive() && preferred.IsInteractable()) preferred.Select();
    }

    private static bool MouseUsed()
    {
        var mouse = Mouse.current;
        return mouse != null && (mouse.delta.ReadValue().sqrMagnitude > .01f ||
            mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame ||
            mouse.scroll.ReadValue().sqrMagnitude > .01f);
    }
    private static bool GamepadUsed()
    {
        var pad = Gamepad.current;
        return pad != null && (pad.leftStick.ReadValue().sqrMagnitude > .25f ||
            pad.dpad.ReadValue().sqrMagnitude > .25f || pad.buttonSouth.wasPressedThisFrame ||
            pad.startButton.wasPressedThisFrame);
    }
    private static void ObserveMode()
    {
        if (MouseUsed()) _usingGamepad = false;
        else if (GamepadUsed()) _usingGamepad = true;
        else if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) _usingGamepad = false;
    }
    private void LateUpdate()
    {
        ObserveMode();
        if (_preferred == null || !_preferred.IsActive() || !_preferred.IsInteractable()) return;
        var system = EventSystem.current;
        if (system == null) return;
        if (MouseUsed())
        {
            // Clear click-selected focus as well, so moving away stops the selected animation.
            system.SetSelectedGameObject(null);
            return;
        }
        var keyboard = Keyboard.current;
        bool navigate = keyboard != null && (keyboard.upArrowKey.wasPressedThisFrame ||
            keyboard.downArrowKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame ||
            keyboard.rightArrowKey.wasPressedThisFrame || keyboard.tabKey.wasPressedThisFrame ||
            keyboard.wKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame ||
            keyboard.sKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame);
        if ((navigate || GamepadUsed()) && system.currentSelectedGameObject == null) _preferred.Select();
    }
}
