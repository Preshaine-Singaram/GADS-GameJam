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

    [Tooltip("If true, guard turns 180 degrees once when reaching each waypoint.")]
    [SerializeField] private bool m_TurnAroundAtWaypoint = true;

    [Header("Vision")]
    [Tooltip("Player transform to detect. If null, fallback uses Player tag lookup.")]
    [SerializeField] private Transform m_Player;

    [Tooltip("Tag used for fallback player lookup if Player is not assigned.")]
    [SerializeField] private string m_PlayerTag = "Player";

    [Tooltip("Optional eye point for line-of-sight checks.")]
    [SerializeField] private Transform m_EyePoint;

    [Tooltip("Maximum distance at which this NPC can see the player.")]
    [SerializeField] private float m_ViewDistance = 14f;

    [Tooltip("Half-angle of field of view cone in degrees.")]
    [SerializeField, Range(1f, 180f)] private float m_ViewAngle = 65f;

    [Tooltip("Layers used for line-of-sight raycasts.")]
    [SerializeField] private LayerMask m_VisibilityMask = ~0;

    [Tooltip("If true, NPC chases the player once detected.")]
    [SerializeField] private bool m_ChaseWhenPlayerSeen = true;

    [Tooltip("Optional area collider: NPC only starts chase if player is inside this zone.")]
    [SerializeField] private Collider m_ChaseArea;

    [Tooltip("If true, guard must see player while player is inside Chase Area.")]
    [SerializeField] private bool m_RequirePlayerInsideChaseArea = true;

    [Tooltip("Patrol targets (KillablePatrolTarget). If the guard sees any of them dead (lying down), they chase the player globally, ignoring Chase Area until the chase ends.")]
    [SerializeField] private KillablePatrolTarget[] m_DeadBodyTargetsToWatch;

    [Tooltip("Extra tolerance for chase-area containment checks.")]
    [SerializeField, Range(0f, 2f)] private float m_ChaseAreaPadding = 0.25f;

    [Tooltip("Seconds NPC continues chasing after player leaves line-of-sight.")]
    [SerializeField] private float m_LostSightGraceSeconds = 2f;

    [Tooltip("Stopping distance used while chasing the player.")]
    [SerializeField] private float m_ChaseStoppingDistance = 1.4f;

    [Header("Catch / Game Over")]
    [Tooltip("Horizontal distance at or below which the guard counts as catching the player while chasing. Also compared against Chase Stopping Distance so the guard can register a catch when the agent stops near the player.")]
    [SerializeField] private float m_CatchContactDistance = 1.55f;

    [Header("Debug")]
    [Tooltip("Logs guard detection/chase state changes to Console.")]
    [SerializeField] private bool m_EnableDebugLogs = false;

    [Tooltip("Draws vision ray and state helpers in Scene view.")]
    [SerializeField] private bool m_DrawDebugGizmos = true;
    #endregion

    #region Private Fields
    private int m_CurrentWaypointIndex = -1;
    private int m_PingPongDirection = 1;
    private float m_WaitTimer;
    private bool m_IsPatrolling;
    private bool m_IsChasingPlayer;
    private float m_TimeSinceLastSight;
    private float m_DefaultStoppingDistance;
    private bool m_HasTurnedAtCurrentWaypoint;
    private bool m_DebugPlayerFound;
    private bool m_DebugInArea;
    private bool m_DebugCanSeePlayer;
    private bool m_DebugEligible;
    private bool m_DebugCanSeeDeadPatrolTarget;
    private bool m_CanSeeDeadPatrolTargetThisFrame;
    private bool m_ChaseTriggeredByDeadBody;
    private string m_LastDebugState = string.Empty;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (m_Agent == null)
            m_Agent = GetComponent<NavMeshAgent>();

        if (m_Agent != null)
        {
            m_Agent.stoppingDistance = Mathf.Max(0f, m_ArrivalDistance);
            m_DefaultStoppingDistance = m_Agent.stoppingDistance;
        }

        ResolvePlayerReference();
    }

    private void Start()
    {
        if (!m_AutoStartPatrol)
            return;

        BeginPatrol();
    }

    private void Update()
    {
        HandleVisionAndChase();

        TryCatchPlayerWhileChasing();

        if (m_IsChasingPlayer)
            return;

        if (!m_IsPatrolling || m_Agent == null || m_Route == null || m_Route.WaypointCount <= 0)
            return;

        if (m_Agent.pathPending)
            return;

        if (m_Agent.remainingDistance > Mathf.Max(m_ArrivalDistance, m_Agent.stoppingDistance))
            return;

        if (!m_HasTurnedAtCurrentWaypoint && m_TurnAroundAtWaypoint)
        {
            transform.Rotate(0f, 180f, 0f, Space.Self);
            m_HasTurnedAtCurrentWaypoint = true;
        }

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
        m_IsChasingPlayer = false;
        m_TimeSinceLastSight = 0f;
        m_HasTurnedAtCurrentWaypoint = false;
        m_ChaseTriggeredByDeadBody = false;

        if (m_Agent != null)
            m_Agent.stoppingDistance = m_DefaultStoppingDistance;

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
    private void HandleVisionAndChase()
    {
        if (m_Agent == null)
            return;

        if (m_Player == null)
            ResolvePlayerReference();

        m_CanSeeDeadPatrolTargetThisFrame = CanSeeDeadPatrolTarget();
        m_DebugCanSeeDeadPatrolTarget = m_CanSeeDeadPatrolTargetThisFrame;

        m_DebugPlayerFound = m_Player != null;
        m_DebugInArea = IsPlayerInsideChaseArea();
        bool canSeePlayer = CanSeePlayer();
        m_DebugCanSeePlayer = canSeePlayer;
        m_DebugEligible = m_ChaseWhenPlayerSeen && IsPlayerEligibleForChase();
        LogDebugState();

        if (canSeePlayer)
        {
            m_TimeSinceLastSight = 0f;

            if (m_ChaseWhenPlayerSeen)
            {
                if (!IsPlayerEligibleForChase())
                    return;

                if (!m_IsChasingPlayer)
                    StartChasingPlayer();

                UpdateChaseDestination();
            }

            return;
        }

        if (!m_IsChasingPlayer && m_CanSeeDeadPatrolTargetThisFrame && m_ChaseWhenPlayerSeen)
        {
            StartChasingPlayer();
            UpdateChaseDestination();
            return;
        }

        if (!m_IsChasingPlayer)
            return;

        if (!IsPlayerEligibleForChase())
        {
            StopChasingAndResumePatrol();
            return;
        }

        m_TimeSinceLastSight += Time.deltaTime;
        if (m_TimeSinceLastSight < m_LostSightGraceSeconds)
        {
            UpdateChaseDestination();
            return;
        }

        StopChasingAndResumePatrol();
    }

    private void StartChasingPlayer()
    {
        m_IsChasingPlayer = true;
        m_IsPatrolling = false;
        m_WaitTimer = 0f;
        m_TimeSinceLastSight = 0f;
        m_Agent.stoppingDistance = Mathf.Max(0f, m_ChaseStoppingDistance);

        if (m_CanSeeDeadPatrolTargetThisFrame)
            m_ChaseTriggeredByDeadBody = true;
    }

    private void StopChasingAndResumePatrol()
    {
        m_IsChasingPlayer = false;
        m_TimeSinceLastSight = 0f;
        m_ChaseTriggeredByDeadBody = false;
        m_Agent.stoppingDistance = m_DefaultStoppingDistance;

        if (m_Route != null && m_Route.WaypointCount > 0)
            BeginPatrol();
    }

    private void UpdateChaseDestination()
    {
        if (m_Player == null || m_Agent == null)
            return;

        m_Agent.SetDestination(m_Player.position);
    }

    private void TryCatchPlayerWhileChasing()
    {
        if (!m_IsChasingPlayer || m_Player == null)
            return;

        Vector3 guardXZ = transform.position;
        guardXZ.y = 0f;
        Vector3 playerXZ = m_Player.position;
        playerXZ.y = 0f;

        float catchRadius = Mathf.Max(0.05f, m_CatchContactDistance, m_ChaseStoppingDistance + 0.12f);
        if ((guardXZ - playerXZ).sqrMagnitude > catchRadius * catchRadius)
            return;

        PlayerDeathScreen.TriggerPlayerDeath();
    }

    private bool CanSeePlayer()
    {
        if (m_Player == null)
            return false;

        Vector3 origin = m_EyePoint != null ? m_EyePoint.position : transform.position + Vector3.up * 1.6f;
        Vector3 playerTargetPoint = GetPlayerTargetPoint();
        Vector3 toPlayer = playerTargetPoint - origin;
        float sqrDistance = toPlayer.sqrMagnitude;
        float maxDistance = Mathf.Max(0.01f, m_ViewDistance);
        if (sqrDistance > maxDistance * maxDistance)
            return false;

        Vector3 dirToPlayer = toPlayer.normalized;
        float angleToPlayer = Vector3.Angle(transform.forward, dirToPlayer);
        if (angleToPlayer > m_ViewAngle)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(origin, dirToPlayer, maxDistance, m_VisibilityMask, QueryTriggerInteraction.Ignore);
        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null)
                continue;

            // Ignore this NPC's own colliders.
            if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
                continue;

            // Ignore chase-area collider so zone volumes do not block LOS.
            if (m_ChaseArea != null && hitCollider == m_ChaseArea)
                continue;

            Transform hitTransform = hits[i].transform;
            if (hitTransform == m_Player || hitTransform.IsChildOf(m_Player))
                return true;

            // First valid non-player hit blocks line of sight.
            return false;
        }

        return false;
    }

    private bool CanSeeDeadPatrolTarget()
    {
        if (m_DeadBodyTargetsToWatch == null || m_DeadBodyTargetsToWatch.Length == 0)
            return false;

        for (int i = 0; i < m_DeadBodyTargetsToWatch.Length; i++)
        {
            KillablePatrolTarget target = m_DeadBodyTargetsToWatch[i];
            if (target == null || !target.IsDead)
                continue;

            if (CanSeePointOnTransform(target.transform, GetDeadPatrolTargetSightPoint(target)))
                return true;
        }

        return false;
    }

    private static Vector3 GetDeadPatrolTargetSightPoint(KillablePatrolTarget _target)
    {
        if (_target == null)
            return Vector3.zero;

        Collider[] colliders = _target.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c != null && c.enabled)
                return c.bounds.center;
        }

        return _target.transform.position + Vector3.up * 0.35f;
    }

    private bool CanSeePointOnTransform(Transform _subjectRoot, Vector3 _worldPoint)
    {
        Vector3 origin = m_EyePoint != null ? m_EyePoint.position : transform.position + Vector3.up * 1.6f;
        Vector3 toPoint = _worldPoint - origin;
        float sqrDistance = toPoint.sqrMagnitude;
        float maxDistance = Mathf.Max(0.01f, m_ViewDistance);
        if (sqrDistance > maxDistance * maxDistance)
            return false;

        Vector3 dirToPoint = toPoint.normalized;
        if (Vector3.Angle(transform.forward, dirToPoint) > m_ViewAngle)
            return false;

        // Collide with triggers so corpses that only have trigger colliders (non-trigger disabled on death) still block LOS to walls behind them.
        RaycastHit[] hits = Physics.RaycastAll(origin, dirToPoint, Mathf.Sqrt(sqrDistance), m_VisibilityMask, QueryTriggerInteraction.Collide);
        if (hits == null || hits.Length == 0)
            return true;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (hitCollider == null)
                continue;

            if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform))
                continue;

            if (m_ChaseArea != null && hitCollider == m_ChaseArea)
                continue;

            Transform hitTransform = hits[i].transform;
            if (hitTransform == _subjectRoot || hitTransform.IsChildOf(_subjectRoot))
                return true;

            return false;
        }

        return true;
    }

    private void ResolvePlayerReference()
    {
        if (m_Player != null || string.IsNullOrWhiteSpace(m_PlayerTag))
            return;

        GameObject playerObject = GameObject.FindWithTag(m_PlayerTag);
        if (playerObject != null)
            m_Player = playerObject.transform;
    }

    private bool IsPlayerEligibleForChase()
    {
        if (m_ChaseTriggeredByDeadBody || m_CanSeeDeadPatrolTargetThisFrame)
            return true;

        if (!m_RequirePlayerInsideChaseArea)
            return true;

        // If no chase area is assigned, do not block chase.
        if (m_ChaseArea == null)
            return true;

        if (m_Player == null)
            return false;

        return IsAnyPlayerSampleInsideChaseArea();
    }

    private bool IsPlayerInsideChaseArea()
    {
        if (!m_RequirePlayerInsideChaseArea)
            return true;

        if (m_ChaseArea == null || m_Player == null)
            return false;

        return IsAnyPlayerSampleInsideChaseArea();
    }

    private static bool IsPointInsideCollider(Collider _collider, Vector3 _point)
    {
        Vector3 closestPoint = _collider.ClosestPoint(_point);
        return (closestPoint - _point).sqrMagnitude < 0.0025f;
    }

    private bool IsAnyPlayerSampleInsideChaseArea()
    {
        if (m_ChaseArea == null || m_Player == null)
            return false;

        Vector3 playerCenter = GetPlayerTargetPoint();
        Vector3 playerPivot = m_Player.position;
        Vector3 playerFeet = playerPivot;

        if (m_Player.TryGetComponent(out Collider playerCollider))
            playerFeet = playerCollider.bounds.center - Vector3.up * playerCollider.bounds.extents.y;

        return IsPointInsideOrNearCollider(m_ChaseArea, playerPivot, m_ChaseAreaPadding) ||
               IsPointInsideOrNearCollider(m_ChaseArea, playerCenter, m_ChaseAreaPadding) ||
               IsPointInsideOrNearCollider(m_ChaseArea, playerFeet, m_ChaseAreaPadding);
    }

    private static bool IsPointInsideOrNearCollider(Collider _collider, Vector3 _point, float _padding)
    {
        Vector3 closestPoint = _collider.ClosestPoint(_point);
        float maxDistance = Mathf.Max(0f, _padding);
        return (closestPoint - _point).sqrMagnitude <= maxDistance * maxDistance;
    }

    private Vector3 GetPlayerTargetPoint()
    {
        if (m_Player == null)
            return Vector3.zero;

        if (m_Player.TryGetComponent(out Collider playerCollider))
            return playerCollider.bounds.center;

        return m_Player.position + Vector3.up * 0.9f;
    }

    private void LogDebugState()
    {
        if (!m_EnableDebugLogs)
            return;

        string state = $"Found:{m_DebugPlayerFound} InArea:{m_DebugInArea} See:{m_DebugCanSeePlayer} SeeDead:{m_DebugCanSeeDeadPatrolTarget} Eligible:{m_DebugEligible} Chasing:{m_IsChasingPlayer} Patrol:{m_IsPatrolling}";
        if (state == m_LastDebugState)
            return;

        m_LastDebugState = state;
        Debug.Log($"[{name}] {state}", this);
    }

    private void OnDrawGizmosSelected()
    {
        if (!m_DrawDebugGizmos)
            return;

        Vector3 origin = m_EyePoint != null ? m_EyePoint.position : transform.position + Vector3.up * 1.6f;
        Vector3 target = m_Player != null ? GetPlayerTargetPoint() : origin + transform.forward * Mathf.Max(1f, m_ViewDistance);
        bool canSee = Application.isPlaying && m_DebugCanSeePlayer;
        Gizmos.color = canSee ? Color.green : Color.red;
        Gizmos.DrawLine(origin, target);
        Gizmos.DrawWireSphere(origin, Mathf.Max(0.5f, m_ViewDistance));
    }

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

        m_HasTurnedAtCurrentWaypoint = false;

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
