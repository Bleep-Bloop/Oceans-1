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
}

[RequireComponent(typeof(FieldOfView))]
public class EnemyStateMachine : MonoBehaviour
{

    // Properties //
    [SerializeField] private float movementSpeed = 1;
    [SerializeField] private float LookRotationDampFactor = 8.0f;
    [SerializeField] private float meleeReach = 0.5f;

    // Components //
    public BoxCollider wanderZone; // A BoxCollider used to mark the area an enemy will wander when in WanderingState.
    private FieldOfView fieldOfView;

    // Runtime //
    [SerializeField] private EnemyState defaultState; // The state the enemy returns too when not chasing/attacking. ToDo: Better comment
    [SerializeField] private EnemyState currentState;
    [SerializeField] private Vector3 currentMovementTarget;
    [SerializeField] private float attackCooldownTime; // Time spent in AttackCooldownState.
    private bool enemySpotted;
    bool coroutineRunning = false;

    private IEnumerator changeStateCoroutine;

    // Debug //
    [SerializeField] private bool DEBUG_MODE; // Visualize wander point, ToDo: VisionCone, and whatever else can be shown.
    [SerializeField] private GameObject pointMarker; // PointMarker prefab used to visual transforms.


    [SerializeField] private LayerMask PlayerLayerMask;

    private IEnumerator ChangeStateAfterTime(EnemyState newState, float waitTime)
    {
        coroutineRunning = true;
        yield return new WaitForSeconds(waitTime);
        ChangeState(defaultState);
        StopCoroutine(changeStateCoroutine);
    }

    private void Awake()
    {
        fieldOfView = GetComponent<FieldOfView>();
    }

    private void Start()
    {
        enemySpotted = false;
        currentState = defaultState;
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
            default:
                IdleState();
                break;
        }

        // Player is always searching for enemies so this is in Update()

        // Move to last sighted spot of enemy
       if (fieldOfView.visibleTargets.Count > 0)
        {
            ChangeState(EnemyState.Attacking);
            //currentMovementTarget = fieldOfView.visibleTargets[0].position; // moved to attacking
        }
        else
        {
            enemySpotted = false;
        }

        // Move enemy towards currentMovementTarget
        transform.position = Vector3.MoveTowards(transform.position, currentMovementTarget, movementSpeed * Time.deltaTime);
        //FaceMoveDirection(); // ToDo: A check on this to stop the warning might be ok

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
        if (Vector3.Distance(transform.position, currentMovementTarget) < 0.001 || currentMovementTarget == Vector3.zero)
        {
            currentMovementTarget = GetRandomPointInsideCollider(wanderZone);
            currentMovementTarget.y = transform.position.y; // Set Y position at enemy's height so they do not raise/lower. ToDo: Handle multiple levels/stairs   
        }

        transform.position = Vector3.MoveTowards(transform.position, currentMovementTarget, movementSpeed * Time.deltaTime);
        FaceMoveDirection();

    }

    // ToDo: Check point is not inside another object
    // Used to find spot inside boxCollider to move enemy too
    public Vector3 GetRandomPointInsideCollider(BoxCollider boxCollider)
    {
        Vector3 extents = boxCollider.size / 2f;
        Vector3 point = new Vector3(
            Random.Range(-extents.x, extents.x),
            Random.Range(-extents.y, extents.y), // we can probably set this to the height here maybe check back later ToDo:
            Random.Range(-extents.z, extents.z)
        );

        if (DEBUG_MODE)
        {
            // debugPlacementPoint will hover above the wander point 
            Vector3 debugPlacementPoint = point;
            debugPlacementPoint.y += 3;

            GameObject currentMarker = Instantiate(pointMarker, debugPlacementPoint, Quaternion.identity); // Debug - Show location
            Destroy(currentMarker, 3);

        }

        return boxCollider.transform.TransformPoint(point);
    }


    // Attacking
    private void AttackingState()
    {

        // Ensure player is not taken out of AttackingState if spotted while in SearchingState
        if(coroutineRunning)
        {
            StopAllCoroutines();
        }
        

        // Enemy has vision of player Chase target. if sight is lost move to last seen location
        if(fieldOfView.visibleTargets.Count > 0)
        {
            enemySpotted = true;
            currentMovementTarget = fieldOfView.visibleTargets[0].position;
        }
        else
        {
            enemySpotted = false;
        }
           

        if (enemySpotted && Vector2.Distance(transform.position, currentMovementTarget) < 0.01)
        {

            Debug.Log("ATTACK PLAYER");

            RaycastHit hit;

            Vector3 p1 = transform.position;
           // float distanceToObstacle = 0;

            // melee range radius
            if(Physics.SphereCast(p1, 5, transform.forward, out hit, meleeReach, PlayerLayerMask))
            {
             //   distanceToObstacle = hit.distance;
                Debug.Log("Player Hit");
            }


          
            
            ChangeState(EnemyState.AttackingCooldown);
        }
        else if (!enemySpotted && Vector2.Distance(transform.position, currentMovementTarget) < 0.01)
        {
            ChangeState(EnemyState.Searching);
        }

        FaceMoveDirection();
    
    }

    private void AttackingCooldownState()
    {

    }

    private void SearchingState()
    {

        changeStateCoroutine = ChangeStateAfterTime(EnemyState.Idle, 5.0f);
        StartCoroutine(changeStateCoroutine);


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

    // Used in attempt to have enemy rotate and find player while searching
    /*
    // Find a random spot in the circle around a locatoin
    private Vector3 FindSpotInCircleAroundLocation(Transform transform, float circleRadius)
    {
        Vector2 randomPointInCircle = transform.position + Random.insideUnitSphere * circleRadius * 0.5f; // 0.5 half the radius
        Vector3 newLookAtPoint = new Vector3();

        newLookAtPoint.x = randomPointInCircle.x;
        newLookAtPoint.y = base.transform.position.y;
        newLookAtPoint.z = randomPointInCircle.y;


        if (DEBUG_MODE)
        {
            // Debug - Show location
            GameObject currentMarker = Instantiate(pointMarker, newLookAtPoint, Quaternion.identity);
            Destroy(currentMarker, 1);
        }

        return newLookAtPoint;
    }
    */

    // Rotate the enemy to face towards their currentMovementTarget
    protected void FaceMoveDirection()
    {
        Vector3 targetPoint = new Vector3(currentMovementTarget.x, transform.position.y, currentMovementTarget.z) - transform.position;
        Quaternion targetRotation = Quaternion.LookRotation(targetPoint, Vector3.up); // ToDo: This is throwing a warning because it equals zero sometimes

        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * LookRotationDampFactor);
    }

    private void ChangeState(EnemyState newState)
    {
        currentState = newState;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.tag == "Player")
        {
            Debug.Log("Player Hit - Move to Attack Cooldown State");
        }
    }



}
