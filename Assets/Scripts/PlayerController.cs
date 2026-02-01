using UnityEngine;
using Unity.Mathematics;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : GameCharacterController
{
    public Transform cameraTarget;
    public Transform aimVisual;
    public MeshRenderer aimRenderer;
    public Transform aimSelect;

    public MaskPedestal interactingPedestal { get; set; }
    
    private int m_shaderIDPlayerPosition;
    private int m_shaderIDPlayerWeapon;
    private int m_shaderIDPlayerWeaponFill;
    private Camera m_gameCamera;
    private Quaternion m_cameraRotation;
    
    private void Awake()
    {
        m_gameCamera = Camera.main;
        Debug.Assert(m_gameCamera is not null);
        Shader.SetGlobalFloat("_EnableDither", 1f);
    }

    private void OnDestroy()
    {
        Shader.SetGlobalFloat("_EnableDither", 0f);
    }

    protected override void OnStart()
    {
        m_rng.InitState();
        
        m_cameraRotation = cameraTarget.rotation;

        m_shaderIDPlayerPosition = Shader.PropertyToID("_Player_Position");
        m_shaderIDPlayerWeapon = Shader.PropertyToID("_CurrentWeaponColor");
        m_shaderIDPlayerWeaponFill = Shader.PropertyToID("_CurrentWeaponFill");
        
        Debug.Assert(attacks.Length == 4);

        for (int i = 0; i < 4; ++i)
        {
            attacks[i].selectionDirection = math.normalize(attacks[i].selectionDirection);
        }
    }

    protected override float3 GetAimInput()
    {
        float3 result = float3.zero;
        
        if (math.lengthsq(m_input.aimInput) > math.EPSILON)
        {
            if (m_input.isCurrentDeviceMouse)
            {
                result = m_gameCamera.ScreenToViewportPoint(m_input.aimInput);
                result = math.normalize(new float3(result.x - 0.5f, 0f, result.y - 0.5f));
            }
            else
            {
                result = math.normalize(new float3(m_input.aimInput.x, 0.0f, m_input.aimInput.y));
            }

            if (!m_isAttacking)
            {
                result.y = math.atan2(result.x, result.z) +
                         math.radians(m_gameCamera.transform.eulerAngles.y);
            }
        }

        return result;
    }

    protected override void OnAim()
    {
        aimVisual.forward = m_aimDirection;
        aimSelect.forward = math.mul(quaternion.Euler(0.0f, math.radians(m_gameCamera.transform.eulerAngles.y), 0.0f), math.forward());
    }

    protected override bool OnHandleAttacking(ref float3 movement, ref bool doMovement)
    {
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
            aimSelect.gameObject.SetActive(true);
        }
        else
        {
            aimSelect.gameObject.SetActive(false);
        }

        if (doMovement && interactingPedestal is not null)
        {
            if (m_input.attack)
            {
                attacks[interactingPedestal.maskIndex].unlocked = true;
                attacks[interactingPedestal.maskIndex].selectionVisual.SetActive(true);
                m_attackIndex =  interactingPedestal.maskIndex;
            }

            return false;
        }

        return true;
    }

    protected override float GetTargetRotation()
    {
        float3 inputDirection = math.normalize(new float3(m_input.move.x, 0.0f, m_input.move.y));
        return math.atan2(inputDirection.x, inputDirection.z) + math.radians(m_gameCamera.transform.eulerAngles.y);
    }

    private void LateUpdate()
    {
        cameraTarget.rotation = m_cameraRotation;
        Shader.SetGlobalVector(m_shaderIDPlayerPosition, cameraTarget.position);
        
        aimRenderer.material.SetColor(m_shaderIDPlayerWeapon, m_attackIndex >= 0 ? attacks[m_attackIndex].color : Color.white);
        aimRenderer.material.SetFloat(m_shaderIDPlayerWeaponFill, m_attackIndex >= 0 ? attacks[m_attackIndex].timeBuffer / (attacks[m_attackIndex].duration + attacks[m_attackIndex].cooldown) : 0f);
    }
}