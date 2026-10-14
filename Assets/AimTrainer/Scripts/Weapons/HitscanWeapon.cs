using System;
using AimTrainer.Audio;
using AimTrainer.Combat;
using AimTrainer.Movement;
using AimTrainer.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AimTrainer.Weapons
{
    /// <summary>一发子弹的结果，供成绩统计和界面使用。</summary>
    public readonly struct ShotReport
    {
        /// <summary>是否打中了带生命值的目标（不含墙面）。</summary>
        public readonly bool HitTarget;
        public readonly HitZone Zone;
        public readonly DamageResult Damage;
        public readonly Health Target;
        public readonly float Distance;

        /// <summary>这是连射的第几发（从 1 开始）。</summary>
        public readonly int ShotIndex;

        /// <summary>这一发的总散布（度）。</summary>
        public readonly float Spread;

        public readonly AccuracyTier Tier;

        public ShotReport(bool hitTarget, HitZone zone, DamageResult damage, Health target, float distance,
            int shotIndex, float spread, AccuracyTier tier)
        {
            HitTarget = hitTarget;
            Zone = zone;
            Damage = damage;
            Target = target;
            Distance = distance;
            ShotIndex = shotIndex;
            Spread = spread;
            Tier = tier;
        }
    }

    /// <summary>
    /// 射线检测（Hitscan）武器：子弹瞬间命中，没有飞行时间。
    ///
    /// 每一发的方向 = 瞄准方向 + 固定弹道图案（后坐力，可以压枪抵消）+ 圆内随机散布。
    /// 散布 = 基础散布（随连射变大，下蹲变小）+ 移动惩罚（急停后为 0，静步较小，跑动很大，空中最大）。
    /// 射速由 FireClock 控制，与帧率无关。
    /// </summary>
    [DefaultExecutionOrder(10)]
    [DisallowMultipleComponent]
    public sealed class HitscanWeapon : MonoBehaviour
    {
        [Tooltip("枪械参数资源。留空时使用代码里的默认值。")]
        [SerializeField] WeaponDefinition definition;

        [SerializeField] PlayerController player;
        [SerializeField] Viewmodel viewmodel;
        [SerializeField] PlayerAudio playerAudio;

        [Tooltip("射线能打中的层。默认排除 Ignore Raycast 层。")]
        [SerializeField] LayerMask hitMask = Physics.DefaultRaycastLayers;

        [Tooltip("固定随机种子，方便复现弹道。0 表示每次运行都不同。")]
        [SerializeField] int randomSeed;

        readonly FireClock fireClock = new FireClock();
        readonly SprayTracker spray = new SprayTracker();
        WeaponStats defaultStats;
        WeaponStats overrideStats;
        System.Random rng;
        int ammo = -1;
        float reloadTimer;
        bool suppressUntilRelease;
        bool wasArmed;

        /// <summary>当前枪械参数：菜单选的枪（SetStats）优先，其次是 Definition 资源，最后是代码默认值。</summary>
        public WeaponStats Stats => overrideStats ?? (definition != null ? definition.stats : defaultStats ??= new WeaponStats());

        /// <summary>换枪：使用给定的参数（传 null 恢复使用 Definition 资源），并装满弹匣。</summary>
        public void SetStats(WeaponStats stats)
        {
            overrideStats = stats;
            ResetWeapon();
        }

        public int Ammo => ammo < 0 ? Stats.magazineSize : ammo;
        public bool IsReloading => reloadTimer > 0f;

        /// <summary>换弹进度 0~1，没在换弹时为 0。</summary>
        public float ReloadProgress => IsReloading ? 1f - reloadTimer / Mathf.Max(0.01f, Stats.reloadTime) : 0f;

        /// <summary>为 false 时不能开枪（菜单打开、死亡、回合结束时）。</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>如果现在开枪，第一发的散布（度），给准星和调试面板显示。</summary>
        public float CurrentSpread { get; private set; }

        public AccuracyTier CurrentTier { get; private set; }

        public event Action<ShotReport> ShotFired;

        public void Configure(WeaponDefinition weapon, PlayerController owner, Viewmodel model, PlayerAudio sounds)
        {
            definition = weapon;
            player = owner;
            viewmodel = model;
            playerAudio = sounds;
        }

        /// <summary>装满弹匣、停止换弹、清空连射计数。回合开始时调用。</summary>
        public void ResetWeapon()
        {
            ammo = Stats.magazineSize;
            reloadTimer = 0f;
            fireClock.Reset();
            spray.Reset();
            suppressUntilRelease = true;
        }

        void Awake()
        {
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (viewmodel == null) viewmodel = GetComponentInChildren<Viewmodel>();
            if (playerAudio == null) playerAudio = GetComponentInParent<PlayerAudio>();
            rng = randomSeed != 0 ? new System.Random(randomSeed) : new System.Random();
            ammo = Stats.magazineSize;
        }

        void Update()
        {
            WeaponStats stats = Stats;
            MovementTuning tuning = player.Tuning;
            float dt = Time.deltaTime;

            UpdateReload(stats, dt);

            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            bool buttonDown = mouse != null && mouse.leftButton.isPressed;
            bool armed = InputEnabled && player.InputEnabled && player.IsCursorLocked;

            // 刚锁定鼠标（点击画面或菜单按钮）的那一下不算开枪：必须松开左键再按。
            if (armed && !wasArmed && buttonDown) suppressUntilRelease = true;
            wasArmed = armed;
            if (suppressUntilRelease && !buttonDown) suppressUntilRelease = false;

            // 换弹时扳机仍算“按住”，FireClock 才不会在换弹结束的瞬间把它当成一次新的点击（半自动不会自己开一枪）。
            bool triggerHeld = armed && buttonDown && !suppressUntilRelease;

            if (armed && KeyBindings.WasPressed(keyboard, PlayerInputReader.Bindings.reload)) StartReload(stats);

            float movementSpread = WeaponMath.MovementSpread(stats, player.HorizontalSpeed, player.IsGrounded,
                tuning.accurateSpeedThreshold, tuning.WalkSpeed, tuning.runSpeed);
            CurrentTier = WeaponMath.Tier(player.HorizontalSpeed, player.IsGrounded, tuning.accurateSpeedThreshold,
                tuning.WalkSpeed);
            CurrentSpread = WeaponMath.TotalSpread(stats, spray.PeekShotIndex(Time.timeAsDouble, stats),
                player.CrouchAmount, movementSpread);

            if (IsReloading && triggerHeld && mouse.leftButton.wasPressedThisFrame && playerAudio != null)
            {
                playerAudio.PlayDryFire();
            }

            int shots = fireClock.Tick(Time.timeAsDouble, triggerHeld, stats.ShotInterval, stats.automatic);
            if (IsReloading) shots = 0;
            if (shots > 0)
            {
                // 移动的假人只改了 Transform，物理世界要到下一次物理步才更新；开枪前同步一次，
                // 射线才会和画面上看到的假人位置一致。
                Physics.SyncTransforms();
            }
            for (int i = 0; i < shots && Ammo > 0; i++) FireOne(stats, movementSpread);

            if (Ammo <= 0 && !IsReloading) StartReload(stats);
            if (viewmodel != null) viewmodel.SetReloadProgress(ReloadProgress, IsReloading);
        }

        void UpdateReload(WeaponStats stats, float dt)
        {
            if (!IsReloading) return;
            reloadTimer -= dt;
            if (reloadTimer > 0f) return;
            reloadTimer = 0f;
            ammo = stats.magazineSize;
            spray.Reset();
        }

        void StartReload(WeaponStats stats)
        {
            if (IsReloading || Ammo >= stats.magazineSize) return;
            reloadTimer = Mathf.Max(0.01f, stats.reloadTime);
            if (playerAudio != null) playerAudio.PlayReload();
        }

        void FireOne(WeaponStats stats, float movementSpread)
        {
            int shotIndex = spray.RegisterShot(Time.timeAsDouble, stats);
            float spread = WeaponMath.TotalSpread(stats, shotIndex, player.CrouchAmount, movementSpread);
            WeaponMath.RecoilPattern(stats, shotIndex, out float recoilPitch, out float recoilYaw);
            WeaponMath.SampleDisc(rng, spread, out float spreadYaw, out float spreadPitch);

            Quaternion offset = Quaternion.Euler(-(recoilPitch + spreadPitch), recoilYaw + spreadYaw, 0f);
            Vector3 direction = player.AimRotation * offset * Vector3.forward;
            Vector3 origin = player.EyePosition;
            ammo = Ammo - 1;

            bool hitTarget = false;
            HitZone zone = HitZone.Body;
            DamageResult damage = DamageResult.None;
            Health target = null;
            float distance = stats.maxRange;
            Vector3 end = origin + direction * stats.maxRange;

            // 射线从摄像机发出，起点在玩家自己的碰撞体内部，PhysX 不会命中包含起点的碰撞体。
            if (Physics.Raycast(origin, direction, out RaycastHit hit, stats.maxRange, hitMask,
                    QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                distance = hit.distance;
                if (hit.collider.TryGetComponent(out Hitbox hitbox) && hitbox.Owner != null && !hitbox.Owner.IsDead)
                {
                    zone = hitbox.Zone;
                    target = hitbox.Owner;
                    float amount = WeaponMath.Damage(stats, zone, hit.distance);
                    damage = target.ApplyDamage(new DamageInfo(amount, zone, hit.point, direction, player.gameObject));
                    hitTarget = true;
                    if (playerAudio != null) playerAudio.PlayHitConfirm(zone, damage.Killed);
                }
                else if (hit.collider.GetComponent<Hitbox>() == null)
                {
                    FxPool.BulletHole(hit.point, hit.normal);
                }
            }

            Vector3 muzzle = viewmodel != null ? viewmodel.MuzzlePosition : origin;
            FxPool.Tracer(muzzle, end, new Color(1f, 0.92f, 0.65f, 0.85f));
            if (viewmodel != null) viewmodel.Kick(stats.viewKick);
            if (playerAudio != null) playerAudio.PlayShot();

            AccuracyTier tier = WeaponMath.Tier(player.HorizontalSpeed, player.IsGrounded,
                player.Tuning.accurateSpeedThreshold, player.Tuning.WalkSpeed);
            ShotFired?.Invoke(new ShotReport(hitTarget, zone, damage, target, distance, shotIndex, spread, tier));
        }
    }
}
