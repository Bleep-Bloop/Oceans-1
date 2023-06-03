using UnityEngine;

public class PlayerFallState : PlayerBaseState
{

    public PlayerFallState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        stateMachine.velocity.y = 0.0f;
    }

    public override void Tick()
    {

        ApplyGravity();
        Move();
        FaceMoveDirection();

        // Check if landing on enemy
        RaycastHit hit;

        if (Physics.SphereCast(stateMachine.transform.position, 0.5f, -stateMachine.transform.up, out hit, 0.5f, stateMachine.enemyLayerMask))
        {
            EnemyStateMachine hitEnemy = hit.collider.gameObject.GetComponent<EnemyStateMachine>();
            
            if (hitEnemy && hitEnemy.GetCanDie())
                hitEnemy.Death();

        }


        if (stateMachine.characterController.isGrounded)
        {
            stateMachine.SwitchState(new PlayerMoveState(stateMachine));
        }
    }

    public override void Exit()
    {
    }

}
