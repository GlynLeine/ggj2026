using Unity.Mathematics;
using UnityEngine;

public class EnemyBrain : InputDriver
{
    public enum WeaponType
    {
        Gauntlets,
        Spear,
        Scythe,
        Rifle,
    }
    
    public WeaponType weaponType = WeaponType.Gauntlets;
    public float attackPreviewDuration = 0.3f;
    public float reactionTime = 0.3f;
    
    [HideInInspector] public AttackInfo attackInfo;
    
    private PlayerController m_player;
    private float m_attackTimeBuffer;
    private float2 m_aimDirection;

    private void Awake()
    {
        m_player = FindAnyObjectByType<PlayerController>();
    }

    private void Update()
    {
        float2 movementInput = float2.zero;
        float3 toPlayer = m_player.transform.position - transform.position;
        float2 toPlayer2D = new float2(toPlayer.x, toPlayer.z);
        float playerDistanceSq = math.lengthsq(toPlayer2D);
        float totalAttackDuration = reactionTime + attackPreviewDuration + attackInfo.cooldown + attackInfo.duration;
        float attackDuration = attackInfo.cooldown + attackInfo.duration;
        float attackPreviewStartTime = attackDuration + reactionTime;
        float attackPreviewEndTime = attackPreviewStartTime + attackPreviewDuration;

        AttackInput(m_attackTimeBuffer > attackPreviewStartTime && m_attackTimeBuffer < attackPreviewEndTime);
        if (m_attackTimeBuffer < attackPreviewStartTime)
        {
            m_aimDirection = toPlayer2D;
        }
        
        if (playerDistanceSq > attackInfo.aoe.y * attackInfo.aoe.y)
        {
            movementInput = toPlayer2D * math.rsqrt(playerDistanceSq);
            if (m_attackTimeBuffer > attackDuration)
            {
                m_attackTimeBuffer = attackDuration;
            }
        }
        else
        {
            if (m_attackTimeBuffer < totalAttackDuration)
            {
                m_attackTimeBuffer += Time.deltaTime;
            }
            
            if (m_attackTimeBuffer >= totalAttackDuration)
            {
                m_attackTimeBuffer = 0f;
            }
        }
        
        AimInput(m_aimDirection);
        MoveInput(movementInput);
    }
}
