using AimTrainer.Movement;
using UnityEngine;

namespace AimTrainer.Player
{
    public enum MoveState
    {
        Idle,
        Running,
        Walking,
        Crouching,
        Airborne,
    }

    /// <summary>
    /// 第一人称控制器：鼠标视角 + 移动（跑步、静步、下蹲、跳跃、急停）。
    ///
    /// 物体结构：本物体的原点在脚底，挂 CharacterController；摄像机是子物体，放在眼睛高度。
    /// 每帧顺序固定为：读输入 → 转视角 → 下蹲 → 算水平速度 → 跳跃与重力 → 移动 → 摄像机高度。
    /// 视角放在最前面，移动方向用的就是本帧最新的朝向，摄像机也在同一帧里转好再渲染。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("引用")]
        [Tooltip("移动参数资源。留空时使用代码里的默认值。")]
        [SerializeField] MovementSettings settings;

        [Tooltip("第一人称摄像机，应为本物体的子物体。留空时自动查找。")]
        [SerializeField] Camera playerCamera;

        [Header("视角")]
        [Tooltip("填游戏内的灵敏度数值：每个鼠标计数转 0.07° × 灵敏度。")]
        [SerializeField, Min(0.001f)] float sensitivity = 0.4f;

        [Tooltip("水平视野（度），端游固定为 103。")]
        [SerializeField, Range(60f, 130f)] float horizontalFov = ViewMath.DefaultHorizontalFov;

        [Tooltip("抬头和低头的最大角度。")]
        [SerializeField, Range(60f, 90f)] float maxPitch = 89f;

        [SerializeField] bool invertY;

        [Header("按键")]
        [Tooltip("滚轮也能跳跃（很多玩家把跳跃绑在滚轮上）。")]
        [SerializeField] bool scrollWheelJump = true;

        [Tooltip("由本组件管理鼠标锁定：开始时锁定，Esc 释放，点击画面重新锁定。有训练菜单的场景里由菜单管理，这里关闭。")]
        [SerializeField] bool manageCursor = true;

        readonly Collider[] overlapBuffer = new Collider[16];
        readonly StopTimer stopTimer = new StopTimer();

        CharacterController body;
        MovementTuning defaultTuning;

        PlanarVector horizontalVelocity;
        float verticalVelocity;
        float yaw;
        float pitch;
        float crouchAmount;
        bool grounded;
        bool jumpRequested;
        float jumpRequestAge;
        float appliedHeight = -1f;
        float appliedAspect = -1f;
        float appliedHorizontalFov = -1f;
        double accumulatedYaw;

        public MovementTuning Tuning => settings != null ? settings.tuning : defaultTuning ??= new MovementTuning();
        public Camera PlayerCamera => playerCamera;

        public PlanarVector HorizontalVelocity => horizontalVelocity;
        public float HorizontalSpeed => horizontalVelocity.Magnitude;
        public float VerticalVelocity => verticalVelocity;
        public bool IsGrounded => grounded;
        public float CrouchAmount => crouchAmount;
        public MoveState State { get; private set; }

        /// <summary>站在地面上且水平速度不超过精准门槛。</summary>
        public bool IsAccurate => grounded && HorizontalSpeed <= Tuning.accurateSpeedThreshold;

        public StopTimer StopTimer => stopTimer;
        public float Yaw => yaw;
        public float Pitch => pitch;
        public bool IsCursorLocked => Cursor.lockState == CursorLockMode.Locked;

        /// <summary>为 false 时忽略所有键盘鼠标输入（打开菜单、死亡等待复活时）。</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>是否由本组件管理鼠标锁定。</summary>
        public bool ManageCursor
        {
            get => manageCursor;
            set => manageCursor = value;
        }

        /// <summary>当前瞄准方向（不含任何画面抖动），射线从 EyePosition 沿这个方向发出。</summary>
        public Quaternion AimRotation => Quaternion.Euler(-pitch, yaw, 0f);

        /// <summary>眼睛（摄像机）的世界坐标。</summary>
        public Vector3 EyePosition => playerCamera != null
            ? playerCamera.transform.position
            : transform.position + Vector3.up * Tuning.standingEyeHeight;

        public float HorizontalFov
        {
            get => horizontalFov;
            set => horizontalFov = Mathf.Clamp(value, 60f, 130f);
        }

        public bool InvertY
        {
            get => invertY;
            set => invertY = value;
        }

        /// <summary>上次归零以来水平转过的总角度（度），用来核对 360° 距离。</summary>
        public double AccumulatedYaw => accumulatedYaw;

        public float Sensitivity
        {
            get => sensitivity;
            set => sensitivity = Mathf.Max(0.001f, value);
        }

        /// <summary>给代码或编辑器工具创建的玩家设置引用。</summary>
        public void Configure(MovementSettings movementSettings, Camera firstPersonCamera)
        {
            settings = movementSettings;
            playerCamera = firstPersonCamera;
        }

        public void ResetAccumulatedYaw() => accumulatedYaw = 0d;

        /// <summary>
        /// 把玩家放到指定位置和朝向，并重置移动状态（速度、下蹲、跳跃缓冲、急停测量）。用于出生和复活。
        /// </summary>
        public void Teleport(Vector3 feetPosition, float yawDegrees)
        {
            if (body == null) body = GetComponent<CharacterController>();
            bool wasEnabled = body.enabled;
            body.enabled = false;
            transform.position = feetPosition;
            body.enabled = wasEnabled;
            yaw = Mathf.Repeat(yawDegrees, 360f);
            pitch = 0f;
            horizontalVelocity = PlanarVector.Zero;
            verticalVelocity = 0f;
            grounded = false;
            jumpRequested = false;
            crouchAmount = 0f;
            stopTimer.Cancel();
            ApplyBodyDimensions(Tuning);
            ApplyLookRotation();
        }

        public void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Awake()
        {
            body = GetComponent<CharacterController>();
            if (playerCamera == null) playerCamera = GetComponentInChildren<Camera>();

            // 默认的 0.001 会忽略极小的位移，低速时会吞掉移动。
            body.minMoveDistance = 0f;

            yaw = transform.eulerAngles.y;
            pitch = 0f;
            ApplyBodyDimensions(Tuning);
            ApplyLookRotation();
        }

        void Start()
        {
            if (manageCursor) LockCursor();
        }

        void OnDisable()
        {
            if (manageCursor) UnlockCursor();
        }

        void Update()
        {
            InputFrame input = PlayerInputReader.Read(scrollWheelJump);
            if (manageCursor) HandleCursor(input);

            // 鼠标没锁定（点了别的窗口、按了 Esc、打开了菜单）或输入被禁用时忽略所有输入。
            if (!IsCursorLocked || !InputEnabled) input = default;

            ApplyLook(input.LookDelta);

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            MovementTuning t = Tuning;
            UpdateCrouch(input.Crouch, t, dt);
            UpdateJumpRequest(input.JumpPressed, t, dt);

            PlanarVector wish = MovementModel.WishDirection(input.Forward, input.Right, yaw);
            float targetSpeed = MovementModel.TargetSpeed(t, input.Walk, crouchAmount);
            PlanarVector before = horizontalVelocity;
            bool wasGrounded = grounded;

            if (wasGrounded)
            {
                horizontalVelocity = MovementModel.StepGround(horizontalVelocity, wish, targetSpeed, t, dt);
                if (jumpRequested)
                {
                    verticalVelocity = t.JumpVelocity;
                    jumpRequested = false;
                }
                else if (verticalVelocity < 0f)
                {
                    verticalVelocity = -t.groundStickSpeed;
                }
            }
            else
            {
                horizontalVelocity = MovementModel.StepAir(horizontalVelocity, wish, targetSpeed, t, dt);
            }

            float dy = MovementModel.IntegrateVertical(ref verticalVelocity, t.gravity, dt);
            Move(new Vector3(horizontalVelocity.X * dt, dy, horizontalVelocity.Z * dt), t, wasGrounded && verticalVelocity <= 0f);

            stopTimer.Tick(before, horizontalVelocity, wish, wasGrounded, dt, t, targetSpeed);
            UpdateState(input.Walk);
            UpdateEyeHeight(t);
        }

        void LateUpdate() => UpdateFieldOfView();

        void Move(Vector3 motion, MovementTuning t, bool onGround)
        {
            // 空中关闭自动上台阶，否则跳到一半会被“吸”上本来跳不上的台子。
            // 下蹲时用更小的台阶高度，否则 PhysX 上台阶时会把胶囊抬高，蹲着也钻不进矮通道。
            float step = Mathf.Lerp(t.stepOffset, Mathf.Min(t.stepOffset, t.crouchStepOffset), crouchAmount);
            body.stepOffset = onGround ? Mathf.Min(step, body.height) : 0f;

            CollisionFlags flags = body.Move(motion);
            grounded = (flags & CollisionFlags.Below) != 0;
            if (onGround && !grounded) SnapToGround(t, step);

            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;

            if ((flags & CollisionFlags.Sides) != 0)
            {
                // 撞墙后只保留实际走出去的速度，贴墙滑动时速度会沿墙面方向，正面撞墙会停下。
                Vector3 actual = body.velocity;
                var actualPlanar = new PlanarVector(actual.x, actual.z);
                if (actualPlanar.SqrMagnitude < horizontalVelocity.SqrMagnitude) horizontalVelocity = actualPlanar;
            }
        }

        /// <summary>
        /// 上一帧还在地面、这一帧却离地了：多半是走下台阶或跑下陡坡，而不是真的腾空。
        /// 向下探测不超过一个台阶高度，找到能站的地面就直接贴上去，避免状态在“空中”和地面之间闪烁。
        /// </summary>
        void SnapToGround(MovementTuning t, float maxDrop)
        {
            float castRadius = body.radius * 0.95f;
            Vector3 bottomSphere = transform.position + body.center + Vector3.down * (body.height * 0.5f - body.radius);
            float distance = maxDrop + body.skinWidth + (body.radius - castRadius);
            if (!Physics.SphereCast(bottomSphere, castRadius, Vector3.down, out RaycastHit hit, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return;
            }
            if (hit.transform.IsChildOf(transform)) return;
            if (hit.normal.y < Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad)) return;

            CollisionFlags flags = body.Move(Vector3.down * hit.distance);
            grounded = (flags & CollisionFlags.Below) != 0;
            if (grounded) verticalVelocity = -t.groundStickSpeed;
        }

        void ApplyLook(Vector2 delta)
        {
            float degreesPerCount = ViewMath.DegreesPerCount(sensitivity);
            float yawDelta = delta.x * degreesPerCount;
            yaw = Mathf.Repeat(yaw + yawDelta, 360f);
            accumulatedYaw += yawDelta;
            pitch = Mathf.Clamp(pitch + (invertY ? -delta.y : delta.y) * degreesPerCount, -maxPitch, maxPitch);
            ApplyLookRotation();
        }

        void ApplyLookRotation()
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (playerCamera != null) playerCamera.transform.localRotation = Quaternion.Euler(-pitch, 0f, 0f);
        }

        void UpdateCrouch(bool wantCrouch, MovementTuning t, float dt)
        {
            bool canStand = wantCrouch || crouchAmount <= 0f || HasHeadroomToStand(t);
            crouchAmount = MovementModel.StepCrouch(crouchAmount, wantCrouch, canStand, t.crouchTransitionTime, dt);
            // 运行中在 Inspector 里改了身高参数时也要更新碰撞体，所以按目标身高判断，而不是只看下蹲程度。
            if (!Mathf.Approximately(BodyHeight(t), appliedHeight)) ApplyBodyDimensions(t);
        }

        void UpdateJumpRequest(bool pressed, MovementTuning t, float dt)
        {
            if (pressed)
            {
                jumpRequested = true;
                jumpRequestAge = 0f;
            }
            else if (jumpRequested)
            {
                jumpRequestAge += dt;
                if (jumpRequestAge > t.jumpBufferTime) jumpRequested = false;
            }
        }

        bool HasHeadroomToStand(MovementTuning t)
        {
            // 用略小于身体的胶囊检查站立后的空间，避免贴着墙时误判。
            float radius = body.radius * 0.95f;
            Vector3 feet = transform.position;
            Vector3 bottom = feet + Vector3.up * (body.radius + 0.05f);
            Vector3 top = feet + Vector3.up * Mathf.Max(t.standingHeight - body.radius, body.radius + 0.05f);

            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, radius, overlapBuffer,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = overlapBuffer[i];
                if (hit != body && !hit.transform.IsChildOf(transform)) return false;
            }
            return true;
        }

        float BodyHeight(MovementTuning t) =>
            Mathf.Max(Mathf.Lerp(t.standingHeight, t.crouchHeight, crouchAmount), body.radius * 2f + 0.01f);

        void ApplyBodyDimensions(MovementTuning t)
        {
            float height = BodyHeight(t);
            appliedHeight = height;
            body.height = height;
            body.center = new Vector3(0f, height * 0.5f, 0f);
            UpdateEyeHeight(t);
        }

        void UpdateEyeHeight(MovementTuning t)
        {
            if (playerCamera == null) return;
            float eye = Mathf.Lerp(t.standingEyeHeight, t.crouchEyeHeight, crouchAmount);
            playerCamera.transform.localPosition = new Vector3(0f, eye, 0f);
        }

        void UpdateFieldOfView()
        {
            if (playerCamera == null) return;
            float aspect = playerCamera.aspect;
            if (Mathf.Approximately(aspect, appliedAspect) && Mathf.Approximately(horizontalFov, appliedHorizontalFov)) return;
            playerCamera.fieldOfView = ViewMath.HorizontalToVerticalFov(horizontalFov, aspect);
            appliedAspect = aspect;
            appliedHorizontalFov = horizontalFov;
        }

        void UpdateState(bool walkHeld)
        {
            if (!grounded) State = MoveState.Airborne;
            else if (HorizontalSpeed < 0.1f) State = MoveState.Idle;
            else if (crouchAmount > 0.5f) State = MoveState.Crouching;
            else if (walkHeld) State = MoveState.Walking;
            else State = MoveState.Running;
        }

        void HandleCursor(InputFrame input)
        {
            if (input.EscapePressed) UnlockCursor();
            else if (input.AttackPressed && !IsCursorLocked) LockCursor();
        }
    }
}
