using System.Collections;
using System.Collections.Generic;
using UnityEngine;
enum EnemyState
{
    Idle,
    Wandering,
    Attacking,
    AttackingCooldown,
    Searching,
    Dying,
}

[RequireComponent(typeof(FieldOfView))]
public class EnemyStateMachine : MonoBehaviour
{

    // Properties //
    [SerializeField] private float movementSpeed = 1;
    [SerializeField] private float LookRotationDampFactor = 8.0f;
    [SerializeField] private float meleeReach = 1f;
    [SerializeField] private float meleeRadius = 0.5f;
    [SerializeField] public bool canDie;

    // Components //
    public BoxCollider wanderZone; // A BoxCollider used to mark the area an enemy will wander when in WanderingState.
    private FieldOfView fieldOfView;

    // Runtime //
    [SerializeField] private EnemyState defaultState; // The state the enemy returns too when not chasing/attacking. ToDo: Better comment
    [SerializeField] private EnemyState currentState;
    [SerializeField] private Vector3 currentMovementTarget;
    [SerializeField] private float attackCooldownTime = 5.0f; // Time spent in AttackCooldownState.
    private bool targetSpotted;
    bool coroutineRunning = false;
    [SerializeField] public RaycastHit meleeHit;
    [SerializeField] private float searchingStateTime = 5.0f;

    private IEnumerator changeStateCoroutine;

    // Debug //
    [SerializeField] private bool DEBUG_MODE; // Visualize wander point, ToDo: VisionCone, and whatever else can be shown.
    [SerializeField] private GameObject pointMarker; // PointMarker prefab used to visual transforms.


    [SerializeField] private LayerMask TargetLayerMask;

    [SerializeField] Transform currentTarget;


    private void Awake()
    {
        fieldOfView = GetComponent<FieldOfView>();
    }

    private void Start()
    {
        targetSpotted = false;
        currentState = defaultState;
    }


    /// <summary>
    /// Changes currentState to newState
    /// </summary>
    private void ChangeState(EnemyState newState)
    {
        currentState = newState;
    }

    private IEnumerator ChangeStateAfterTime(EnemyState newState, float waitTime)
    {
        coroutineRunning = true;
        yield return new WaitForSeconds(waitTime);
        ChangeState(newState);
        inAttackCooldown = false;
        StopCoroutine(changeStateCoroutine);
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
            case EnemyState.Searching:
                SearchingState();
                break;
            case EnemyState.Dying:
                Death();
                break;
            default:
                IdleState();
                break;
        }

    }

// Idle
    private void IdleState()
    {

    }

// Wandering
    /// <summary>
    /// Enemy moves towards a random point inside wanderZone.
    /// On arrival a new point is chosen.
    /// </summary>
    private void WanderingState()
    {

        // Find new wander point when necessary
        if (Vector3.Distance(transform.position, currentMovementTarget) < 0.001 || currentMovementTarget == Vector3.zero)
        {
            currentMovementTarget = GetRandomPointInsideCollider(wanderZone);
            currentMovementTarget.y = transform.position.y; // Set Y position at enemy's height so they do not raise/lower. ToDo: Handle multiple levels/stairs   
        }

        // Move player towards wander point
        Move(currentMovementTarget);
        FaceMoveDirection();

        // Check for target
        CheckVision(fieldOfView);

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

        // Debug - Show location
        if (DEBUG_MODE)
        {
            // debugPlacementPoint will hover above the wander point 
            Vector3 debugPlacementPoint = point;
            debugPlacementPoint.y += 3;

            GameObject currentMarker = Instantiate(pointMarker, debugPlacementPoint, Quaternion.identity); 
            Destroy(currentMarker, 3);
        }

        return boxCollider.transform.TransformPoint(point);

    }

// Attacking
    private void AttackingState()
    {

        // Ensure player is not taken out of AttackingState if spotted while in SearchingState.
        if(coroutineRunning)
            StopAllCoroutines();

        // Enemy has vision of target, if sight is lost move to last seen location.
        if(fieldOfView.visibleTargets.Count > 0)
        {
            targetSpotted = true;
            currentMovementTarget = fieldOfView.visibleTargets[0].position;
        }
        else
        {
            targetSpotted = false;
        }
           
        // Attack target 
        if (targetSpotted && Vector2.Distance(transform.position, currentMovementTarget) < meleeReach) 
        {

            // Check if enemy will hit target.
            if(Physics.SphereCast(transform.position, meleeRadius, transform.forward, out meleeHit, meleeReach, TargetLayerMask))
            {
                // On collision with target switch to AttackingCoolDown State.
                ChangeState(EnemyState.AttackingCooldown);
            }

        }
        // If the enemy has arrived to last seen location and they cannot see target, move to searching state.
        else if (!targetSpotted && Vector2.Distance(transform.position, currentMovementTarget) < 0.01)
        {
            ChangeState(EnemyState.Searching);
        }

        //transform.position = Vector3.MoveTowards(transform.position, currentMovementTarget, movementSpeed * Time.deltaTime);
        // Ensure enemy is always facing target.
        Move(currentMovementTarget);
        FaceMoveDirection();
    
    }

    private void OnDrawGizmos()
    {
        // Draw the sphere used in attacking SphereCast.
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(this.transform.position + (this.transform.forward * meleeReach), meleeRadius);
    }

    [SerializeField] private bool inAttackCooldown = false;
    private void AttackingCooldownState()
    {

        if(inAttackCooldown == false)
        {
            currentMovementTarget = FindSpotInCircle(transform, fieldOfView.viewRadius / 1.1f);
            
            inAttackCooldown = true;
        }

        Move(currentMovementTarget);
        FaceTargetDirection(currentTarget.position);

        changeStateCoroutine = ChangeStateAfterTime(defaultState, attackCooldownTime);
        StartCoroutine(changeStateCoroutine);


    }

    private void SearchingState()
    {

        changeStateCoroutine = ChangeStateAfterTime(defaultState, searchingStateTime);
        StartCoroutine(changeStateCoroutine);

        // Spin

        // WIP - Trying to get enemy to randomly spin around checking area 
        #region Attempt At Random Spinning Checks
        /*
        if (searchingPointSet == false)
        {

            // Find point and get rotation
            targetPoint = FindSpotInCircleAroundLocation(transform, fieldOfView.viewRadius);
            searchingTargetRotation = Quaternion.LookRotation(targetPoint, Vector3.up);

            searchingPointSet = true;
        }

        Vector3 dirFromAtoB = (transform.position - targetPoint).normalized;
        float dotProd = Vector3.Dot(dirFromAtoB, targetPoint);

        // Object is pre much looking at spot so find new spot
        if (dotProd > 0.1)
        {
            searchingPointSet = false;
        }
        else
        {
            // or else just keep rotating
            transform.rotation = Quaternion.Slerp(transform.rotation, searchingTargetRotation, Time.deltaTime * LookRotationDampFactor);
            searchingPointSet = true;
        }*/
        #endregion



    }

    // Find a random point in circle around transform
    private Vector3 FindSpotInCircle(Transform transform, float circleRadius)
    {
        Vector2 randomPointInCircle = transform.position + Random.insideUnitSphere * circleRadius * 0.5f; // 0.5 - Half the radius
        Vector3 randomCirclePoint = new Vector3();

        randomCirclePoint.x = randomPointInCircle.x;
        randomCirclePoint.y = base.transform.position.y;
        randomCirclePoint.z = randomPointInCircle.y;

        // Debug - Show location
        if (DEBUG_MODE)
        {
            GameObject currentMarker = Instantiate(pointMarker, randomCirclePoint, Quaternion.identity);
            Destroy(currentMarker, 1);
        }

        return randomCirclePoint;
   
    }
    

    // Rotate the enemy to face towards their currentMovementTarget
    protected void FaceMoveDirection()
    {
        Vector3 targetPoint = new Vector3(currentMovementTarget.x, transform.position.y, currentMovementTarget.z) - transform.position;
        Quaternion targetRotation = Quaternion.LookRotation(targetPoint, Vector3.up); // ToDo: Check thrown warning

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * LookRotationDampFactor);
    }

    // Rotate the enemy to face towards targetPosition
    protected void FaceTargetDirection(Vector3 targetPosition)
    {
        Vector3 targetPoint = new Vector3(targetPosition.x, transform.position.y, targetPosition.z) - transform.position;
        Quaternion targetRotation = Quaternion.LookRotation(targetPoint, Vector3.up); // ToDo: Check thrown warning

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * LookRotationDampFactor);
    }


    /// <summary>
    /// Checks the field of view for visibile targets and triggers Attacking state if true.
    /// </summary>
    private void CheckVision(FieldOfView vision)
    {
        if (vision.visibleTargets.Count > 0)
        {
            SetCurrentTarget(vision.visibleTargets[0]);
            ChangeState(EnemyState.Attacking);
        }
        else
        {
            targetSpotted = false;
            SetCurrentTarget(null);
        }
    }

    private void Move(Vector3 movementLocation)
    {
        transform.position = Vector3.MoveTowards(transform.position, movementLocation, movementSpeed * Time.deltaTime);
    }
    
    private void SetCurrentTarget(Transform target)
    {
        currentTarget = target;
    }
    
    public void Death()
    {
        transform.localScale = new Vector3(1, .5f, 1f);
        transform.localPosition = new Vector3(0, 0.8f, 0);
        Destroy(gameObject, 2.0f);
    }


}
