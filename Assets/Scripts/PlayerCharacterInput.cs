using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerInput))]
public class PlayerCharacterInput : InputDriver
{
    public bool cursorLocked { get; private set; } = false;
    public bool cursorInputLocked { get; private set; } = true;
    
    private PlayerInput m_playerInput;

    public override bool isCurrentDeviceMouse => m_playerInput.currentControlScheme == "Keyboard&Mouse";

    private void Awake()
    {
        m_playerInput = GetComponent<PlayerInput>();
    }

    public void OnMove(InputValue value)
    {
        MoveInput(value.Get<Vector2>());
    }

    public void OnAim(InputValue value)
    {
        if(cursorInputLocked)
        {
            AimInput(value.Get<Vector2>());
        }
    }

    public void OnChangeMask(InputValue value)
    {
        ChangeMaskInput(value.isPressed);
    }
    
    public void OnDodge(InputValue value)
    {
        DodgeInput(value.isPressed);
    }

    public void OnAttack(InputValue value)
    {
        AttackInput(value.isPressed);
    }
		
    private void OnApplicationFocus(bool hasFocus)
    {
        SetCursorState(cursorLocked);
    }

    public void SetCursorState(bool newState)
    {
        cursorLocked = newState;
        Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
    }

    public void LockCursorInput(bool locked)
    {
        cursorInputLocked = locked;
    }
}
