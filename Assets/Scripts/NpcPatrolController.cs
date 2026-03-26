using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Moves an NPC along a PatrolRoute using a NavMeshAgent.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NpcPatrolController : MonoBehaviour
{
    #region Inspector
    [Header("References")]
    [Tooltip("Route used for this NPC patrol.")]
    [SerializeField] private PatrolRoute m_Route;

    [Tooltip("Agent used for movement. If null, auto-fetched from this GameObject.")]
    [SerializeField] private NavMeshAgent m_Agent;

    [Header("Movement")]
    [Tooltip("Start waypoint index in the route.")]
    [SerializeField] private int m_StartingWaypointIndex = 0;

    [Tooltip("Distance from destination considered as arrived.")]
    [SerializeField] private float m_ArrivalDistance = 0.35f;

    [Tooltip("Seconds to wait at each waypoint before moving on.")]
    [SerializeField] private float m_WaitAtWaypointSeconds = 1.2f;

    [Tooltip("If true, patrol starts automatically on Start().")]
    [SerializeField] private bool m_AutoStartPatrol = true;
    #endregion

    #region Private Fields
    private int m_CurrentWaypointIndex = -1;
    private int m_PingPongDirection = 1;
    private float m_WaitTimer;
    private bool m_IsPatrolling;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (m_Agent == null)
            m_Agent = GetComponent<NavMeshAgent>();

        if (m_Agent != null)
            m_Agent.stoppingDistance = Mathf.Max(0f, m_ArrivalDistance);
    }

    private void Start()
    {
        if (!m_AutoStartPatrol)
            return;

        BeginPatrol();
    }

    private void Update()
    {
        if (!m_IsPatrolling || m_Agent == null || m_Route == null || m_Route.WaypointCount <= 0)
            return;

        if (m_Agent.pathPending)
            return;

        if (m_Agent.remainingDistance > Mathf.Max(m_ArrivalDistance, m_Agent.stoppingDistance))
            return;

        m_WaitTimer += Time.deltaTime;
        if (m_WaitTimer < m_WaitAtWaypointSeconds)
            return;

        m_WaitTimer = 0f;
        AdvanceToNextWaypoint();
    }
    #endregion

    #region Public Methods
    [ContextMenu("Begin Patrol")]
    public void BeginPatrol()
    {
        if (m_Agent == null || m_Route == null || m_Route.WaypointCount <= 0)
        {
            m_IsPatrolling = false;
            return;
        }

        m_CurrentWaypointIndex = Mathf.Clamp(m_StartingWaypointIndex, 0, m_Route.WaypointCount - 1);
        m_PingPongDirection = 1;
        m_WaitTimer = 0f;
        m_IsPatrolling = true;

        MoveToCurrentWaypoint();
    }

    [ContextMenu("Stop Patrol")]
    public void StopPatrol()
    {
        m_IsPatrolling = false;
        m_WaitTimer = 0f;

        if (m_Agent == null)
            return;

        m_Agent.ResetPath();
        m_Agent.velocity = Vector3.zero;
    }
    #endregion

    #region Private Methods
    private void MoveToCurrentWaypoint()
    {
        if (m_Agent == null || m_Route == null)
            return;

        Transform waypoint = m_Route.GetWaypoint(m_CurrentWaypointIndex);
        if (waypoint == null)
            return;

        m_Agent.SetDestination(waypoint.position);
    }

    private void AdvanceToNextWaypoint()
    {
        if (m_Route == null || m_Route.WaypointCount <= 0)
            return;

        if (m_Route.Mode == PatrolRoute.RouteMode.PingPong)
        {
            if (m_CurrentWaypointIndex >= m_Route.WaypointCount - 1)
                m_PingPongDirection = -1;
            else if (m_CurrentWaypointIndex <= 0)
                m_PingPongDirection = 1;
        }

        m_CurrentWaypointIndex = m_Route.GetNextIndex(m_CurrentWaypointIndex, m_PingPongDirection);
        MoveToCurrentWaypoint();
    }
    #endregion
}
