using System;
using AimTrainer.Combat;
using AimTrainer.Player;
using AimTrainer.Weapons;
using UnityEngine;

namespace AimTrainer.Bots
{
    /// <summary>
    /// 假人：站桩 / 横跳 / 探头 / 冲出，可以开枪反击。
    ///
    /// 反击流程：
    /// 1. 每 0.05 秒检查一次玩家是否在正前方的视野圆锥内、距离够近、中间没有墙挡着。
    /// 2. 连续看到玩家满“反应时间”（默认 0.3 秒，带随机浮动）后开始开枪，开枪前会先停下脚步。
    /// 3. 每一枪按命中率掷骰决定瞄哪里（头 / 身体 / 腿 / 打偏），再真的沿这条线发射射线：
    ///    射线先碰到掩体就打在掩体上，碰到玩家才造成伤害。
    ///
    /// 由 BotFactory 在运行时用方块拼出来，不需要预制体。头部判定框比看到的头大 20%。
    /// </summary>
    [DefaultExecutionOrder(20)]
    public sealed class BotAgent : MonoBehaviour
    {
        const float PerceptionInterval = 0.05f;
        public const float EyeHeight = 1.6f;
        const float BodyRadius = 0.3f;

        /// <summary>冲出（Swing）时的移动速度：和玩家跑步一样快。</summary>
        const float SwingSpeed = 5.4f;

        /// <summary>移动时能直接走上 / 走下的高度差（米）。更高的台阶或更深的落差当作边缘，不会走过去。</summary>
        const float StepHeight = 0.45f;

        enum PeekPhase
        {
            Hidden,
            MovingOut,
            Exposed,
            MovingIn,
        }

        readonly RaycastHit[] rayBuffer = new RaycastHit[8];
        readonly Collider[] overlapBuffer = new Collider[8];
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Health health;
        Renderer[] renderers;
        Color[] baseColors;
        Collider[] colliders;
        AudioSource audioSource;
        Transform muzzle;
        MaterialPropertyBlock block;

        BotSkill skill;
        BotBehavior behavior;
        bool canShoot;
        PlayerController player;
        Health playerHealth;
        System.Random rng;
        ReactionTimer reaction;

        Vector3 anchor;
        Vector3 strafeAxis;
        float strafeHalfWidth;
        Vector3 peekTarget;
        float homeYaw;
        float yaw;

        float strafeDirection;
        float decisionTimer;
        float holdTimer;
        PeekPhase peekPhase;
        float perceptionTimer;
        bool canSeePlayer;
        bool engaging;
        bool alerted;
        float nextShotTime;
        float flashTimer;
        float deathTimer;

        public Health Health => health;
        public bool IsDead => health != null && health.IsDead;

        /// <summary>所在点位的名称（随机刷新的假人为“随机位置”），用于按点位统计。</summary>
        public string Label { get; private set; } = "随机位置";

        /// <summary>当前能不能看到玩家（小地图用它决定是否显示这个假人）。</summary>
        public bool CanSeePlayer => canSeePlayer && !IsDead;

        /// <summary>生成的时间（Time.time）。</summary>
        public float SpawnTime { get; private set; }

        /// <summary>第一次看到玩家的时间（Time.time），没看到过为 -1。用于统计“从对上视线到击杀”的耗时。</summary>
        public float FirstSeenPlayerTime { get; private set; } = -1f;

        /// <summary>是否允许开枪（全局开关，由训练模式控制，例如暂停或回合结束时关闭）。</summary>
        public bool FireEnabled { get; set; } = true;

        /// <summary>死亡动画播完、物体即将销毁时触发。</summary>
        public event Action<BotAgent> Removed;

        /// <summary>由 BotFactory 调用，设置部件引用。</summary>
        public void BindParts(Health botHealth, Renderer[] botRenderers, Collider[] botColliders, AudioSource source,
            Transform gunMuzzle)
        {
            health = botHealth;
            renderers = botRenderers;
            colliders = botColliders;
            audioSource = source;
            muzzle = gunMuzzle;
            baseColors = new Color[renderers.Length];
            block = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(block);
                baseColors[i] = block.GetColor(ColorId);
            }
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        /// <summary>设置行为。spawnPoint 为空时按 fallbackBehavior 在当前位置活动。</summary>
        public void Initialize(BotSkill botSkill, PlayerController target, BotBehavior fallbackBehavior, bool shoots,
            BotSpawnPoint spawnPoint, int seed)
        {
            skill = botSkill ?? new BotSkill();
            player = target;
            playerHealth = target != null ? target.GetComponent<Health>() : null;
            canShoot = shoots;
            rng = new System.Random(seed);
            reaction = new ReactionTimer(rng, skill.reactionTime, skill.reactionJitter, skill.memoryTime);

            anchor = transform.position;
            homeYaw = transform.eulerAngles.y;
            yaw = homeYaw;
            strafeAxis = transform.right;
            behavior = fallbackBehavior;
            strafeHalfWidth = 1.5f;
            peekTarget = anchor + transform.right * 1.5f;
            if (spawnPoint != null)
            {
                behavior = spawnPoint.Behavior;
                strafeHalfWidth = spawnPoint.StrafeWidth * 0.5f;
                peekTarget = spawnPoint.PeekPosition;
                peekTarget.y = anchor.y;
                Label = spawnPoint.Label;
            }

            strafeDirection = rng.NextDouble() < 0.5 ? -1f : 1f;
            decisionTimer = RandomRange(0.2f, 0.7f);
            peekPhase = PeekPhase.Hidden;
            holdTimer = behavior == BotBehavior.Swing ? RandomRange(0.3f, 1.5f) : RandomRange(0.4f, 1.6f);
            perceptionTimer = RandomRange(0f, PerceptionInterval);
            SpawnTime = Time.time;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateFlash(dt);
            if (IsDead)
            {
                UpdateDeath(dt);
                return;
            }

            UpdatePerception(dt);
            bool shooting = engaging && canShoot && FireEnabled;
            if (!shooting) UpdateMovement(dt);
            UpdateFacing(dt);
            if (shooting) TryShoot();
        }

        // ---------- 感知 ----------

        void UpdatePerception(float dt)
        {
            if (player == null || (playerHealth != null && playerHealth.IsDead))
            {
                canSeePlayer = false;
                engaging = false;
                alerted = false;
                reaction?.Reset();
                return;
            }

            perceptionTimer -= dt;
            if (perceptionTimer <= 0f)
            {
                perceptionTimer += PerceptionInterval;
                canSeePlayer = CheckCanSeePlayer();
                engaging = reaction.Tick(canSeePlayer, PerceptionInterval);
                if (canSeePlayer && FirstSeenPlayerTime < 0f) FirstSeenPlayerTime = Time.time;
            }
        }

        bool CheckCanSeePlayer()
        {
            Vector3 eye = EyePosition;
            Vector3 target = player.EyePosition;
            Vector3 toTarget = target - eye;
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

            // 已经发现玩家或刚被打中时按 360° 判断（会转身追踪），否则只看正前方的圆锥。
            float halfAngle = engaging || alerted || reaction.SeenTime > 0f ? 180f : skill.visionHalfAngle;
            if (!BotMath.InVisionCone(forward.x, forward.y, forward.z, toTarget.x, toTarget.y, toTarget.z, halfAngle,
                    skill.visionRange))
            {
                return false;
            }
            return HasLineOfSight(eye, target);
        }

        bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 1e-3f) return true;
            int count = Physics.RaycastNonAlloc(from, delta / distance, rayBuffer, distance, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

            float nearestBlocker = float.MaxValue;
            float nearestPlayer = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Transform t = rayBuffer[i].collider.transform;
                if (t.IsChildOf(transform)) continue;
                if (t.IsChildOf(player.transform)) nearestPlayer = Mathf.Min(nearestPlayer, rayBuffer[i].distance);
                else nearestBlocker = Mathf.Min(nearestBlocker, rayBuffer[i].distance);
            }
            return nearestBlocker >= nearestPlayer || nearestBlocker == float.MaxValue;
        }

        // ---------- 移动 ----------

        void UpdateMovement(float dt)
        {
            switch (behavior)
            {
                case BotBehavior.Strafe:
                    UpdateStrafe(dt);
                    break;
                case BotBehavior.Peek:
                    UpdatePeek(dt);
                    break;
                case BotBehavior.Swing:
                    UpdateSwing(dt);
                    break;
            }
        }

        /// <summary>冲出：等待 → 全速冲到终点 → 停在终点。冲出途中发现玩家会停下来（和急停开枪一样），反应时间到了就开枪。</summary>
        void UpdateSwing(float dt)
        {
            switch (peekPhase)
            {
                case PeekPhase.Hidden:
                    holdTimer -= dt;
                    if (holdTimer <= 0f) peekPhase = PeekPhase.MovingOut;
                    break;
                case PeekPhase.MovingOut:
                    if (MoveTowards(peekTarget, dt, SwingSpeed)) peekPhase = PeekPhase.Exposed;
                    break;
            }
        }

        void UpdateStrafe(float dt)
        {
            decisionTimer -= dt;
            if (decisionTimer <= 0f)
            {
                double roll = rng.NextDouble();
                if (roll < 0.15) strafeDirection = 0f;
                else if (strafeDirection == 0f) strafeDirection = rng.NextDouble() < 0.5 ? -1f : 1f;
                else if (roll < 0.85) strafeDirection = -strafeDirection;
                decisionTimer = RandomRange(0.22f, 0.8f);
            }
            if (strafeDirection == 0f) return;

            Vector3 next = transform.position + strafeAxis * (strafeDirection * skill.moveSpeed * dt);
            float offset = Vector3.Dot(next - anchor, strafeAxis);
            if (Mathf.Abs(offset) > strafeHalfWidth || !SnapToGround(ref next) || IsBlocked(next))
            {
                strafeDirection = -strafeDirection;
                return;
            }
            transform.position = next;
        }

        void UpdatePeek(float dt)
        {
            switch (peekPhase)
            {
                case PeekPhase.Hidden:
                    holdTimer -= dt;
                    if (holdTimer <= 0f) peekPhase = PeekPhase.MovingOut;
                    break;
                case PeekPhase.MovingOut:
                    if (MoveTowards(peekTarget, dt, skill.moveSpeed))
                    {
                        peekPhase = PeekPhase.Exposed;
                        holdTimer = RandomRange(0.8f, 2.2f);
                    }
                    break;
                case PeekPhase.Exposed:
                    holdTimer -= dt;
                    if (holdTimer <= 0f && !canSeePlayer) peekPhase = PeekPhase.MovingIn;
                    break;
                case PeekPhase.MovingIn:
                    if (MoveTowards(anchor, dt, skill.moveSpeed))
                    {
                        peekPhase = PeekPhase.Hidden;
                        holdTimer = RandomRange(0.6f, 2f);
                    }
                    break;
            }
        }

        /// <summary>
        /// 水平方向朝目标走一步，高度贴着地面（能上下台阶和斜坡）。走不过去（被挡住、前面是边缘）或已经到达时返回 true。
        /// </summary>
        bool MoveTowards(Vector3 destination, float dt, float speed)
        {
            Vector3 position = transform.position;
            var flat = new Vector2(destination.x - position.x, destination.z - position.z);
            float distance = flat.magnitude;
            if (distance < 0.01f) return true;
            float step = Mathf.Min(distance, speed * dt);
            Vector3 next = position + new Vector3(flat.x, 0f, flat.y) * (step / distance);
            if (!SnapToGround(ref next) || IsBlocked(next)) return true;
            transform.position = next;
            return step >= distance;
        }

        bool IsBlocked(Vector3 feet)
        {
            // 胶囊底部离地 0.3 米：低台阶和斜坡不会算成挡住。
            Vector3 bottom = feet + Vector3.up * (BodyRadius + 0.3f);
            Vector3 top = feet + Vector3.up * (1.8f - BodyRadius);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, BodyRadius, overlapBuffer,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                if (!overlapBuffer[i].transform.IsChildOf(transform)) return true;
            }
            return false;
        }

        /// <summary>
        /// 找脚下的地面并把 feet.y 改成地面高度：只认上下 StepHeight 以内的地面。
        /// 找不到（前面是箱子边缘、高台边缘或太高的台阶）时返回 false，假人不会走到半空中。
        /// </summary>
        bool SnapToGround(ref Vector3 feet)
        {
            Vector3 origin = feet + Vector3.up * (StepHeight + 0.05f);
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, rayBuffer, StepHeight * 2f + 0.05f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (rayBuffer[i].collider.transform.IsChildOf(transform)) continue;
                nearest = Mathf.Min(nearest, rayBuffer[i].distance);
            }
            if (nearest == float.MaxValue) return false;
            feet.y = origin.y - nearest;
            return true;
        }

        void UpdateFacing(float dt)
        {
            float targetYaw = homeYaw;
            if (player != null && (engaging || canSeePlayer || alerted || behavior == BotBehavior.Strafe || !canShoot))
            {
                // 交火中、看到玩家，或者是不开枪的靶子（一直面向玩家，方便练习爆头）。
                Vector3 to = player.transform.position - transform.position;
                if (to.x * to.x + to.z * to.z > 1e-4f) targetYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            }
            yaw = Mathf.MoveTowardsAngle(yaw, targetYaw, skill.turnSpeed * dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // ---------- 开枪 ----------

        void TryShoot()
        {
            if (Time.time < nextShotTime || player == null) return;
            nextShotTime = Time.time + skill.fireInterval * RandomRange(0.85f, 1.2f);

            // 射线从假人眼睛发出（和视线判定同一个起点），弹道线从枪口画出。
            Vector3 from = EyePosition;
            Vector3 muzzlePoint = muzzle != null ? muzzle.position : from;
            Vector3 eye = player.EyePosition;
            Vector3 feet = player.transform.position;
            float distance = Vector3.Distance(EyePosition, eye);
            float chance = BotMath.AdjustedHitChance(skill.hitChance, player.HorizontalSpeed, player.Tuning.runSpeed,
                player.IsGrounded, player.CrouchAmount, distance);
            BotShotOutcome outcome = BotMath.RollShot(rng, chance, skill.headshotChance);

            // 掷骰决定瞄准点，然后真的沿这条线发射射线：玩家躲在掩体后面时，子弹会打在掩体上。
            // 瞄准点按玩家当前身高取比例，蹲下时身体和腿的位置也跟着变低。
            Vector3 aim;
            HitZone zone;
            switch (outcome)
            {
                case BotShotOutcome.Head:
                    aim = eye;
                    zone = HitZone.Head;
                    break;
                case BotShotOutcome.Body:
                    aim = Vector3.Lerp(feet, eye, 0.65f);
                    zone = HitZone.Body;
                    break;
                case BotShotOutcome.Legs:
                    aim = Vector3.Lerp(feet, eye, 0.3f);
                    zone = HitZone.Legs;
                    break;
                default:
                    Vector3 side = Vector3.Cross(Vector3.up, (eye - from).normalized);
                    aim = eye + side * (RandomRange(0.6f, 1.2f) * (rng.NextDouble() < 0.5 ? -1f : 1f)) +
                          Vector3.up * RandomRange(-0.4f, 0.7f);
                    zone = HitZone.Body;
                    break;
            }

            Vector3 direction = (aim - from).normalized;
            float range = Vector3.Distance(from, aim) + 60f;
            Vector3 impact = from + direction * range;
            if (TryRaycastIgnoringSelf(from, direction, range, out RaycastHit hit))
            {
                impact = hit.point;
                bool hitPlayer = hit.collider.transform.IsChildOf(player.transform);
                if (hitPlayer && outcome != BotShotOutcome.Miss && playerHealth != null)
                {
                    float damage = zone == HitZone.Head ? 160f : zone == HitZone.Legs ? 34f : 40f;
                    playerHealth.ApplyDamage(new DamageInfo(damage, zone, hit.point, direction, gameObject));
                }
                else if (!hitPlayer && hit.collider.GetComponent<Hitbox>() == null)
                {
                    FxPool.BulletHole(hit.point, hit.normal);
                }
            }

            FxPool.Tracer(muzzlePoint, impact, new Color(1f, 0.45f, 0.35f, 0.9f));
            if (audioSource != null) audioSource.PlayOneShot(Audio.ProceduralSounds.EnemyShot, 0.9f);
        }

        bool TryRaycastIgnoringSelf(Vector3 from, Vector3 direction, float range, out RaycastHit nearest)
        {
            int count = Physics.RaycastNonAlloc(from, direction, rayBuffer, range, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);
            nearest = default;
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (rayBuffer[i].collider.transform.IsChildOf(transform)) continue;
                if (rayBuffer[i].distance >= best) continue;
                best = rayBuffer[i].distance;
                nearest = rayBuffer[i];
            }
            return best < float.MaxValue;
        }

        // ---------- 受击与死亡 ----------

        void OnDamaged(Health h, DamageInfo info, DamageResult result)
        {
            flashTimer = 0.06f;
            SetTint(Color.white);
            // 被打中的假人会立刻注意到玩家（不再受视野圆锥限制），但仍然要经过反应时间才能还手。
            alerted = true;
        }

        void OnDied(Health h, DamageInfo info, DamageResult result)
        {
            foreach (Collider c in colliders)
            {
                if (c != null) c.enabled = false;
            }
            deathTimer = 0f;
        }

        void UpdateFlash(float dt)
        {
            if (flashTimer <= 0f) return;
            flashTimer -= dt;
            if (flashTimer <= 0f) RestoreTint();
        }

        void UpdateDeath(float dt)
        {
            deathTimer += dt;
            float k = Mathf.Clamp01(deathTimer / 0.25f);
            transform.rotation = Quaternion.Euler(-80f * k, yaw, 0f);
            if (deathTimer < 0.6f) return;
            Removed?.Invoke(this);
            Removed = null;
            Destroy(gameObject);
        }

        void SetTint(Color color)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(block);
                block.SetColor(ColorId, color);
                block.SetColor(BaseColorId, color);
                renderers[i].SetPropertyBlock(block);
            }
        }

        void RestoreTint()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(block);
                block.SetColor(ColorId, baseColors[i]);
                block.SetColor(BaseColorId, baseColors[i]);
                renderers[i].SetPropertyBlock(block);
            }
        }

        Vector3 EyePosition => transform.position + Vector3.up * EyeHeight;

        float RandomRange(float min, float max) => min + (float)rng.NextDouble() * (max - min);

        void OnDestroy()
        {
            if (health == null) return;
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
    }
}
