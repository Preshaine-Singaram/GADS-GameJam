using UnityEngine;

/// <summary>
/// Defines a reusable patrol route using ordered waypoint transforms.
/// </summary>
public class PatrolRoute : MonoBehaviour
{
    #region Enums
    public enum RouteMode
    {
        Loop = 0,
        PingPong = 1,
        Random = 2
    }
    #endregion

    #region Inspector
    [Header("Route")]
    [Tooltip("Ordered waypoint list used by NPC patrol controllers.")]
    [SerializeField] private Transform[] m_Waypoints;

    [Tooltip("How this route advances between points.")]
    [SerializeField] private RouteMode m_RouteMode = RouteMode.Loop;

    [Header("Debug")]
    [Tooltip("Draw route lines and waypoint spheres in Scene view.")]
    [SerializeField] private bool m_DrawGizmos = true;

    [Tooltip("Color used for route gizmos.")]
    [SerializeField] private Color m_GizmoColor = Color.cyan;
    #endregion

    #region Public Properties
    public int WaypointCount => m_Waypoints != null ? m_Waypoints.Length : 0;
    public RouteMode Mode => m_RouteMode;
    #endregion

    #region Public Methods
    public Transform GetWaypoint(int _index)
    {
        if (m_Waypoints == null || _index < 0 || _index >= m_Waypoints.Length)
            return null;

        return m_Waypoints[_index];
    }

    public int GetNextIndex(int _currentIndex, int _direction)
    {
        int count = WaypointCount;
        if (count <= 0)
            return -1;

        if (count == 1)
            return 0;

        switch (m_RouteMode)
        {
            case RouteMode.Loop:
                return (_currentIndex + 1 + count) % count;

            case RouteMode.PingPong:
                if (_direction >= 0)
                {
                    if (_currentIndex >= count - 1)
                        return count - 2;
                    return _currentIndex + 1;
                }
                else
                {
                    if (_currentIndex <= 0)
                        return 1;
                    return _currentIndex - 1;
                }

            case RouteMode.Random:
                int next = _currentIndex;
                int guard = 0;
                while (next == _currentIndex && guard < 16)
                {
                    next = Random.Range(0, count);
                    guard++;
                }
                return next;

            default:
                return (_currentIndex + 1 + count) % count;
        }
    }
    #endregion

    #region Unity Lifecycle
    private void OnDrawGizmos()
    {
        if (!m_DrawGizmos || m_Waypoints == null || m_Waypoints.Length == 0)
            return;

        Gizmos.color = m_GizmoColor;

        for (int i = 0; i < m_Waypoints.Length; i++)
        {
            Transform wp = m_Waypoints[i];
            if (wp == null)
                continue;

            Gizmos.DrawSphere(wp.position, 0.2f);

            int nextIndex = i + 1;
            if (m_RouteMode == RouteMode.Loop && nextIndex >= m_Waypoints.Length)
                nextIndex = 0;

            if (nextIndex < m_Waypoints.Length)
            {
                Transform nextWp = m_Waypoints[nextIndex];
                if (nextWp != null)
                    Gizmos.DrawLine(wp.position, nextWp.position);
            }
        }
    }
    #endregion
}
