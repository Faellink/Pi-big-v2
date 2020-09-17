using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCharacterController : MonoBehaviour {

    private const float NORMAL_FOV = 60f;
    private const float HOOKSHOT_FOV = 100f;

    [Tooltip("Sensibilidade do mouse")]
    [SerializeField] private float _mouseSensitivity = 1f;
    [Tooltip("Ponta/Gancho do hookshot")]
    [SerializeField] private Transform _debugHitPointTransform;
    [Tooltip("Corda do hookshot")]
    [SerializeField] private Transform _hookshotTransform;

    private CharacterController _characterController;
    private float _cameraVerticalAngle;
    private float _characterVelocityY;
    private Vector3 _characterVelocityMomentum;
    private Camera _playerCamera;
    private CameraFov _cameraFov;
    private ParticleSystem _speedLinesParticleSystem;
    private State _state;
    private Vector3 _hookshotPosition;
    private float _hookshotSize;
    [Tooltip("Velocidade de arremesso do hookshot")]
    [SerializeField] private float _hookshotThrowSpeed = 150f;
    [Tooltip("Força do pulo enquanto utiliza o hookshot")]
    [SerializeField] private float _jumpSpeed = 30f;
    [Tooltip("Força do pulo no chao")]
    [SerializeField] private float _jumpForce = 3f;
    [Tooltip("Força da gravidade do Player")]
    [SerializeField] private float _gravityDownForce = -60f;
    [Tooltip("Velocidade de movimentação do Player")]
    [SerializeField] private float _moveSpeed = 20f;

    private enum State {

        Normal,
        HookshotThrown,
        HookshotFlyingPlayer,

    }

    private void Awake() {

        _characterController = GetComponent<CharacterController>();
        _playerCamera = transform.Find("Main Camera").GetComponent<Camera>();
        _cameraFov = _playerCamera.GetComponent<CameraFov>();
        _speedLinesParticleSystem = GetComponentInChildren<ParticleSystem>();
        Cursor.lockState = CursorLockMode.Locked;
        _state = State.Normal;
        _hookshotTransform = _characterController.transform.Find("Hookshot").transform;
        _hookshotTransform.gameObject.SetActive(false);

    }

    private void Update() {

        switch (_state) {
        default:
        case State.Normal:
            MovePlayerCamera();
            CharacterMovement();
            HookshotStart();
            break;
        case State.HookshotThrown:
            HookshotThrow();
            MovePlayerCamera();
            CharacterMovement();
            break;
        case State.HookshotFlyingPlayer:
            MovePlayerCamera();
            HookshotMovement();
            break;
        }

    }

    private void MovePlayerCamera() {

        float lookX = Input.GetAxisRaw("Mouse X");
        float lookY = Input.GetAxisRaw("Mouse Y");

        // Rotate the transform with the input speed around its local Y axis
        transform.Rotate(new Vector3(0f, lookX * _mouseSensitivity, 0f), Space.Self);

        // Add vertical inputs to the camera's vertical angle
        _cameraVerticalAngle -= lookY * _mouseSensitivity;

        // Limit the camera's vertical angle to min/max
        _cameraVerticalAngle = Mathf.Clamp(_cameraVerticalAngle, -89f, 89f);

        // Apply the vertical angle as a local rotation to the camera transform along its right axis (makes it pivot up and down)
        _playerCamera.transform.localEulerAngles = new Vector3(_cameraVerticalAngle, 0, 0);

    }

    private void CharacterMovement() {

        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");

        Vector3 characterVelocity = transform.right * moveX * _moveSpeed + transform.forward * moveZ * _moveSpeed;

        if (_characterController.isGrounded) {

            _characterVelocityY = 0f;
            // Jump
            if (TestInputJump()) {

                _characterVelocityY = _jumpForce;

            }

        }

        // Apply gravity to the velocity
        _characterVelocityY += _gravityDownForce * Time.deltaTime;


        // Apply Y velocity to move vector
        characterVelocity.y = _characterVelocityY;

        // Apply momentum
        characterVelocity += _characterVelocityMomentum;

        // Move Character Controller
        _characterController.Move(characterVelocity * Time.deltaTime);

        // Dampen momentum
        if (_characterVelocityMomentum.magnitude > 0f) {

            float momentumDrag = 3f;
            _characterVelocityMomentum -= _characterVelocityMomentum * momentumDrag * Time.deltaTime;
            if (_characterVelocityMomentum.magnitude < .0f) {

                _characterVelocityMomentum = Vector3.zero;

            }

        }
    }

    private void ResetGravityEffect() {

        _characterVelocityY = 0f;

    }

    private void HookshotStart() {

        if (TestInputDownHookshot()) {

            if (Physics.Raycast(_playerCamera.transform.position, _playerCamera.transform.forward, out RaycastHit raycastHit)) {
                // Hit something
                _debugHitPointTransform.position = raycastHit.point;
                _hookshotPosition = raycastHit.point;
                _hookshotSize = 0f;
                _hookshotTransform.gameObject.SetActive(true);
                _hookshotTransform.localScale = Vector3.zero;
                _state = State.HookshotThrown;
            }

        }

    }

    private void HookshotThrow() {

        _hookshotTransform.LookAt(_hookshotPosition);

        _hookshotSize += _hookshotThrowSpeed * Time.deltaTime;
        _hookshotTransform.localScale = new Vector3(1, 1, _hookshotSize);
        if (_hookshotSize >= Vector3.Distance(transform.position, _hookshotPosition)) {

            _state = State.HookshotFlyingPlayer;
            _cameraFov.SetCameraFov(HOOKSHOT_FOV);
            _speedLinesParticleSystem.Play();

        }

    }

    private void HookshotMovement() {

        _hookshotTransform.LookAt(_hookshotPosition);

        Vector3 hookshotDir = (_hookshotPosition - transform.position).normalized;

        float hookshotSpeedMin = 10f;
        float hookshotSpeedMax = 40f;
        float hookshotSpeed = Mathf.Clamp(Vector3.Distance(transform.position, _hookshotPosition), hookshotSpeedMin, hookshotSpeedMax);
        float hookshotSpeedMultiplier = 5f;

        // Move Character Controller
        _characterController.Move(hookshotDir * hookshotSpeed * hookshotSpeedMultiplier * Time.deltaTime);

        float reachedHookshotPositionDistance = 1f;
        if (Vector3.Distance(transform.position, _hookshotPosition) < reachedHookshotPositionDistance) {

            // Reached Hookshot Position
            StopHookshot();

        }

        if (TestInputDownHookshot()) {

            // Cancel Hookshot
            StopHookshot();

        }

        if (TestInputJump()) {

            // Cancelled with Jump
            float momentumExtraSpeed = 7f;
            _characterVelocityMomentum = hookshotDir * hookshotSpeed * momentumExtraSpeed;
            _characterVelocityMomentum += Vector3.up * _jumpSpeed;
            StopHookshot();

        }

    }

    private void StopHookshot() {

        _state = State.Normal;
        ResetGravityEffect();
        _hookshotTransform.gameObject.SetActive(false);
        _cameraFov.SetCameraFov(NORMAL_FOV);
        _speedLinesParticleSystem.Stop();
    }


    private bool TestInputDownHookshot() {

        return Input.GetKeyDown(KeyCode.E);
    }


    private bool TestInputJump() {

        return Input.GetKeyDown(KeyCode.Space);

    }

}
