using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCharacterInput : MonoBehaviour
{
    public Vector2 move { get; private set; }
    public Vector2 aimInput { get; private set; }
    public bool dodge { get; private set; }
    public bool sprint { get; private set; }

    public bool analogMovement;

    public bool cursorLocked { get; private set; } = true;
    public bool cursorInputLocked { get; private set; } = true;

    public void OnMove(InputValue value)
    {
        MoveInput(value.Get<Vector2>());
    }

    public void OnAim(InputValue value)
    {
        if(cursorInputLocked)
        {
            CursorInput(value.Get<Vector2>());
        }
    }

    public void OnDodge(InputValue value)
    {
        DodgeInput(value.isPressed);
    }

    public void OnSprint(InputValue value)
    {
        SprintInput(value.isPressed);
    }

    public void MoveInput(Vector2 newMoveDirection)
    {
        move = newMoveDirection;
    } 

    public void CursorInput(Vector2 newLookDirection)
    {
        aimInput = newLookDirection;
    }

    public void DodgeInput(bool newJumpState)
    {
        dodge = newJumpState;
    }

    public void SprintInput(bool newSprintState)
    {
        sprint = newSprintState;
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
