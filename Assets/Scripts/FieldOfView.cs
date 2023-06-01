using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FieldOfView : MonoBehaviour
{

    // Distance the field of view will find targets.
    public float viewRadius;
    
    // Angle of the field of view's 'vision'.
    [Range(0, 360)]
    public float viewAngle;

    // LayerMask the Field of View is searching for.
    [SerializeField] private LayerMask targetMask;
    // LayerMask of objects blocking vision.
    [SerializeField] private LayerMask obstacleMask;

    public List<Transform> visibleTargets = new List<Transform>();

    IEnumerator FindTargetsWithDelay(float delay)
    {
        while (true)
        {
            yield return new WaitForSeconds(delay);
            FindVisibleTargets();
        }
    }

    void Start()
    {
        StartCoroutine("FindTargetsWithDelay", .2f);
    }

    void FindVisibleTargets()
    {

        visibleTargets.Clear(); 

        Collider[] targetsInViewRadius = Physics.OverlapSphere(transform.position, viewRadius, targetMask);

        for (int i = 0; i < targetsInViewRadius.Length; i++)
        {
            Transform target = targetsInViewRadius[i].transform;

            // Check if target is inside view angle
            Vector3 directionToTarget = (target.position - transform.position).normalized;
            if(Vector3.Angle(transform.forward, directionToTarget) < viewAngle/2)
            {
                float distanceToTarget = Vector3.Distance(transform.position, target.position);

                // Raycast to target to see if an obstacle is blocking view
                if(!Physics.Raycast(transform.position, directionToTarget, distanceToTarget, obstacleMask))
                    visibleTargets.Add(target);

            }
        }
    }

    // Note: Using trigonometry, with a circle 0 is on the right side moving counterclockwise.
    // In unity 0 is on the top moving clockwise. We must convert the Unity angle to use in our math.
    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        // Convert to global
        if(!angleIsGlobal)
            angleInDegrees += transform.eulerAngles.y;

        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }

}
