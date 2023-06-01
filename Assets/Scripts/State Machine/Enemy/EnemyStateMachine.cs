using System.Collections;
using System.Collections.Generic;
using UnityEngine;
enum EnemyState
{
    Idle,
    Wandering,
    Attacking,
    AttackingCooldown,
}
public class EnemyStateMachine : MonoBehaviour
{

    // Properties //
    [SerializeField] private float movementSpeed = 1;

    // Components //
    public BoxCollider wanderZone; // A BoxCollider used to mark the area an enemy will wander when in WanderingState.

    // Runtime //
    [SerializeField] private EnemyState currentState;
    [SerializeField] private Vector3 currentMovementTarget;

    // Debug //
    [SerializeField] private bool DEBUG_MODE; // Visualize wander point, ToDo: VisionCone, and whatever else can be shown.
    [SerializeField] private GameObject pointMarker; // PointMarker prefab used to visual transforms.


    private void Awake()
    {  
    }

    private void Start()
    {
        currentState = EnemyState.Wandering;
    }

    private void Update()
    {
        switch (currentState)
        {
            case EnemyState.Idle:
                IdleState();
                break;
            case EnemyState.Wandering:
                WanderingState();
                break;
            case EnemyState.Attacking:
                AttackingState();
                break;
            case EnemyState.AttackingCooldown:
                AttackingCooldownState();
                break;
            default:
                break;
        }
    }


// Idle
    private void IdleState()
    {

    }


// Wandering
    /// <summary>
    /// Player will find a random point inside randomPointBox and move towards it.
    /// On arrival they will find a new point
    /// </summary>
    private void WanderingState()
    {

        // Find new wander point when necessary
        if(Vector3.Distance(transform.position, currentMovementTarget) < 0.001 || currentMovementTarget == Vector3.zero)
        {
            currentMovementTarget = GetRandomPointInsideCollider(wanderZone);
            currentMovementTarget.y = transform.position.y; // Set Y position at enemy's height so they do not raise/lower. ToDo: Handle multiple levels/stairs   
        }

        transform.position = Vector3.MoveTowards(transform.position, currentMovementTarget, movementSpeed * Time.deltaTime);
    }

    // ToDo: Check point is not inside another object
    // Used to find spot inside boxCollider to move enemy too
    public Vector3 GetRandomPointInsideCollider(BoxCollider boxCollider)
    {
        Vector3 extents = boxCollider.size / 2f;
        Vector3 point = new Vector3(
            Random.Range(-extents.x, extents.x),
            Random.Range(-extents.y, extents.y),
            Random.Range(-extents.z, extents.z)
        );

        if(DEBUG_MODE)
        {
            // debugPlacementPoint will hover above the wander point 
            Vector3 debugPlacementPoint = point;
            debugPlacementPoint.y += 3;

            if (DEBUG_MODE)
            {
                GameObject currentMarker = Instantiate(pointMarker, debugPlacementPoint, Quaternion.identity); // Debug - Show location
                Destroy(currentMarker, 3);
            }       
        }
                     
        return boxCollider.transform.TransformPoint(point);
    }


// Attacking
    private void AttackingState()
    {

    }

    private void AttackingCooldownState()
    {

    }

}
