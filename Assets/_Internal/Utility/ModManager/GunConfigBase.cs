using UnityEngine;

namespace STN.ModSDK {
    [CreateAssetMenu(fileName = "GunConfigBase", menuName = "STN/Weapons/GunConfig", order = 100)]
	public class GunConfigBase : ScriptableObject
	{
	public const float IGNORE_F = -9999f;
	public const int IGNORE_I = -9999;

	[Header("Main")]
	[Tooltip("Force applied by hitscan impact (server authority for damage).")]
	public float force = IGNORE_F;
	[Tooltip("Visual fire rate used by client effects (seconds between shots).")]
	public float visualFireRate = IGNORE_F;
	[Tooltip("Enable burst fire mode (true overrides).")]
	public bool burstFire = false;

	[Header("Accuracy / Spread")]
	[Tooltip("Default hip-fire spread (0..1).")]
	public float standardSpread = IGNORE_F;
	[Tooltip("Increase in spread per hip-fire shot.")]
	public float standardSpreadRate = IGNORE_F;
	[Tooltip("Rate at which spread returns to normal when not firing.")]
	public float spDecRate = IGNORE_F;
	[Tooltip("Maximum spread cap.")]
	public float maxSpread = IGNORE_F;
	[Tooltip("Default aim spread (0..1).")]
	public float aimSpread = IGNORE_F;
	[Tooltip("Increase in spread per shot while aiming.")]
	public float aimSpreadRate = IGNORE_F;
	[Tooltip("Multiplier to spread while crouching.")]
	public float crouchSpreadModifier = IGNORE_F;
	[Tooltip("Multiplier to spread while prone.")]
	public float proneSpreadModifier = IGNORE_F;
	[Tooltip("Multiplier to spread while moving.")]
	public float moveSpreadModifier = IGNORE_F;

	[Header("Recoil")]
	[Tooltip("Vertical recoil per shot (degrees).")]
	public float kickbackAngle = IGNORE_F;
	[Tooltip("Horizontal recoil factor relative to vertical.")]
	public float xKickbackFactor = IGNORE_F;
	[Tooltip("Maximum accumulated recoil (degrees).")]
	public float maxKickback = IGNORE_F;
	[Tooltip("Delay before recoil returns (s).")]
	public float recoilDelay = IGNORE_F;
	[Tooltip("Recoil per shot while aiming (degrees).")]
	public float kickbackAim = IGNORE_F;
	[Tooltip("Recoil multiplier while crouching.")]
	public float crouchKickbackMod = IGNORE_F;
	[Tooltip("Recoil multiplier while moving.")]
	public float moveKickbackMod = IGNORE_F;

	[Header("Range / Timing")]
	[Tooltip("Raycast range for hitscan weapons (meters).")]
	public float range = IGNORE_F;

	// Fire mode
	[Tooltip("If true, weapon can auto fire (true overrides).")]
	public bool autoFire = false;
	[Tooltip("Shots per burst (burst mode).")]
	public int burstCount = IGNORE_I;
	[Tooltip("Time to fire a full burst (s).")]
	public float burstTime = IGNORE_F;
	[Header("Visual Animation Timings")]
	[Tooltip("Delay between fire input and shot (visual sync).")]
	public float delay = IGNORE_F;
	[Tooltip("Seconds idle before idle animation plays.")]
	public float timeToIdle = IGNORE_F;
	[Tooltip("Time to equip weapon (s).")]
	public float takeOutTime = IGNORE_F;
	[Tooltip("Time to put away weapon (s).")]
	public float putAwayTime = IGNORE_F;
	[Tooltip("Time to clear jam (s).")]
	public float jamLength = IGNORE_F;
	[Tooltip("Time to play clean animation (s).")]
	public float cleanLength = IGNORE_F;

	[Header("Reload Timings")]
	[Tooltip("Enable progressive reload (true overrides).")]
	public bool progressiveReload = false;
	[Tooltip("Last-round reload time (s).")]
	public float lastRoundFiredReloadTime = IGNORE_F;
	[Tooltip("Empty reload time (s).")]
	public float emptyReloadTime = IGNORE_F;
	[Tooltip("Delay before starting reload (s).")]
	public float waitforReload = IGNORE_F;
	[Tooltip("Progressive reload enter time (s).")]
	public float reloadInTime = IGNORE_F;
	[Tooltip("Progressive reload exit time (s).")]
	public float reloadOutTime = IGNORE_F;
	[Tooltip("Partial reload time (s).")]
	public float partReloadTime = IGNORE_F;
	[Tooltip("Time for putting one bullet into empty gun (s). Use -9999 to ignore.")]
	public float emptyGunOneBulletReloadTime = IGNORE_F;

	[Header("Tracer")]
	[Tooltip("Shots per tracer (e.g., 3 = every 3rd shot). Use -9999 to ignore.")]
	public int traceEvery = IGNORE_I;
	[Tooltip("Tracer simulate time (s).")]
	public float simulateTime = IGNORE_F;
	[Header("Audio Volumes/Pitch")]
	[Tooltip("Pitch of fire sound.")]
	public float firePitch = IGNORE_F;
	[Tooltip("Volume of fire sound (0..1).")]
	public float fireVolume = IGNORE_F;
	[Tooltip("Volume of empty/dry-fire sound (0..1).")]
	public float emptyVolume = IGNORE_F;
	[Tooltip("Volume multiplier for reload sounds (0..1).")]
	public float reloadVolumes = IGNORE_F;
	[Tooltip("Volume of jam sound (0..1).")]
	public float jamVolume = IGNORE_F;
	[Tooltip("Volume of clean sound (0..1).")]
	public float cleanVolume = IGNORE_F;
	[Header("Sway")]
	[Tooltip("If true, overrides global sway rates (true overrides).")]
	public bool overwriteSway = false;
	[Tooltip("Move sway rate (x,y). Use -9999 to ignore per component.")]
	public Vector2 moveSwayRate = new Vector2(IGNORE_F, IGNORE_F);
	[Tooltip("Move sway amplitude (x,y). Use -9999 to ignore per component.")]
	public Vector2 moveSwayAmplitude = new Vector2(IGNORE_F, IGNORE_F);
	[Tooltip("Run sway rate (x,y). Use -9999 to ignore per component.")]
	public Vector2 runSwayRate = new Vector2(IGNORE_F, IGNORE_F);
	[Tooltip("Run sway amplitude (x,y). Use -9999 to ignore per component.")]
	public Vector2 runAmplitude = new Vector2(IGNORE_F, IGNORE_F);
	[Tooltip("Idle sway rate (x,y). Use -9999 to ignore per component.")]
	public Vector2 idleSwayRate = new Vector2(IGNORE_F, IGNORE_F);
	[Tooltip("Idle sway amplitude (x,y). Use -9999 to ignore per component.")]
	public Vector2 idleAmplitude = new Vector2(IGNORE_F, IGNORE_F);
	[Header("Z Kickback")]
	[Tooltip("Enable Z kickback (true overrides).")]
	public bool useZKickBack = false;
	[Tooltip("Z kickback amount.")]
	public float kickBackZ = IGNORE_F;
	[Tooltip("Z kickback return rate.")]
	public float zRetRate = IGNORE_F;
	[Tooltip("Z kickback maximum.")]
	public float maxZ = IGNORE_F;
	public bool overrideAvoidance = false;
	public bool avoids = false;
	public float dist = IGNORE_F;
	public float minDist = IGNORE_F;

	// Launcher / projectile
	[Header("Launcher/Projectile")]
	[Tooltip("If true, weapon launches projectiles (true overrides).")]
	public bool shootsProjectiles = false;
	[Tooltip("Initial projectile speed.")]
	public float initialSpeed = IGNORE_F;
	[Tooltip("Number of projectiles per shot.")]
	public int projectileCount = IGNORE_I;
	[Tooltip("Launch angle for projectile weapons.")]
	public float launchAngle = IGNORE_F;

	// Special hold-down weapons
	[Header("Hold-Down Weapons")]
	[Tooltip("If true, weapon deals damage while held (true overrides).")]
	public bool isHoldDownWeapon = false;
	[Tooltip("Camera shake while holding down.")]
	public float holdDownCameraShake = IGNORE_F;

	// Shell ejection visuals
	[Header("Shell Ejection")]
	[Tooltip("If true, shell ejection effects enabled (true overrides).")]
	public bool shellEjection = false;
	[Tooltip("Delay before shell ejection.")]
	public float ejectDelay = IGNORE_F;
}
}
