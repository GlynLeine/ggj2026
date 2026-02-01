using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

[System.Serializable]
public class AttackInfo
{
    public float duration = 1f;
    public float cooldown = 2f;
    public float2 aoe = new float2(1f, 1f);
    public float damage = 1f;
    public float knockbackForce = 1f;
    public float3 movement = new float3(0f, 0f, 0f);
    public float value0;
    public float2 selectionDirection = new float2(0f, 1f);
    public Color color = new Color(0.5f, 0f, 1f);
    public GameObject selectionVisual;
    public GameObject weaponCollider;
    [HideInInspector]
    public bool unlocked = false;
    [HideInInspector]
    public float timeBuffer = 0f;
}

[RequireComponent(typeof(CharacterController))]
public abstract class GameCharacterController : MonoBehaviour
{
    [Header("Movement")]
    public float movementSpeed = 5.335f;
    [Range(0.0f, 0.3f)] public float rotationSmoothTime = 0.12f;
    public float speedChangeRate = 10.0f;
    public AudioClip[] footstepAudioClips;
    [Range(0, 1)] public float footstepAudioVolume = 0.5f;

    [Header("Dodge")]
    public float dodgeDistance = 2.0f;
    public float dodgeTime = 0.25f;
    public float dodgeTimeout = 0.50f;
    public float backwardDodgeDelay = 0.1f;
    
    [Header("Falling & Landing")]
    public float gravity = -15.0f;
    public float fallTimeout = 0.15f;
    public float groundedOffset = -0.14f;
    public float groundedRadius = 0.28f;
    public LayerMask groundLayers;
    public AudioClip landingAudioClip;

    [Header("Attacks")]
    public float maxHealth;
    public MeshRenderer attackPreview;
    public AttackInfo[] attacks;

    protected float m_health;
    
    protected float m_speed;
    protected float m_animationBlend;
    protected float m_targetRotation;
    protected float m_rotationVelocity;
    protected float m_verticalVelocity;
    protected readonly float m_terminalVelocity = 53.0f;
    protected float3 m_knockbackVelocity;

    protected bool m_grounded = true;
    protected float m_dodgeTimeBuffer;
    protected float m_fallTimeBuffer;

    protected float3 m_attackOrigin;
    protected float3 m_aimDirection = math.forward();
    protected float2 m_aimInput;
    protected float3 m_dodgeDirection;
    protected float m_dodgeSign;
    protected HashSet<EntityId> m_attackedCharacters = new HashSet<EntityId>();
    protected TagHandle m_tag;

    protected bool m_isAttacking;
    protected bool m_attackPreview;
    protected int m_attackIndex = -1;
    
    protected int m_animIDSpeed;
    protected int m_animIDDodge;
    protected int m_animIDDodgeDirection;
    protected int m_animIDGrounded;
    protected int m_animIDFreeFall;
    protected int m_animIDMotionSpeed;
    
    protected int m_shaderIDPreviewColor;
    protected int m_shaderIDPreviewIsCircle;
    protected int m_shaderIDPreviewFill;
    protected int m_shaderIDPreviewUseArrow;
    protected int m_shaderIDPreviewRadius;

    protected int m_characterCollisionLayer;
    
    protected Animator m_animator;
    protected CharacterController m_controller;
    protected InputDriver m_input;

    protected Random m_rng;

    protected virtual void OnStart() {} 
    
    void Start()
    {
        m_rng.InitState();
        
        m_animator = GetComponent<Animator>();
        m_controller = GetComponent<CharacterController>();
        m_input = GetComponent<InputDriver>();
        m_tag = TagHandle.GetExistingTag(gameObject.tag);
        
        Debug.Assert(m_input is not null);

        m_animIDSpeed = Animator.StringToHash("Speed");
        m_animIDDodge = Animator.StringToHash("Dodge");
        m_animIDDodgeDirection = Animator.StringToHash("DodgeDirection");
        m_animIDGrounded = Animator.StringToHash("Grounded");
        m_animIDFreeFall = Animator.StringToHash("FreeFall");
        m_animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
        
        m_shaderIDPreviewColor = Shader.PropertyToID("_Color");
        m_shaderIDPreviewIsCircle = Shader.PropertyToID("_Is_Circle");
        m_shaderIDPreviewFill = Shader.PropertyToID("_Fill");
        m_shaderIDPreviewUseArrow = Shader.PropertyToID("_Use_Arrow");
        m_shaderIDPreviewRadius = Shader.PropertyToID("_Cone_Radius");
        
        m_characterCollisionLayer = LayerMask.NameToLayer("Character");
        
        m_fallTimeBuffer = 0f;
        m_health = maxHealth;
        
        OnStart();
        
        Debug.Assert(dodgeTime + backwardDodgeDelay < dodgeTimeout);

        foreach (AttackInfo attack in attacks)
        {
            attack.timeBuffer = attack.duration + attack.cooldown;
            attack.weaponCollider.GetComponent<WeaponCollider>().characterController = this;
        }
    }

    /// x and z are the normalized input, y is the aim direction angle in radians
    protected virtual float3 GetAimInput()
    {
        float3 result = math.normalize(new float3(m_input.aimInput.x, 0.0f, m_input.aimInput.y));
        if (!m_isAttacking)
        {
            result.y = math.atan2(result.x, result.z);
        }
        return result;
    }

    protected virtual void OnAim() {}
    
    void HandleAim()
    {
        if (math.lengthsq(m_input.aimInput) > math.EPSILON)
        {
            float3 inputDirection = GetAimInput();
            m_aimInput = new float2(inputDirection.x, inputDirection.z);

            if (!m_isAttacking)
            {
                m_aimDirection = math.mul(quaternion.Euler(0.0f, inputDirection.y, 0.0f), math.forward());
            }
        }
        else
        {
            m_aimInput = float2.zero;
        }

        OnAim();
    }

    void HandleFallingAndLanding()
    {
        if (m_grounded)
        {
            m_fallTimeBuffer = 0f;

            m_animator.SetBool(m_animIDFreeFall, false);

            // stop our velocity dropping infinitely when grounded
            if (m_verticalVelocity < 0.0f)
            {
                m_verticalVelocity = -2f;
            }
        }
        else
        {
            // fall timeout
            if (m_fallTimeBuffer < fallTimeout)
            {
                m_fallTimeBuffer += Time.deltaTime;
            }
            else
            {
                m_animator.SetBool(m_animIDFreeFall, true);
            }
        }

        if (m_verticalVelocity < m_terminalVelocity)
        {
            m_verticalVelocity += gravity * Time.deltaTime;
        }

        float3 spherePosition = new float3(transform.position.x, transform.position.y - groundedOffset, transform.position.z);
        m_grounded = Physics.CheckSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);
        m_animator.SetBool(m_animIDGrounded, m_grounded);
    }

    void HandleDodge(ref float3 movement, ref bool doMovement)
    {
        if (m_dodgeTimeBuffer < dodgeTimeout)
        {
            m_dodgeTimeBuffer += Time.deltaTime;
            m_animator.SetBool(m_animIDDodge, false);
        }
        else if (m_grounded && m_input.dodge && !m_isAttacking)
        {
            m_input.DodgeInput(false);

            m_dodgeDirection = -m_aimDirection;
            m_dodgeTimeBuffer = 0f;
            m_animator.SetBool(m_animIDDodge, true);
            m_dodgeSign = math.sign(math.dot(m_dodgeDirection, transform.forward));
            m_animator.SetFloat(m_animIDDodgeDirection, m_dodgeSign);
            transform.forward = m_dodgeDirection * m_dodgeSign;
        }
        
        if (m_dodgeTimeBuffer < dodgeTimeout)
        {
            if ((m_dodgeSign > 0f && m_dodgeTimeBuffer < dodgeTime) ||
                (m_dodgeSign < 0f && m_dodgeTimeBuffer < (dodgeTime + backwardDodgeDelay) && m_dodgeTimeBuffer > backwardDodgeDelay))
            {
                movement = m_dodgeDirection * (dodgeDistance / dodgeTime) * Time.deltaTime;
            }

            m_speed = 0f;
            m_animationBlend = 0f;
            doMovement = false;
        }
    }

    public virtual void OnAttackHit(GameCharacterController otherCharacter)
    {
        if (m_attackIndex < 0)
        {
            return;
        }

        if (otherCharacter.CompareTag(m_tag))
        {
            return;
        }

        if (!m_attackedCharacters.Add(otherCharacter.GetEntityId()))
        {
            return;
        }
        
        otherCharacter.Hurt(attacks[m_attackIndex].damage);

        float3 otherCharacterDirection = otherCharacter.transform.position - transform.position;
        otherCharacterDirection.y = 0f;
        otherCharacterDirection = math.normalizesafe(otherCharacterDirection, m_aimDirection);
        
        // update attack
        switch (m_attackIndex)
        {
            case 0:
            {
                // Gauntlets
                otherCharacter.GetKnockback(otherCharacterDirection * attacks[m_attackIndex].knockbackForce);
                break;
            }
            case 1:
            {
                // Spear
                otherCharacter.GetKnockback((m_aimDirection + otherCharacterDirection) * 0.5f * attacks[m_attackIndex].knockbackForce);
                break;
            }
            case 2:
            {
                // Scythe
                otherCharacter.GetKnockback(otherCharacterDirection * attacks[m_attackIndex].knockbackForce);
                break;
            }
            case 3:
            {
                // Rifle
                otherCharacter.GetKnockback((m_aimDirection + otherCharacterDirection) * 0.5f * attacks[m_attackIndex].knockbackForce);
                break;
            }
        }
    }

    public void GetKnockback(float3 force)
    {
        m_knockbackVelocity += force * 4f;
        Debug.Log(m_knockbackVelocity);
    }

    public void OnTriggerEnter(Collider other)
    {
        WeaponCollider weaponCollider = other.transform.parent.GetComponent<WeaponCollider>();
        if (weaponCollider is null || weaponCollider.characterController == this)
        {
            return;
        }

        weaponCollider.characterController.OnAttackHit(this);
    }

    public void Hurt(float damage)
    {
        m_health -= damage;
    }
    
    protected virtual bool OnHandleAttacking(ref float3 movement, ref bool doMovement) { return true; }
    
    void HandleAttacking(ref float3 movement, ref bool doMovement)
    {
        foreach (AttackInfo attack in attacks)
        {
            if (attack.timeBuffer < (attack.duration + attack.cooldown))
            {
                attack.timeBuffer += Time.deltaTime;
            }
        }

        if (!OnHandleAttacking(ref movement, ref doMovement))
        {
            return;
        }

        if (!doMovement || m_attackIndex < 0)
        {
            return;
        }

        AttackInfo currentAttack = attacks[m_attackIndex];
        float totalAttackTime = currentAttack.duration + currentAttack.cooldown;
        
        if (m_input.attack && currentAttack.timeBuffer >= totalAttackTime)
        {
            m_attackPreview = true;
            attackPreview.gameObject.SetActive(true);
        }

        if (m_attackPreview)
        {
            if (!m_isAttacking)
            {
                m_attackOrigin = transform.position;
            }
            
            attackPreview.material.SetColor(m_shaderIDPreviewColor, currentAttack.color);
            attackPreview.transform.forward = m_aimDirection;
            switch (m_attackIndex)
            {
                case 0:
                {
                    // Gauntlets
                    attackPreview.material.SetFloat(m_shaderIDPreviewIsCircle, 1f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewFill, 1f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewUseArrow, 0f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewRadius, currentAttack.aoe.x);
                    attackPreview.transform.localScale = new float3(currentAttack.aoe.y);
                    attackPreview.transform.position = m_attackOrigin + m_aimDirection * currentAttack.value0 + new float3(0f, 0.05f, 0f);
                    break;
                }
                case 1:
                {
                    // Spear
                    attackPreview.material.SetFloat(m_shaderIDPreviewIsCircle, 0f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewFill, 1f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewUseArrow, 1f);
                    attackPreview.transform.localScale = new float3(currentAttack.aoe.x, 1f, currentAttack.aoe.y) * 0.5f;
                    attackPreview.transform.position = m_attackOrigin + m_aimDirection * currentAttack.aoe.y * 0.5f + new float3(0f, 0.05f, 0f);
                    break;
                }
                case 2:
                {
                    // Scythe
                    attackPreview.material.SetFloat(m_shaderIDPreviewIsCircle, 1f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewFill, 1f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewUseArrow, 0f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewRadius, currentAttack.aoe.x);
                    attackPreview.transform.localScale = new float3(currentAttack.aoe.y);
                    attackPreview.transform.position = m_attackOrigin + m_aimDirection * currentAttack.value0 + new float3(0f, 0.05f, 0f);
                    break;
                }
                case 3:
                {
                    // Rifle
                    attackPreview.material.SetFloat(m_shaderIDPreviewIsCircle, 0f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewFill, 1f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewUseArrow, 0f);
                    attackPreview.transform.localScale = new float3(currentAttack.aoe.x, 1f, currentAttack.aoe.y) * 0.5f;
                    attackPreview.transform.position = m_attackOrigin + m_aimDirection * currentAttack.aoe.y * 0.5f + new float3(0f, 0.05f, 0f);
                    break;
                }
            }
            
            if (!m_input.attack && !m_isAttacking)
            {
                // start attack
                currentAttack.timeBuffer = 0f;
                transform.forward = m_aimDirection;
                m_isAttacking = true;
                currentAttack.weaponCollider.SetActive(true);
            }
        }

        if (currentAttack.timeBuffer > currentAttack.duration)
        {
            if (m_isAttacking)
            {
                m_attackPreview = false;
                attackPreview.gameObject.SetActive(false);
                m_isAttacking = false;
                currentAttack.weaponCollider.SetActive(false);
                m_attackedCharacters.Clear();
                Physics.IgnoreLayerCollision(m_characterCollisionLayer, m_characterCollisionLayer, false);
            }

            return;
        }

        // update attack
        switch (m_attackIndex)
        {
            case 0:
            {
                // Gauntlets
                movement = float3.zero;
                break;
            }
            case 1:
            {
                // Spear
                movement = m_aimDirection * (currentAttack.movement.z / currentAttack.duration) * Time.deltaTime;
                Physics.IgnoreLayerCollision(m_characterCollisionLayer, m_characterCollisionLayer, true);
                break;
            }
            case 2:
            {
                // Scythe
                movement = float3.zero;
                break;
            }
            case 3:
            {
                // Rifle
                movement = float3.zero;
                break;
            }
        }
        
        m_speed = 0f;
        m_animationBlend = 0f;
        doMovement = false;
    }
    
    protected virtual float GetTargetRotation()
    {
        float3 inputDirection = math.normalize(new float3(m_input.move.x, 0.0f, m_input.move.y));
        return math.atan2(inputDirection.x, inputDirection.z);
    }
    
    void HandleMovement(ref float3 movement, bool doMovement, float inputMagnitude)
    {
        if (!doMovement)
        {
            m_targetRotation = math.atan2(movement.x, movement.z);
            return;
        }

        float targetSpeed = movementSpeed;
        if (math.lengthsq(m_input.move) <= math.EPSILON)
        {
            targetSpeed = 0.0f;
        }

        float currentHorizontalSpeed = math.length(new float3(m_controller.velocity.x, 0.0f, m_controller.velocity.z));
        float knockbackSpeed = math.length(m_knockbackVelocity);
        currentHorizontalSpeed = math.max(0f, currentHorizontalSpeed - knockbackSpeed);

        float speedOffset = 0.1f;

        if (currentHorizontalSpeed < targetSpeed - speedOffset || currentHorizontalSpeed > targetSpeed + speedOffset)
        {
            m_speed = Mathf.Lerp(currentHorizontalSpeed, targetSpeed * inputMagnitude,
                Time.deltaTime * speedChangeRate);
            m_speed = Mathf.Round(m_speed * 1000f) / 1000f;
        }
        else
        {
            m_speed = targetSpeed;
        }

        m_animationBlend = Mathf.Lerp(m_animationBlend, targetSpeed, Time.deltaTime * speedChangeRate);
        if (m_animationBlend < 0.01f)
        {
            m_animationBlend = 0f;
        }

        if (math.lengthsq(m_input.move) > math.EPSILON)
        {
            m_targetRotation = GetTargetRotation();

            float rotation = math.radians(Mathf.SmoothDampAngle(transform.eulerAngles.y,
                math.degrees(m_targetRotation),
                ref m_rotationVelocity, rotationSmoothTime));

            transform.rotation = quaternion.Euler(0.0f, rotation, 0.0f);
        }

        movement += math.mul(quaternion.Euler(0.0f, m_targetRotation, 0.0f), math.forward()) * m_speed *
                   Time.deltaTime;
    }

    private void FixedUpdate()
    {
        m_knockbackVelocity *= 0.75f;
    }

    protected abstract void OnDeath();
    
    void Update()
    {
        bool doMovement = true;
        HandleAim();
        HandleFallingAndLanding();
        
        float3 movement = float3.zero;
        float inputMagnitude = m_input.analogMovement ? m_input.move.magnitude : 1f;
        HandleDodge(ref movement, ref doMovement);
        HandleAttacking(ref movement, ref doMovement);
        HandleMovement(ref movement, doMovement, inputMagnitude);

        movement += new float3(0.0f, m_verticalVelocity, 0.0f) * Time.deltaTime + m_knockbackVelocity * Time.deltaTime;
        
        m_controller.Move(movement);
        
        m_animator.SetFloat(m_animIDSpeed, m_animationBlend);
        m_animator.SetFloat(m_animIDMotionSpeed, inputMagnitude);

        if (m_health <= 0f)
        {
            OnDeath();
        }
    }

    protected void OnFootstep(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight > 0.5f)
        {
            if (footstepAudioClips.Length > 0)
            {
                var index = m_rng.NextInt(0, footstepAudioClips.Length);
                AudioSource.PlayClipAtPoint(footstepAudioClips[index], transform.TransformPoint(m_controller.center), footstepAudioVolume);
            }
        }
    }

    protected void OnLand(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight > 0.5f)
        {
            AudioSource.PlayClipAtPoint(landingAudioClip, transform.TransformPoint(m_controller.center), footstepAudioVolume);
        }
    }
}
