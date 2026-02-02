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
        if (!m_player)
        {
            return;
        }
        
        float2 movementInput = float2.zero;
        float3 toPlayer = m_player.transform.position - transform.position;
        float2 toPlayer2D = new float2(toPlayer.x, toPlayer.z);
        float playerDistanceSq = math.lengthsq(toPlayer2D);
        toPlayer2D *= math.rsqrt(playerDistanceSq);
        float totalAttackDuration = reactionTime + attackPreviewDuration + attackInfo.cooldown + attackInfo.duration;
        float attackDuration = attackInfo.cooldown + attackInfo.duration;
        float attackPreviewStartTime = attackDuration + reactionTime;
        float attackPreviewEndTime = attackPreviewStartTime + attackPreviewDuration;

        AttackInput(m_attackTimeBuffer > attackPreviewStartTime && m_attackTimeBuffer < attackPreviewEndTime);
        if (m_attackTimeBuffer > attackInfo.duration && m_attackTimeBuffer < attackPreviewStartTime)
        {
            m_aimDirection = toPlayer2D;
        }

        bool inReach = playerDistanceSq > (attackInfo.aoe.y * attackInfo.aoe.y);
        if (inReach || math.all(math.abs(math.normalize(new float2(transform.forward.x, transform.forward.z)) - toPlayer2D) >= 0.1f))
        {
            movementInput = toPlayer2D;
        }
        
        if(inReach)
        {
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
