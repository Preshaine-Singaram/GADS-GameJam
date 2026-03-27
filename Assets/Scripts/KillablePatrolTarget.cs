using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

/// <summary>
/// Patrol NPC that can be shot: movement stops and the body is rotated to a lying pose.
/// Add to the same GameObject as <see cref="NpcPatrolController"/> and <see cref="NavMeshAgent"/>.
/// Tag colliders with the same tag your pistol uses (default: Target).
/// </summary>
[DisallowMultipleComponent]
public class KillablePatrolTarget : MonoBehaviour
{
    #region Inspector
    [Header("References (optional — auto-filled from this GameObject)")]
    [SerializeField] private NpcPatrolController m_Patrol;
    [SerializeField] private NavMeshAgent m_Agent;

    [Header("Death pose")]
    [Tooltip("World-space Y rotation is kept; X/Z come from this euler so you can tune prone direction.")]
    [SerializeField] private Vector3 m_LayDownEuler = new Vector3(90f, 0f, 0f);

    [Tooltip("Small vertical nudge after rotation (helps feet not floating).")]
    [SerializeField] private float m_LayDownPositionYOffset = -0.25f;

    [Header("Optional")]
    [Tooltip("If true, non-trigger colliders on this object are disabled after death (no more blocking shots).")]
    [SerializeField] private bool m_DisableCollidersOnDeath = true;

    [SerializeField] private UnityEvent m_OnKilled;
    #endregion

    #region Public Properties
    public bool IsDead { get; private set; }

    /// <summary>Invoked once when <see cref="Kill"/> succeeds.</summary>
    public event Action Killed;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (m_Patrol == null)
            m_Patrol = GetComponent<NpcPatrolController>();

        if (m_Agent == null)
            m_Agent = GetComponent<NavMeshAgent>();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Stops AI movement and applies a lying-down pose. Safe to call multiple times.
    /// </summary>
    public void Kill()
    {
        if (IsDead)
            return;

        IsDead = true;

        if (m_Patrol != null)
        {
            m_Patrol.StopPatrol();
            m_Patrol.enabled = false;
        }

        if (m_Agent != null)
        {
            m_Agent.isStopped = true;
            m_Agent.ResetPath();
            m_Agent.velocity = Vector3.zero;
            m_Agent.enabled = false;
        }

        ApplyLayDownPose();

        if (m_DisableCollidersOnDeath)
            DisableNonTriggerColliders();

        m_OnKilled?.Invoke();
        Killed?.Invoke();
    }
    #endregion

    #region Private Methods
    private void ApplyLayDownPose()
    {
        float yaw = transform.eulerAngles.y;
        transform.rotation = Quaternion.Euler(m_LayDownEuler.x, yaw + m_LayDownEuler.y, m_LayDownEuler.z);

        if (Mathf.Abs(m_LayDownPositionYOffset) > 0.0001f)
            transform.position += Vector3.up * m_LayDownPositionYOffset;
    }

    private void DisableNonTriggerColliders()
    {
        var colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c != null && !c.isTrigger)
                c.enabled = false;
        }
    }
    #endregion
}
