using System.Collections;
using UnityEngine;
using Fusion;
using UnityEngine.Windows;

[OrderAfter(typeof(NetworkPhysicsSimulation2D))]
public class PlayerMovement : NetworkBehaviour
{
    [Header("Movement")]
    private PlayerBehaviour _behaviour;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private LayerMask _ladderLayer;
    private NetworkTransform _nt;
    private InputHandler _inputController;
    private PlayerLadderController _ladderController;

    [SerializeField] float _speed = 5f;
    [SerializeField] float _ladderSpeedMultiplier = 0.5f;
    [SerializeField] float _jumpForce = 10f;
    [SerializeField] float _gravity = -9.81f;
    [SerializeField] float _gravityMultiplier = 3f;
    private float _velocity;

    private Collider2D _collider;
    [Networked]
    private NetworkBool IsGrounded { get; set; }
    [Networked]
    private NetworkBool IsLadder { get; set; }

    [Space()]
    [Header("Particle")]
    [SerializeField] private ParticleManager _particleManager;

    [Space()]
    [Header("Sound")]
    [SerializeField] private SoundChannelSO _sfxChannel;
    [SerializeField] private SoundSO _jumpSound;
    [SerializeField] private AudioSource _playerSource;

    private void Awake()
    {
        _nt = GetComponent<NetworkTransform>();
        _collider = GetComponentInChildren<Collider2D>();
        _behaviour = GetBehaviour<PlayerBehaviour>();
        _inputController = GetBehaviour<InputHandler>();
        _ladderController = GetBehaviour<PlayerLadderController>();
    }

    public override void Spawned()
    {
        Runner.SetPlayerAlwaysInterested(Object.InputAuthority, Object, true);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube((Vector2)transform.position + Vector2.down * (_collider.bounds.extents.y - .4f), Vector2.one * .85f);
    }

    /// <summary>
    /// Detects grounded and wall sliding state
    /// </summary>
    private void DetectGround()
    {
        IsGrounded = (bool)Runner.GetPhysicsScene2D().OverlapBox((Vector2)transform.position + Vector2.down * (_collider.bounds.extents.y - .4f), Vector2.one * .85f, 0, _groundLayer);
    }

    private void DetectLadder()
    {
        IsLadder = (bool)Runner.GetPhysicsScene2D().OverlapBox((Vector2)transform.position + Vector2.down * (_collider.bounds.extents.y - .4f), Vector2.one * .85f, 0, _ladderLayer);
    }

    public bool GetGrounded() => IsGrounded;

    public override void FixedUpdateNetwork()
    {
        if (GetInput<InputData>(out var input))
        {
            var pressed = input.GetButtonPressed(_inputController.PrevButtons);
            _inputController.PrevButtons = input.Buttons;

            DetectGround();
            DetectLadder();

            Jump(pressed);
            UpdateMovement(input);
        }
    }

    private void UpdateMovement(InputData input)
    {
        Vector3 moveVector = Vector3.zero;
        moveVector.y = _velocity;

        if (input.GetButton(InputButton.LEFT) && _behaviour.InputsAllowed)
        {
            moveVector += _speed * Vector3.left;
        }
        else if (input.GetButton(InputButton.RIGHT) && _behaviour.InputsAllowed)
        {
            moveVector += _speed * Vector3.right;
        }
        else if (IsLadder && input.GetButton(InputButton.UP) && _behaviour.InputsAllowed)
        {
            moveVector += _speed * _ladderSpeedMultiplier * Vector3.up;
        }
        else if (IsLadder && input.GetButton(InputButton.DOWN) && _behaviour.InputsAllowed)
        {
            moveVector += _speed * _ladderSpeedMultiplier * Vector3.down;
        }

        _nt.transform.Translate(moveVector * Runner.DeltaTime);
    }

    private void Jump(NetworkButtons pressedButtons)
    {
        if (!IsGrounded && !IsLadder)
            _velocity += _gravity * _gravityMultiplier * Runner.DeltaTime;
        else
            _velocity = 0f;

        if (pressedButtons.IsSet(InputButton.JUMP))
        {
            if (_behaviour.InputsAllowed)
            {
                if (IsGrounded)
                {
                    _velocity = _jumpForce;
                    //RPC_PlayJumpEffects((Vector2)transform.position - Vector2.up * .5f);
                }
            }
        }
    }

    [Rpc(sources: RpcSources.InputAuthority, RpcTargets.All)]
    private void RPC_PlayJumpEffects(Vector2 particlePos)
    {
        PlayJumpSound();
        PlayJumpParticle(particlePos);
    }

    private void PlayJumpSound()
    {
        _sfxChannel.CallSoundEvent(_jumpSound, Object.HasInputAuthority ? null : _playerSource);
    }

    private void PlayJumpParticle(Vector2 pos)
    {
        _particleManager.Get(ParticleManager.ParticleID.Jump).transform.position = pos;
    }
}