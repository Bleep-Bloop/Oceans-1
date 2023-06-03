using System.Collections;
using System.Collections.Generic;
using UnityEngine;
enum EnemyState
{
    Idle,
    Wandering,
    Patrolling,
    Attacking,
    AttackingCooldown,
    Searching,
    Observing,
    Alerting,
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
    [SerializeField] private float observerTurnSpeed = 0.5f;
    [SerializeField] public bool canDie;
    [SerializeField] private float spottedCooldownTime = 5; // ToDo: Iffy Name

    // Components //
    public BoxCollider wanderZone; // A BoxCollider used to mark the area an enemy will wander when in WanderingState.
    private FieldOfView fieldOfView;
    [SerializeField] private BoxCollider bc;

    // Patrol parts
    [SerializeField] private List<Transform> wayPoints;
    [SerializeField] private int _currentWaypointIndex = 0;
    [SerializeField] private int _currentLookTowardsIndex = 0;
    [SerializeField] private bool isPatrollingForwards = true;

    // Runtime //
    [SerializeField] private EnemyState defaultState; // The state the enemy returns too when not chasing/attacking. ToDo: Better comment
    [SerializeField] private EnemyState currentState;
    [SerializeField] private Vector3 currentMovementTarget;
    [SerializeField] private float attackCooldownTime = 5.0f; // Time spent in AttackCooldownState.
    private bool targetSpotted;
    bool coroutineRunning = false;
    [SerializeField] public RaycastHit meleeHit;
    [SerializeField] private float searchingStateTime = 5.0f;
    [SerializeField] private bool hasCollided = false;
    [SerializeField] private float timeBetweenCollisions = 0.5f;
    [SerializeField] private float observingDegrees = 90;

    // Observer work
    [SerializeField] private List<Transform> connectedEnemies;

    private IEnumerator changeStateCoroutine;

    // Debug //
    [SerializeField] private bool DEBUG_MODE; // Visualize wander point, ToDo: VisionCone, and whatever else can be shown.
    [SerializeField] private GameObject pointMarker; // PointMarker prefab used to visual transforms.


    [SerializeField] private LayerMask TargetLayerMask;

    [SerializeField] Transform currentTarget;

    Transform currentWaypoint;

    [SerializeField] private float knockbackForce = 1.2f;



    private void Awake()
    {
        fieldOfView = GetComponent<FieldOfView>();
        bc = GetComponent<BoxCollider>();
    }

    private void Start()
    {
        targetSpotted = false;
        currentState = defaultState;
        _currentWaypointIndex = 0;
        _currentLookTowardsIndex = 0;

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
            case EnemyState.Patrolling:
                PatrollingState();
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
            case EnemyState.Observing:
                ObservingState();
                break;
            case EnemyState.Alerting:
                AltertingState();
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

    /// <summary>
    /// Get the x and y value of a Vector3 as a Vector2
    /// </summary>
    /// <param name="vector3"></param>
    /// <returns>Vector2 holding incoming vector3's x and z values</returns>
    private Vector2 GetVector3XZ(Vector3 vector3)
    {
        Vector2 transformXZ = new Vector2(vector3.x, vector3.z);
        return transformXZ;
    }

    // Wandering
    /// <summary>
    /// Enemy moves towards a random point inside wanderZone.
    /// On arrival a new point is chosen.
    /// </summary>
    private void WanderingState()
    {

        // ToDo: Ignore Y when checking distance
        Vector2 currentXZ = GetVector3XZ(transform.position);
        Vector2 currentTargetXZ = GetVector3XZ(currentMovementTarget);

        // If lost sight of target and arrived at last seen position
        if (!currentTarget && Vector2.Distance(currentXZ, currentTargetXZ) < 0.1)
            ChangeState(EnemyState.Searching);


        // Find new wander point when necessary
        if (Vector2.Distance(currentXZ, currentTargetXZ) < 0.001 || currentMovementTarget == Vector3.zero)
        {
            currentMovementTarget = GetRandomPointInsideCollider(wanderZone);
            currentMovementTarget.y = transform.position.y; // Set Y position at enemy's height so they do not raise/lower. ToDo: Handle multiple levels/stairs   
        }

        // Move player towards wander point
        Move(currentMovementTarget);
        FaceMoveDirection();

        // Check for target
        CheckVision(fieldOfView);
        if(currentTarget)
        {
            ChangeState(EnemyState.Attacking);
        }

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
        if (coroutineRunning)
            StopAllCoroutines();

        // Set movement to currentTargets position
        if (currentTarget)
        {
            currentMovementTarget = currentTarget.transform.position;
        }

        // If still pursuing target and in melee reach; check for hit.
        if (currentTarget && Vector2.Distance(transform.position, currentMovementTarget) < meleeReach)
        {
            // If player's layer mask is hit
            if (Physics.SphereCast(transform.position, meleeRadius, transform.forward, out meleeHit, meleeReach, TargetLayerMask))
            { 
                // Damage player
                meleeHit.collider.gameObject.GetComponent<HealthComponent>().TakeDamage(1); 

                // On damage switch to AttackingCoolDown State.
                ChangeState(EnemyState.AttackingCooldown);
            }

        }

        // ToDo: Ignore Y when checking distance
        Vector2 currentXZ = new Vector2(transform.position.x, transform.position.z);
        Vector2 currentTargetXZ = new Vector2(currentMovementTarget.x, currentMovementTarget.z);

        // If lost sight of target and arrived at last seen position
        if (!currentTarget && Vector2.Distance(currentXZ, currentTargetXZ) < 0.1)
            ChangeState(EnemyState.Searching);

        // Ensure currentTarget will nullify when lost sight
        CheckVision(fieldOfView);

        Move(currentMovementTarget);
        FaceMoveDirection();

    }


    [SerializeField] private bool inAttackCooldown = false;
    private void AttackingCooldownState()
    {

        if (inAttackCooldown == false)
        {
            currentMovementTarget = FindSpotInCircle(transform, fieldOfView.viewRadius / 1.1f);

            inAttackCooldown = true;
        }

        Move(currentMovementTarget);
        FaceTargetDirection(currentTarget.position);

        changeStateCoroutine = ChangeStateAfterTime(defaultState, attackCooldownTime);
        StartCoroutine(changeStateCoroutine);

        CheckVision(fieldOfView);

    }

    // Searching
    private void SearchingState()
    {

        CheckVision(fieldOfView);
        if (currentTarget)
        {
            ChangeState(EnemyState.Attacking);
        }

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


    // Patrolling
    private void PatrollingState()
    {
        currentWaypoint = wayPoints[_currentWaypointIndex];

        // ToDo: Ignore Y when checking distance
        Vector2 currentXZ = GetVector3XZ(transform.position);
        Vector2 currentTargetXZ = GetVector3XZ(currentWaypoint.position);

        if (Vector2.Distance(currentXZ, currentTarget.position) < 0.01f)
        {

            if (isPatrollingForwards)
            {
                _currentWaypointIndex = (_currentWaypointIndex + 1) % wayPoints.Count;
            }
            else
            {
                if (_currentWaypointIndex == 0)
                {
                    _currentWaypointIndex = wayPoints.Count-1;
                }
                else
                {
                    _currentWaypointIndex--;
                }
            }

        }
        
        // Move towards currentWaypoint
       // transform.position = Vector3.MoveTowards(transform.position, currentWaypoint.position, movementSpeed * Time.deltaTime);
        Move(currentWaypoint.position);
        FaceTargetDirection(currentWaypoint.position);
        KnockBackCollision();

        CheckVision(fieldOfView);
        if (currentTarget)
        {
            ChangeState(EnemyState.Attacking);
        }
    }

    private void ReversePatrolDirection()
    {
        Debug.Log("Reverse Patrol Direction");
        isPatrollingForwards = !isPatrollingForwards;
        if(isPatrollingForwards)
            _currentWaypointIndex = (_currentWaypointIndex + 1) % wayPoints.Count;
        else
        {
            if(_currentWaypointIndex > 0)
                currentWaypoint = wayPoints[_currentWaypointIndex--];
            else
                currentWaypoint = wayPoints[2];
            
            
        }
    }

   

    private void KnockBackCollision()
    {
        RaycastHit collisionHit;

        if(hasCollided == false)
        {
            // p1 & p2 sphere on bottom and sphere on top
            if (Physics.BoxCast(transform.position, bc.size / 2, Vector3.forward, out collisionHit, transform.rotation, 0.5f) ||
                Physics.BoxCast(transform.position, bc.size / 2, Vector3.right, out collisionHit, transform.rotation, 0.5f) ||
                Physics.BoxCast(transform.position, bc.size / 2, -Vector3.forward, out collisionHit, transform.rotation, 0.5f) ||
                Physics.BoxCast(transform.position, bc.size / 2, -Vector3.right, out collisionHit, transform.rotation, 0.5f) ||
                Physics.BoxCast(transform.position, bc.size / 2, Vector3.up, out collisionHit, transform.rotation, 0.5f))
            {
                
                ReversePatrolDirection();
                if (collisionHit.collider.gameObject.GetComponent<PlayerStateMachine>())
                {
                    Debug.Log("Knock Back");
                    PlayerStateMachine collidedPlayerStateMachine = collisionHit.collider.gameObject.GetComponent<PlayerStateMachine>();

                    Vector3 knockbackVelocity = -collidedPlayerStateMachine.velocity;
                    knockbackVelocity.y += 15; // knockback kickup variable
                    knockbackVelocity.x = -25; 
                    knockbackVelocity.z = -25;

                    collidedPlayerStateMachine.velocity = (knockbackVelocity * knockbackForce);

                    //collidedPlayerStateMachine.velocity = knockbackVelocity * knockbackForce; // -collidedPlayerStateMachine.velocity * knockbackForce;
                    collisionHit.collider.gameObject.GetComponent<HealthComponent>().TakeDamage(1);
                    hasCollided = true;

                }
                
                // To prevent one object from triggering multiple times // ToDo: Save previous collision and then reset in ResetCollisionCheck
                Invoke("ResetCollisionCheck", timeBetweenCollisions);
            }
        }

    }

    private void ResetCollisionCheck()
    {
        hasCollided = false;
    }

    private void OnDrawGizmos()
    {
        // Draw the sphere used in attacking SphereCast.
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(this.transform.position + (this.transform.forward * meleeReach), meleeRadius);
        Gizmos.color = Color.green;
        if(bc)
        {
            Gizmos.DrawCube(this.transform.position, bc.size / 2);
        }

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
    /// ////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    /// </summary>
    // Observing State 
    protected void ObservingState()
    {

        Transform currentLookTowardsPoint = wayPoints[_currentWaypointIndex];

        if(wayPoints.Count >= 0)
        {
            Vector3 targetDirection = currentLookTowardsPoint.position - transform.position;

            float singleStep = observerTurnSpeed * Time.deltaTime;
            Vector3 newDirection = Vector3.RotateTowards(transform.forward, targetDirection, singleStep, 0.0f);
            transform.rotation = Quaternion.LookRotation(newDirection);

            // If facing
            if (Vector3.Angle(transform.forward, targetDirection) < 1)
            {
                _currentWaypointIndex = (_currentWaypointIndex + 1) % wayPoints.Count;
            }
        }

        CheckVision(fieldOfView);
        if(currentTarget) // ToDo: Rename currentTarget to inview or something I think it works better
        {
            ChangeState(EnemyState.Alerting);
        }

    }

    protected void AltertingState()
    {

        // rotate towards but stop if they camera is going to go past a certain point (unsure exactly how to go about that.
        if (currentTarget)
        {

            Transform sightedTarget = currentTarget.transform;
            Vector3 targetDirection = sightedTarget.position - transform.position;

            float singleStep = observerTurnSpeed * Time.deltaTime;
            Vector3 newDirection = Vector3.RotateTowards(transform.forward, targetDirection, singleStep, 0.0f);
            transform.rotation = Quaternion.LookRotation(newDirection);
        
            // Report last seen position to connected enemies
        
        }
        else
        {
            changeStateCoroutine = ChangeStateAfterTime(defaultState, spottedCooldownTime);
            StartCoroutine(changeStateCoroutine);
        }

        // Need to ensure when leaving sight current target is cleared.
        CheckVision(fieldOfView);

        // set currentTarget to all enemies. (Connect enemies in inspector?
    }

    /// <summary>
    /// Checks the field of view for visibile targets and triggers Attacking state if true.
    /// </summary>
    private void CheckVision(FieldOfView vision)
    {
        if (vision.visibleTargets.Count > 0)
        {
            SetCurrentTarget(vision.visibleTargets[0]);
            //ChangeState(EnemyState.Attacking); // Remove changing state here and set it in the given states
        }
        else
        {
            targetSpotted = false;
            SetCurrentTarget(null); // I dont think this is needed we want to save last seen location
        }
    }

    private void Move(Vector3 movementLocation)
    {
        // Maintain enemies altitude
        movementLocation.y = transform.position.y;
        
        transform.position = Vector3.MoveTowards(transform.position, movementLocation, movementSpeed * Time.deltaTime);
    }
    
    private void SetCurrentTarget(Transform target)
    {
        currentTarget = target;
    }
    
    public void Death()
    {
        ChangeState(EnemyState.Dying);
        
        transform.localScale = new Vector3(1, .5f, 1f);
        Vector3 newLocalPosition = transform.localPosition;
        newLocalPosition.y = .8f;
        transform.localPosition = newLocalPosition;

        Destroy(gameObject, 2.0f);
    }


}
