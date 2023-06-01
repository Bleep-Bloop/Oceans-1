using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerJumpState : PlayerBaseState
{

    public PlayerJumpState(PlayerStateMachine stateMachine) : base(stateMachine) { }
    public override void Enter()
    {

        stateMachine.velocity = new Vector3(stateMachine.velocity.x, stateMachine.jumpForce, stateMachine.velocity.z);

    }

    public override void Tick()
    {
        ApplyGravity();

        if (stateMachine.velocity.y <= 0.0f)
            stateMachine.SwitchState(new PlayerFallState(stateMachine));

        FaceMoveDirection();
        Move();

    }

    public override void Exit()
    {
    }

   

}
