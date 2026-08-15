using UnityEngine;

namespace NinetyNine
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        private const float StandingHeight = 1.72f;
        private const float CrouchingHeight = 1.08f;
        private readonly Collider[] _headroomHits = new Collider[12];

        private CharacterController _controller;
        private Camera _camera;
        private float _pitch;
        private float _yaw;
        private float _verticalVelocity;
        private float _stepCycle;
        private float _stamina = 100f;
        private float _sprintRecoveryDelay;
        private float _elevatorMotion;
        private float _elevatorImpulse;
        private float _nextFootstepNoise;
        private float _nextBreathingNoise;
        private bool _crouchToggled;
        private bool _cursorReleased;
        private bool _applicationFocused = true;
        private bool _hidden;
        private Vector3 _hideExitPosition;
        private Vector3 _cameraBasePosition;

        public bool CanMove { get; set; }
        public bool UseStamina { get; set; }
        public float SpeedMultiplier { get; set; } = 1f;
        public float LookSensitivity { get; set; } = 2.6f;
        public Camera ViewCamera => _camera;
        public float Stamina01 => _stamina / 100f;
        public float MovementAmount { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsHidden => _hidden;
        public float HiddenSince { get; private set; }
        public bool IsInsideElevator => transform.position.z < 1.55f && Mathf.Abs(transform.position.x) < 2.05f;

        public void Initialize(Camera playerCamera)
        {
            _controller = GetComponent<CharacterController>();
            _camera = playerCamera;
            _cameraBasePosition = _camera.transform.localPosition;
            _yaw = transform.eulerAngles.y;
        }

        public void ResetInsideCabin()
        {
            if (_controller == null)
            {
                _controller = GetComponent<CharacterController>();
            }

            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.position = new Vector3(0f, 0.08f, -0.72f);
            transform.rotation = Quaternion.identity;
            _pitch = 0f;
            _yaw = 0f;
            _verticalVelocity = 0f;
            _stamina = 100f;
            _sprintRecoveryDelay = 0f;
            MovementAmount = 0f;
            IsSprinting = false;
            _elevatorMotion = 0f;
            _elevatorImpulse = 0f;
            _cursorReleased = false;
            _hidden = false;
            HiddenSince = 0f;
            _crouchToggled = false;
            IsCrouching = false;
            _controller.height = StandingHeight;
            _controller.center = new Vector3(0f, 0.88f, 0f);
            _controller.detectCollisions = true;
            _controller.enabled = wasEnabled;
            if (_camera != null)
            {
                _camera.transform.localRotation = Quaternion.identity;
                _camera.transform.localPosition = _cameraBasePosition;
            }
        }

        private void Update()
        {
            if (!CanMove || _controller == null || _camera == null)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0))
            {
                _cursorReleased = false;
            }
            if (_cursorReleased)
            {
                MovementAmount = 0f;
                return;
            }
            ForceCursorLock();

            float mouseX = Input.GetAxisRaw("Mouse X") * LookSensitivity;
            float mouseY = Input.GetAxisRaw("Mouse Y") * LookSensitivity;
            ApplyLook(mouseX, mouseY);

            if (_hidden)
            {
                MovementAmount = 0f;
                UpdateCameraMotion(0f);
                return;
            }

            Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            input = Vector2.ClampMagnitude(input, 1f);
            if (Input.GetKeyDown(KeyCode.C) || Input.GetKeyDown(KeyCode.LeftControl) ||
                Input.GetKeyDown(KeyCode.RightControl))
            {
                if (!_crouchToggled || CanStandUp())
                {
                    _crouchToggled = !_crouchToggled;
                }
            }
            IsCrouching = _crouchToggled;
            UpdateCrouchShape();
            bool wantsSprint = !IsCrouching && Input.GetKey(KeyCode.LeftShift) && input.sqrMagnitude > 0.05f;
            IsSprinting = wantsSprint && (!UseStamina || _stamina > 0.5f);
            if (UseStamina)
            {
                if (IsSprinting)
                {
                    _stamina = Mathf.Max(0f, _stamina - 22f * Time.deltaTime);
                    _sprintRecoveryDelay = 0.9f;
                }
                else
                {
                    _sprintRecoveryDelay = Mathf.Max(0f, _sprintRecoveryDelay - Time.deltaTime);
                    if (_sprintRecoveryDelay <= 0f)
                    {
                        _stamina = Mathf.Min(100f, _stamina + 14f * Time.deltaTime);
                    }
                }
            }
            float speed = IsSprinting ? 4.5f : 2.15f;
            if (IsCrouching) speed = 1.18f;
            speed *= Mathf.Max(0.25f, SpeedMultiplier);
            Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;

            if (_controller.isGrounded)
            {
                _verticalVelocity = -1.5f;
            }
            else
            {
                _verticalVelocity -= 16f * Time.deltaTime;
            }

            move.y = _verticalVelocity;
            _controller.Move(move * Time.deltaTime);

            MovementAmount = new Vector2(_controller.velocity.x, _controller.velocity.z).magnitude;
            EmitMovementNoise();
            UpdateCameraMotion(MovementAmount);
        }

        private void UpdateCameraMotion(float movementAmount)
        {
            _stepCycle += movementAmount * Time.deltaTime * 2.1f;
            float bob = movementAmount > 0.1f ? Mathf.Sin(_stepCycle * Mathf.PI) *
                (IsCrouching ? 0.012f : 0.026f) : 0f;
            float breathe = Mathf.Sin(Time.time * 1.15f) * 0.006f;
            _elevatorImpulse = Mathf.MoveTowards(_elevatorImpulse, 0f, Time.deltaTime * 1.35f);
            float rideSway = Mathf.Sin(Time.time * 2.7f) * 0.009f * _elevatorMotion;
            float rideVibration = (Mathf.Sin(Time.time * 17f) * 0.004f +
                Mathf.Sin(Time.time * 6.2f) * 0.007f) * _elevatorMotion;
            Vector3 crouchOffset = IsCrouching ? Vector3.down * 0.48f : Vector3.zero;
            Vector3 target = _cameraBasePosition + crouchOffset + new Vector3(rideSway,
                bob + breathe + rideVibration + _elevatorImpulse * 0.045f, 0f);
            _camera.transform.localPosition = Vector3.Lerp(_camera.transform.localPosition, target,
                9f * Time.deltaTime);
            float rideRoll = Mathf.Sin(Time.time * 2.1f) * 0.28f * _elevatorMotion;
            _camera.transform.localRotation = Quaternion.Euler(_pitch, 0f, rideRoll);
        }

        private void UpdateCrouchShape()
        {
            float targetHeight = IsCrouching ? CrouchingHeight : StandingHeight;
            _controller.height = Mathf.MoveTowards(_controller.height, targetHeight, Time.deltaTime * 3.4f);
            _controller.center = new Vector3(0f, _controller.height * 0.5f + 0.02f, 0f);
        }

        private bool CanStandUp()
        {
            float radius = Mathf.Max(0.05f, _controller.radius * 0.92f);
            Vector3 bottom = transform.position + Vector3.up * radius;
            Vector3 top = transform.position + Vector3.up * (StandingHeight - radius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, _headroomHits,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = _headroomHits[i];
                if (hit != null && hit != _controller && !hit.transform.IsChildOf(transform))
                {
                    return false;
                }
            }
            return true;
        }

        private void EmitMovementNoise()
        {
            if (MovementAmount > 0.15f && Time.time >= _nextFootstepNoise)
            {
                float loudness = IsSprinting ? 12f : IsCrouching ? 2.2f : 5.2f;
                EvacuationSignals.Emit(transform.position, loudness, IsSprinting ? NoiseKind.Sprint : NoiseKind.Footstep);
                _nextFootstepNoise = Time.time + (IsSprinting ? 0.3f : IsCrouching ? 0.72f : 0.48f);
            }
            if (UseStamina && _stamina < 24f && Time.time >= _nextBreathingNoise)
            {
                EvacuationSignals.Emit(transform.position, IsCrouching ? 2.4f : 4.5f, NoiseKind.Breathing);
                _nextBreathingNoise = Time.time + 1.25f;
            }
        }

        public void SetElevatorMotion(float amount, bool braking)
        {
            _elevatorMotion = Mathf.Clamp01(amount) * (braking ? 0.65f : 1f);
        }

        public void AddElevatorImpulse(float direction)
        {
            _elevatorImpulse = Mathf.Clamp(direction, -1f, 1f);
        }

        private void ApplyLook(float yawDelta, float pitchDelta)
        {
            _yaw = Mathf.Repeat(_yaw + yawDelta + 180f, 360f) - 180f;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            _pitch = Mathf.Clamp(_pitch - pitchDelta, -85f, 85f);
        }

        public void EnterHidingSpot(Transform hidingPoint, Vector3 exitPosition)
        {
            if (_hidden || hidingPoint == null)
            {
                return;
            }
            _hideExitPosition = exitPosition;
            _controller.enabled = false;
            transform.position = hidingPoint.position;
            transform.rotation = hidingPoint.rotation;
            _yaw = transform.eulerAngles.y;
            _controller.enabled = true;
            _controller.detectCollisions = false;
            _hidden = true;
            HiddenSince = Time.time;
            IsSprinting = false;
            MovementAmount = 0f;
        }

        public void ExitHidingSpot()
        {
            if (!_hidden)
            {
                return;
            }
            _controller.enabled = false;
            transform.position = _hideExitPosition;
            _controller.detectCollisions = true;
            _controller.enabled = true;
            _hidden = false;
        }

        private void ForceCursorLock()
        {
            if (!_applicationFocused || _cursorReleased)
            {
                return;
            }
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

#if UNITY_EDITOR
        public bool VerifyCrouchToggle()
        {
            bool original = _crouchToggled;
            _crouchToggled = !original;
            bool toggled = _crouchToggled != original;
            _crouchToggled = original;
            return toggled && _crouchToggled == original;
        }

        public bool VerifyUnclampedYaw()
        {
            Quaternion before = transform.rotation;
            for (int i = 0; i < 16; i++) ApplyLook(90f, 0f);
            return Quaternion.Angle(before, transform.rotation) < 0.01f;
        }

        public bool VerifyBlockedStandUp()
        {
            GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "CrouchHeadroomTest";
            blocker.transform.position = transform.position + Vector3.up * 1.48f;
            blocker.transform.localScale = new Vector3(0.9f, 0.16f, 0.9f);
            Physics.SyncTransforms();
            bool blocked = !CanStandUp();
            DestroyImmediate(blocker);
            return blocked;
        }
#endif

        private void OnApplicationFocus(bool hasFocus)
        {
            _applicationFocused = hasFocus;
            if (hasFocus && CanMove)
            {
                _cursorReleased = false;
                ForceCursorLock();
            }
        }
    }
}
