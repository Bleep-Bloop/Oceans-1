using UnityEngine;

public class PlayerMoveState : PlayerBaseState
{

    public PlayerMoveState(PlayerStateMachine stateMachine) : base(stateMachine) { }

    public override void Enter()
    {
        stateMachine.velocity.y = Physics.gravity.y;

        stateMachine.inputReader.OnJumpPerformed += SwitchToJumpState;
    }

    public override void Tick()
    {
        if (!stateMachine.characterController.isGrounded)
        {
            stateMachine.SwitchState(new PlayerFallState(stateMachine));
        }

        CalculateMoveDirection();
        FaceMoveDirection();
        Move();
    }

    public override void Exit()
    {
        stateMachine.inputReader.OnJumpPerformed -= SwitchToJumpState;
    }

    private void SwitchToJumpState()
    {
        stateMachine.SwitchState(new PlayerJumpState(stateMachine));
    }



}
