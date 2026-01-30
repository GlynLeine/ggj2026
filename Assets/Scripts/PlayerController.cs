using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    public float movementSpeed = 5.335f;
    [Range(0.0f, 0.3f)] public float rotationSmoothTime = 0.12f;
    public float speedChangeRate = 10.0f;

    public AudioClip landingAudioClip;
    public AudioClip[] footstepAudioClips;
    [Range(0, 1)] public float footstepAudioVolume = 0.5f;

    public float gravity = -15.0f;
    public float fallTimeout = 0.15f;
    public float groundedOffset = -0.14f;
    public float groundedRadius = 0.28f;
    public LayerMask groundLayers;

    public Transform cameraTarget;
    public Transform aimVisual;
    
    private float m_speed;
    private float m_animationBlend;
    private float m_targetRotation;
    private float m_rotationVelocity;
    private float m_verticalVelocity;
    private readonly float m_terminalVelocity = 53.0f;

    private bool m_grounded = true;
    private float m_fallTimeBuffer;

    private float3 m_aimDirection = math.forward();
    
    private int m_animIDSpeed;
    private int m_animIDGrounded;
    private int m_animIDFreeFall;
    private int m_animIDMotionSpeed;
    private int m_shaderIDPlayerPosition;

    private PlayerInput m_playerInput;

    private Animator m_animator;
    private CharacterController m_controller;
    private PlayerCharacterInput m_input;
    private Camera m_mainCamera;
    private Quaternion m_cameraRotation;

    private Random m_rng;
    
    public bool isCurrentDeviceMouse => m_playerInput.currentControlScheme == "Keyboard&Mouse";

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
        m_animIDGrounded = Animator.StringToHash("Grounded");
        m_animIDFreeFall = Animator.StringToHash("FreeFall");
        m_animIDMotionSpeed = Animator.StringToHash("MotionSpeed");

        m_shaderIDPlayerPosition = Shader.PropertyToID("_Player_Position");
        
        m_fallTimeBuffer = 0f;
    }

    // Update is called once per frame
    void Update()
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

        float targetSpeed = movementSpeed;
        if (math.lengthsq(m_input.move) <= math.EPSILON)
        {
            targetSpeed = 0.0f;
        }

        float currentHorizontalSpeed = math.length(new float3(m_controller.velocity.x, 0.0f, m_controller.velocity.z));

        float speedOffset = 0.1f;
        float inputMagnitude = m_input.analogMovement ? m_input.move.magnitude : 1f;

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

            float rotation = math.radians(Mathf.SmoothDampAngle(transform.eulerAngles.y, math.degrees(m_targetRotation),
                ref m_rotationVelocity, rotationSmoothTime));

            transform.rotation = quaternion.Euler(0.0f, rotation, 0.0f);
        }
        
        float3 targetDirection = math.mul(quaternion.Euler(0.0f, m_targetRotation, 0.0f), math.forward());

        m_controller.Move(targetDirection * (m_speed * Time.deltaTime) +
                          new float3(0.0f, m_verticalVelocity, 0.0f) * Time.deltaTime);

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
            
            float aimAngle = math.atan2(inputDirection.x, inputDirection.z) +
                             math.radians(m_mainCamera.transform.eulerAngles.y);
            m_aimDirection = math.mul(quaternion.Euler(0.0f, aimAngle, 0.0f), math.forward());
        }
        
        aimVisual.forward = m_aimDirection;
        
        m_animator.SetFloat(m_animIDSpeed, m_animationBlend);
        m_animator.SetFloat(m_animIDMotionSpeed, inputMagnitude);
    }

    private void LateUpdate()
    {
        cameraTarget.rotation = m_cameraRotation;
        Shader.SetGlobalVector(m_shaderIDPlayerPosition, cameraTarget.position);
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