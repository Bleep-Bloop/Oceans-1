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

    [Header("Properties")]

    [Tooltip("The state returned to when not attacking")]
    [SerializeField] private EnemyState defaultState;
    [SerializeField] private EnemyState currentState;

    [SerializeField] private List<Transform> wayPoints;
    [SerializeField] private List<EnemyStateMachine> connectedEnemies;

    [Tooltip("LayerMash used to find melee hit")]
    [SerializeField] private LayerMask TargetLayerMask;

    [Header("Components")]
    [Tooltip("This BoxCollider marks the area movementTargets can be set in WanderingState")]
    [SerializeField] private BoxCollider wanderZone;
    private FieldOfView fieldOfView;
    [Tooltip("This BoxCollider is used for collision with object")]
    [SerializeField] private BoxCollider boxCollider;

    [Header("Movement")]
    [SerializeField] private float movementSpeed = 1;
    [SerializeField] private float rotationDampFactor = 8.0f;
    Transform currentTarget; // The point the object is moving to.
    Transform currentWaypoint; // Current waypoint used in Observing and Patrolling states.
    private int _currentWaypointIndex = 0;

    // State Timers
    [SerializeField] private float attackCooldownTime = 5.0f;   // Time spent in AttackCooldownState.
    [SerializeField] private float searchingStateTime = 5.0f;   // Time spent in SearchingState.
    [SerializeField] private float spottedCooldownTime = 5.0f;   // Time spent in AlertingState after losing vision.
    [SerializeField] private float timeBetweenCollisions = 0.5f;

    [Header("Combat")]
    [SerializeField] private float meleeReach = 1f;
    [SerializeField] private float meleeRadius = 0.5f;
    [SerializeField] private float knockbackForce = 1.2f;
    [SerializeField] private bool canDie;

    // ToDo: Editor script to hide certain properties depending on enemy type
    [Header("Observer Properties")]
    [SerializeField] private float observerPanningSpeed = 0.5f;

    // Runtime
    private Vector3 currentMovementTarget;
    private bool hasCollided = false;
    private RaycastHit meleeHit;
    private bool inAttackCooldown = false;
    private bool isPatrollingForwards = true;

    private bool coroutineRunning = false;

    [Header("Debugging")]
    [SerializeField] private bool DEBUG_MODE;
    [Tooltip("Collision-less prefab used to visualize points")]
    [SerializeField] private GameObject pointMarker;

    private IEnumerator changeStateCoroutine;

    private void Awake()
    {
        fieldOfView = GetComponent<FieldOfView>();
        boxCollider = GetComponent<BoxCollider>();
    }

    private void Start()
    {
        currentState = defaultState;
        _currentWaypointIndex = 0;

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
                AlertingState();
                break;
            default:
                IdleState();
                break;
        }

    }

    ///// STATES /////
    #region States

    // Idle State //
    /// <summary>
    /// Enemy stands still searching directly infront of them.
    /// </summary>
    private void IdleState()
    {
        if (currentTarget)
            ChangeState(EnemyState.Attacking);

        // Ensure currentTarget is cleared when target leaves sight.
        CheckVision(fieldOfView);
    }


    // Wandering State //
    /// <summary>
    /// Enemy moves towards a random point inside wanderZone.
    /// On arrival a new point is chosen.
    /// </summary>
    private void WanderingState()
    {

        Vector2 currentXZ = GetVector3XZ(transform.position);
        Vector2 currentTargetXZ = GetVector3XZ(currentMovementTarget);

        // If lost sight of target and arrived at last seen position
        if (!currentTarget && Vector2.Distance(currentXZ, currentTargetXZ) < 0.1)
            ChangeState(EnemyState.Searching);

        // Find new wander point when necessary
        if (Vector2.Distance(currentXZ, currentTargetXZ) < 0.001 || currentMovementTarget == Vector3.zero)
            currentMovementTarget = GetRandomPointInsideCollider(wanderZone);

        // Move player towards wander point
        Move(currentMovementTarget);
        FaceMoveDirection();

        // Search for target
        CheckVision(fieldOfView);
        if (currentTarget)
            ChangeState(EnemyState.Attacking);
    }


    // Attacking State//
    /// <summary>
    /// Enemy chases towards currentTarget's last seen position.
    /// When within meleeReach a spherecast checks for hit.
    /// </summary>
    private void AttackingState()
    {

        // Ensure enemy is not taken out of AttackingState if spotted while in SearchingState.
        if (coroutineRunning)
            StopAllCoroutines();

        // Set movement to currentTargets position
        if (currentTarget)
            currentMovementTarget = currentTarget.transform.position;

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

        Vector2 currentXZ = GetVector3XZ(transform.position);
        Vector2 currentTargetXZ = GetVector3XZ(currentMovementTarget);

        // If lost sight of target and arrived at last seen position
        if (!currentTarget && Vector2.Distance(currentXZ, currentTargetXZ) < 0.1)
            ChangeState(EnemyState.Searching);

        // Ensure currentTarget is cleared when target leaves sight.
        CheckVision(fieldOfView);

        Move(currentMovementTarget);
        FaceMoveDirection();
    }


    // Attacking Cooldown State //
    /// <summary>
    /// Enemy picks a random location around target and moves there.
    /// Changes state to defaultState after attackCooldownTime has elapsed.
    /// </summary>
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

        // Ensure currentTarget is cleared when target leaves sight.
        CheckVision(fieldOfView);
    }


    // Searching State //
    /// <summary>
    /// Enemy randomly rotates searching area around themselves.
    /// Returns to defaultState after searchingStateTime has elapsed.
    /// ToDo: Implement random rotation
    /// </summary>
    private void SearchingState()
    {
        // Search for target.
        CheckVision(fieldOfView);
        if (currentTarget)
            ChangeState(EnemyState.Attacking);

        changeStateCoroutine = ChangeStateAfterTime(defaultState, searchingStateTime);
        StartCoroutine(changeStateCoroutine);

        // WIP - Trying to enemy to randomly spin around, simulating a guard checking over his shoulder.
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
            transform.rotation = Quaternion.Slerp(transform.rotation, searchingTargetRotation, Time.deltaTime * rotationDampFactor);
            searchingPointSet = true;
        }*/
        #endregion
    }


    // Patrolling State //
    /// <summary>
    /// Enemy moves sequentially to position of objects in wayPoints list.
    /// On collision with anything the direction of movement through list reverses.
    /// </summary>
    private void PatrollingState()
    {

        currentWaypoint = wayPoints[_currentWaypointIndex];

        Vector2 currentXZ = GetVector3XZ(transform.position);
        Vector2 currentTargetXZ = GetVector3XZ(currentWaypoint.position);

        if (Vector2.Distance(currentXZ, currentTargetXZ) < 0.01f)
        {

            if (isPatrollingForwards)
                _currentWaypointIndex = (_currentWaypointIndex + 1) % wayPoints.Count;
            else
            {
                if (_currentWaypointIndex == 0)
                    _currentWaypointIndex = wayPoints.Count - 1;
                else
                    _currentWaypointIndex--;
            }

        }

        // Move towards currentWaypoint
        Move(currentWaypoint.position);
        FaceTargetDirection(currentWaypoint.position);

        // Check if player has collided and needs to be knocked back
        KnockBackCollision();

        // Search for target.
        CheckVision(fieldOfView);
        if (currentTarget)
            ChangeState(EnemyState.Attacking);
    }

    private void ReversePatrolDirection()
    {

        isPatrollingForwards = !isPatrollingForwards;
        if (isPatrollingForwards)
            _currentWaypointIndex = (_currentWaypointIndex + 1) % wayPoints.Count;
        else
        {
            if (_currentWaypointIndex > 0)
                currentWaypoint = wayPoints[_currentWaypointIndex--];
            else
                currentWaypoint = wayPoints[2];
        }
    }


    // Observing State //
    /// <summary>
    /// Enemy rotates sequentially* towards locations given in wayPoints list.
    /// Upon spotting a target they move to Alerting State.
    /// </summary>
    protected void ObservingState()
    {

        Transform currentLookTowardsPoint = wayPoints[_currentWaypointIndex];

        if (wayPoints.Count >= 0)
        {
            Vector3 targetDirection = currentLookTowardsPoint.position - transform.position;

            float singleStep = observerPanningSpeed * Time.deltaTime;
            Vector3 newDirection = Vector3.RotateTowards(transform.forward, targetDirection, singleStep, 0.0f);
            transform.rotation = Quaternion.LookRotation(newDirection);

            // If facing wayPoint
            if (Vector3.Angle(transform.forward, targetDirection) < 1)
                _currentWaypointIndex = (_currentWaypointIndex + 1) % wayPoints.Count;
        }

        // Search for target.
        CheckVision(fieldOfView);
        if (currentTarget)
            ChangeState(EnemyState.Alerting);
    }

    // Alerting State //
    /// <summary>
    /// Enemy passes spottedTarget's transform to all EnemyStateMachines inside connectedEnemies List.
    /// </summary>
    protected void AlertingState()
    {

        // ToDo - Rotate towards target but clamp/lock rotation past certain point.
        if (currentTarget)
        {

            Transform sightedTarget = currentTarget.transform;
            Vector3 targetDirection = sightedTarget.position - transform.position;

            float singleStep = observerPanningSpeed * Time.deltaTime;
            Vector3 newDirection = Vector3.RotateTowards(transform.forward, targetDirection, singleStep, 0.0f);
            transform.rotation = Quaternion.LookRotation(newDirection);

            // Report last seen position to connected enemies
            foreach (EnemyStateMachine enemy in connectedEnemies)
            {
                enemy.SetCurrentTarget(sightedTarget);
            }

        }
        else
        {
            changeStateCoroutine = ChangeStateAfterTime(defaultState, spottedCooldownTime);
            StartCoroutine(changeStateCoroutine);
        }

        // Ensure currentTarget is cleared when target leaves sight.
        CheckVision(fieldOfView);
    }
    #endregion

    #region UTILITY
    /// <summary>
    /// Get the x and y value of a Vector3 as a Vector2
    /// </summary>
    /// <returns>Vector2 holding given vector3's X and Z values</returns>
    private Vector2 GetVector3XZ(Vector3 vector3)
    {
        Vector2 transformXZ = new Vector2(vector3.x, vector3.z);
        return transformXZ;
    }


    // ToDo: Check point is not inside another object & Rename
    /// <summary>
    /// Finds a point inside given boxCollider.
    /// </summary>
    /// <param name="boxCollider">Box to search in</param>
    /// <returns></returns>
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


    /// <summary>
    /// Finds random point inside circle around given transform
    /// </summary>
    /// <param name="transform">Point to search around</param>
    /// <param name="circleRadius">Radius of search area</param>
    private Vector3 FindSpotInCircle(Transform transform, float circleRadius)
    {
        Vector2 randomPointInCircle = transform.position + Random.insideUnitSphere * circleRadius * 0.5f; // 0.5 - Half the radius
        Vector3 randomCirclePoint = new Vector3();

        // Debug - Show location
        if (DEBUG_MODE)
        {
            GameObject currentMarker = Instantiate(pointMarker, randomCirclePoint, Quaternion.identity);
            Destroy(currentMarker, 1);
        }

        return randomCirclePoint;
    }

    private void OnDrawGizmos()
    {
        // Draw the sphere used in attacking SphereCast.
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(this.transform.position + (this.transform.forward * meleeReach), meleeRadius);
        Gizmos.color = Color.green;
        if (boxCollider)
        {
            Gizmos.DrawCube(this.transform.position, boxCollider.size / 2);
        }

    }
    #endregion

    #region RUNTIME
    private void KnockBackCollision()
    {
        RaycastHit collisionHit;

        if (hasCollided == false)
        {
            // p1 & p2 sphere on bottom and sphere on top
            if (Physics.BoxCast(transform.position, boxCollider.size / 2, Vector3.forward, out collisionHit, transform.rotation, 0.5f) ||
                Physics.BoxCast(transform.position, boxCollider.size / 2, Vector3.right, out collisionHit, transform.rotation, 0.5f) ||
                Physics.BoxCast(transform.position, boxCollider.size / 2, -Vector3.forward, out collisionHit, transform.rotation, 0.5f) ||
                Physics.BoxCast(transform.position, boxCollider.size / 2, -Vector3.right, out collisionHit, transform.rotation, 0.5f) ||
                Physics.BoxCast(transform.position, boxCollider.size / 2, Vector3.up, out collisionHit, transform.rotation, 0.5f))
            {

                ReversePatrolDirection();
                // Knockback ToDo: Implement better.
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

                // To prevent one object from triggering multiple times // ToDo: Save previous collision object and then reset in ResetCollisionCheck.
                Invoke("ResetCollisionCheck", timeBetweenCollisions);
            }
        }
    }

    /// <summary>
    /// Called automatically in KnockBackCollision to prevent multiple collisions.
    /// </summary>
    private void ResetCollisionCheck()
    {
        hasCollided = false;
    }


    /// <summary>
    /// Checks the given FieldOfView for visibile targets and sets currentTarget
    /// </summary>
    private void CheckVision(FieldOfView vision)
    {
        if (vision.visibleTargets.Count > 0)
            SetCurrentTarget(vision.visibleTargets[0]);
        else
            SetCurrentTarget(null);
    }


    private void Move(Vector3 movementLocation)
    {
        // Maintain altitude
        movementLocation.y = transform.position.y;
        transform.position = Vector3.MoveTowards(transform.position, movementLocation, movementSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Rotate towards current movement direction.
    /// </summary>
    protected void FaceMoveDirection()
    {
        Vector3 targetPoint = new Vector3(currentMovementTarget.x, transform.position.y, currentMovementTarget.z) - transform.position;
        Quaternion targetRotation = Quaternion.LookRotation(targetPoint, Vector3.up); // ToDo: Check thrown warning.

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationDampFactor);
    }

    // Rotate to face towards targetPosition
    protected void FaceTargetDirection(Vector3 targetPosition)
    {
        Vector3 targetPoint = new Vector3(targetPosition.x, transform.position.y, targetPosition.z) - transform.position;
        Quaternion targetRotation = Quaternion.LookRotation(targetPoint, Vector3.up); // ToDo: Check thrown warning

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationDampFactor);
    }

    public void SetCurrentTarget(Transform target)
    {
        currentTarget = target;
    }

    public bool GetCanDie()
    {
        return canDie;
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
    #endregion

}
