using UnityEngine;

public abstract class StateMachine : MonoBehaviour
{

    private State currentState;

    public void SwitchState(State state)
    {
        currentState?.Exit();
        currentState = state;
        currentState.Enter();
    }

    public void Update()
    {
        currentState?.Tick();
    }

}
