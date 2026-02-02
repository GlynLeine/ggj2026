using UnityEngine;
using Unity.Mathematics;

public class MaskPedestal : MonoBehaviour
{
    public Transform interactionCenter;
    public float interactionRadius;

    public int maskIndex;
    
    private PlayerController m_player;

    private void Awake()
    {
        m_player = FindAnyObjectByType<PlayerController>();
    }

    void Update()
    {
        if (!m_player || (m_player.interactingPedestal is not null && m_player.interactingPedestal != this))
        {
            return;
        }

        m_player.interactingPedestal = math.distancesq(m_player.transform.position, interactionCenter.position) <=
                                       interactionRadius * interactionRadius ? this : null;
    }
}
