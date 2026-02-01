using UnityEngine;

public class InputDriver : MonoBehaviour
{
    public Vector2 move { get; private set; }
    public Vector2 aimInput { get; private set; }
    public bool changeMask { get; private set; }
    public bool dodge { get; private set; }
    public bool attack { get; private set; }

    public bool analogMovement;

    public virtual bool isCurrentDeviceMouse => false;

    public void MoveInput(Vector2 newMoveDirection)
    {
        move = newMoveDirection;
    } 

    public void AimInput(Vector2 newAimDirection)
    {
        aimInput = newAimDirection;
    }

    public void ChangeMaskInput(bool newChangeMask)
    {
        changeMask = newChangeMask;
    }
    
    public void DodgeInput(bool newDodgeState)
    {
        dodge = newDodgeState;
    }

    public void AttackInput(bool newAttackState)
    {
        attack = newAttackState;
    }
}
