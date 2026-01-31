using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

[System.Serializable]
public class AttackInfo
{
    public float duration = 1f;
    public float cooldown = 2f;
    public float2 aoe = new float2(1f, 1f);
    public float damage = 1f;
    public float3 movement = new float3(0f, 0f, 0f);
    public float value0;
    public float2 selectionDirection = new float2(0f, 1f);
    public Color color = new Color(0.5f, 0f, 1f);
    [HideInInspector]
    public bool unlocked = false;
    [HideInInspector]
    public float timeBuffer = 0f;
}

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
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

    [Space(10)]
    public AttackInfo[] attacks;

    [Header("Misc")]
    public Transform cameraTarget;
    public Transform aimVisual;
    public MeshRenderer attackPreview;

    private float m_speed;
    private float m_animationBlend;
    private float m_targetRotation;
    private float m_rotationVelocity;
    private float m_verticalVelocity;
    private readonly float m_terminalVelocity = 53.0f;

    private bool m_grounded = true;
    private float m_dodgeTimeBuffer;
    private float m_fallTimeBuffer;

    private float3 m_aimDirection = math.forward();
    private float2 m_aimInput;
    private float3 m_dodgeDirection;
    private float m_dodgeSign;

    private bool m_isAttacking;
    private bool m_attackPreview;
    private int m_attackIndex = -1;
    
    private int m_animIDSpeed;
    private int m_animIDDodge;
    private int m_animIDDodgeDirection;
    private int m_animIDGrounded;
    private int m_animIDFreeFall;
    private int m_animIDMotionSpeed;
    
    private int m_shaderIDPlayerPosition;
    private int m_shaderIDPlayerWeapon;
    private int m_shaderIDPlayerWeaponFill;
    
    private int m_shaderIDPreviewColor;
    private int m_shaderIDPreviewIsCircle;
    private int m_shaderIDPreviewFill;
    private int m_shaderIDPreviewUseArrow;
    private int m_shaderIDPreviewRadius;

    private PlayerInput m_playerInput;

    private Animator m_animator;
    private CharacterController m_controller;
    private PlayerCharacterInput m_input;
    private Camera m_mainCamera;
    private Quaternion m_cameraRotation;

    private Random m_rng;

    private bool isCurrentDeviceMouse => m_playerInput.currentControlScheme == "Keyboard&Mouse";

    private void Awake()
    {
        if (m_mainCamera == null)
        {
            m_mainCamera = Camera.main;
        }
    }

    void Start()
    {
        m_rng.InitState();
        
        m_cameraRotation = cameraTarget.rotation;
        
        m_animator = GetComponent<Animator>();
        m_controller = GetComponent<CharacterController>();
        m_input = GetComponent<PlayerCharacterInput>();
        m_playerInput = GetComponent<PlayerInput>();

        m_animIDSpeed = Animator.StringToHash("Speed");
        m_animIDDodge = Animator.StringToHash("Dodge");
        m_animIDDodgeDirection = Animator.StringToHash("DodgeDirection");
        m_animIDGrounded = Animator.StringToHash("Grounded");
        m_animIDFreeFall = Animator.StringToHash("FreeFall");
        m_animIDMotionSpeed = Animator.StringToHash("MotionSpeed");

        m_shaderIDPlayerPosition = Shader.PropertyToID("_Player_Position");
        m_shaderIDPlayerWeapon = Shader.PropertyToID("_CurrentWeaponColor");
        m_shaderIDPlayerWeaponFill = Shader.PropertyToID("_CurrentWeaponFill");
        
        m_shaderIDPreviewColor = Shader.PropertyToID("_Color");
        m_shaderIDPreviewIsCircle = Shader.PropertyToID("_Is_Circle");
        m_shaderIDPreviewFill = Shader.PropertyToID("_Fill");
        m_shaderIDPreviewUseArrow = Shader.PropertyToID("_Use_Arrow");
        m_shaderIDPreviewRadius = Shader.PropertyToID("_Cone_Radius");
        
        m_fallTimeBuffer = 0f;
        
        Debug.Assert(dodgeTime + backwardDodgeDelay < dodgeTimeout);
        Debug.Assert(attacks.Length == 4);

        for (int i = 0; i < 4; ++i)
        {
            attacks[i].selectionDirection = math.normalize(attacks[i].selectionDirection);
            attacks[i].unlocked = true;
            attacks[i].timeBuffer = attacks[i].duration + attacks[i].cooldown;
        }
    }

    void HandleAim()
    {
        if (math.lengthsq(m_input.aimInput) > math.EPSILON)
        {
            float3 inputDirection;
            if (isCurrentDeviceMouse)
            {
                inputDirection = m_mainCamera.ScreenToViewportPoint(m_input.aimInput);
                inputDirection = math.normalize(new float3(inputDirection.x - 0.5f, 0f, inputDirection.y - 0.5f));
            }
            else
            {
                inputDirection = math.normalize(new float3(m_input.aimInput.x, 0.0f, m_input.aimInput.y));
            }
            
            m_aimInput = new float2(inputDirection.x, inputDirection.z);

            if (!m_isAttacking)
            {
                float aimAngle = math.atan2(inputDirection.x, inputDirection.z) +
                                 math.radians(m_mainCamera.transform.eulerAngles.y);
                m_aimDirection = math.mul(quaternion.Euler(0.0f, aimAngle, 0.0f), math.forward());
            }
        }
        else
        {
            m_aimInput = float2.zero;
        }
        
        aimVisual.forward = m_aimDirection;
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
    
    void HandleAttacking(ref float3 movement, ref bool doMovement)
    {
        for (int i = 0; i < 4; ++i)
        {
            if (attacks[i].timeBuffer < (attacks[i].duration + attacks[i].cooldown))
            {
                attacks[i].timeBuffer += Time.deltaTime;
            }
        }
        
        if (!m_isAttacking && m_input.changeMask && math.lengthsq(m_aimInput) > math.EPSILON)
        {
            float closest = 0f;
            int closestIndex = -1;
            for (int i = 0; i < 4; ++i)
            {
                if (!attacks[i].unlocked)
                {
                    continue;
                }
                
                float distance = math.dot(m_aimInput, attacks[i].selectionDirection);
                if (distance > closest)
                {
                    closest = distance;
                    closestIndex = i;
                }
            }

            m_attackIndex = closestIndex;
        }
        
        if (!doMovement)
        {
            return;
        }

        if (m_attackIndex < 0)
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
                    attackPreview.transform.localScale = new float3(currentAttack.aoe.y * 0.5f);
                    attackPreview.transform.position = new float3(transform.position) + m_aimDirection * currentAttack.value0 + new float3(0f, 0.1f, 0f);
                    break;
                }
                case 1:
                {
                    // Spear
                    attackPreview.material.SetFloat(m_shaderIDPreviewIsCircle, 0f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewFill, 1f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewUseArrow, 1f);
                    attackPreview.transform.localScale = new float3(currentAttack.aoe.x, 1f, currentAttack.aoe.y) * 0.5f;
                    attackPreview.transform.position = new float3(transform.position) + m_aimDirection * currentAttack.aoe.y * 0.5f + new float3(0f, 0.1f, 0f);
                    break;
                }
                case 2:
                {
                    // Scythe
                    attackPreview.material.SetFloat(m_shaderIDPreviewIsCircle, 1f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewFill, 1f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewUseArrow, 0f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewRadius, currentAttack.aoe.x);
                    attackPreview.transform.localScale = new float3(currentAttack.aoe.y * 0.5f);
                    attackPreview.transform.position = new float3(transform.position) + m_aimDirection * currentAttack.value0 + new float3(0f, 0.1f, 0f);
                    break;
                }
                case 3:
                {
                    // Rifle
                    attackPreview.material.SetFloat(m_shaderIDPreviewIsCircle, 0f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewFill, 1f);
                    attackPreview.material.SetFloat(m_shaderIDPreviewUseArrow, 0f);
                    attackPreview.transform.localScale = new float3(currentAttack.aoe.x, 1f, currentAttack.aoe.y) * 0.5f;
                    attackPreview.transform.position = new float3(transform.position) + m_aimDirection * currentAttack.aoe.y * 0.5f + new float3(0f, 0.1f, 0f);
                    break;
                }
            }
            
            if (!m_input.attack)
            {
                // start attack
                currentAttack.timeBuffer = 0f;
                transform.forward = m_aimDirection;
                m_isAttacking = true;
                m_attackPreview = false;
                attackPreview.gameObject.SetActive(false);
            }
        }

        if (currentAttack.timeBuffer > currentAttack.duration)
        {
            m_isAttacking = false;
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
            float3 inputDirection = math.normalize(new float3(m_input.move.x, 0.0f, m_input.move.y));
            m_targetRotation = math.atan2(inputDirection.x, inputDirection.z) +
                               math.radians(m_mainCamera.transform.eulerAngles.y);

            float rotation = math.radians(Mathf.SmoothDampAngle(transform.eulerAngles.y,
                math.degrees(m_targetRotation),
                ref m_rotationVelocity, rotationSmoothTime));

            transform.rotation = quaternion.Euler(0.0f, rotation, 0.0f);
        }

        movement = math.mul(quaternion.Euler(0.0f, m_targetRotation, 0.0f), math.forward()) * m_speed *
                   Time.deltaTime;
    }

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
        
        m_controller.Move(movement  +
                          new float3(0.0f, m_verticalVelocity, 0.0f) * Time.deltaTime);
        
        m_animator.SetFloat(m_animIDSpeed, m_animationBlend);
        m_animator.SetFloat(m_animIDMotionSpeed, inputMagnitude);
    }

    private void LateUpdate()
    {
        cameraTarget.rotation = m_cameraRotation;
        Shader.SetGlobalVector(m_shaderIDPlayerPosition, cameraTarget.position);
        
        Shader.SetGlobalColor(m_shaderIDPlayerWeapon, m_attackIndex >= 0 ? attacks[m_attackIndex].color : Color.white);
        Shader.SetGlobalFloat(m_shaderIDPlayerWeaponFill, m_attackIndex >= 0 ? attacks[m_attackIndex].timeBuffer / (attacks[m_attackIndex].duration + attacks[m_attackIndex].cooldown) : 0f);
    }

    private void OnFootstep(AnimationEvent animationEvent)
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

    private void OnLand(AnimationEvent animationEvent)
    {
        if (animationEvent.animatorClipInfo.weight > 0.5f)
        {
            AudioSource.PlayClipAtPoint(landingAudioClip, transform.TransformPoint(m_controller.center), footstepAudioVolume);
        }
    }
}