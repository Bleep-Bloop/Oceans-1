using System.Collections;
using System.Collections.Generic;
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

        if (stateMachine.characterController.isGrounded)
        {
            stateMachine.SwitchState(new PlayerMoveState(stateMachine));
        }
    }

    public override void Exit()
    {
    }

}
