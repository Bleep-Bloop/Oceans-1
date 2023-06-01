using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(InputReader))]
[RequireComponent(typeof(CharacterController))]
public class PlayerStateMachine : StateMachine
{

    public Vector3 velocity;
    public float movementSpeed { get; private set; } = 5.0f;
    public float jumpForce { get; private set; } = 5.0f;
    public float LookRotationDampFactor { get; private set; } = 10.0f;
    public Transform mainCamera { get; private set; }
    public InputReader inputReader { get; private set; }
    public CharacterController characterController { get; private set; }


    private void Awake()
    {
        inputReader = GetComponent<InputReader>();
        characterController = GetComponent<CharacterController>();
    }

    // Start is called before the first frame update
    void Start()
    {
        mainCamera = Camera.main.transform;

        //SwitchState(new PlayerMoveState(this));
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
