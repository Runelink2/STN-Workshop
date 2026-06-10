using UnityEngine;

namespace RetroInvaders {
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioManager : MonoBehaviour {
        [Header("Clips")]
        [SerializeField] private AudioClip playerShootClip;
        [SerializeField] private AudioClip invaderShootClip;
        [SerializeField] private AudioClip invaderKilledClip;
        [SerializeField] private AudioClip playerHitClip;
        [SerializeField] private AudioClip playerDeathClip;
        [SerializeField] private AudioClip ufoKilledClip;
        [SerializeField] private AudioClip waveClearClip;
        [SerializeField] private AudioClip gameOverClip;
        [SerializeField] private AudioClip fleetStepClip;

        [Header("Volume")]
        [SerializeField] private float masterVolume = 1.0f;
        [SerializeField] private float sfxVolume = 1.0f;
        [SerializeField] private float repeatCooldown = 0.04f;
        [SerializeField] private float fleetStepCooldown = 0.03f;

        [SerializeField] private AudioSource source;

        private float lastInvaderShootTime = -100.0f;
        private float lastFleetStepTime = -100.0f;

        public void Configure(GameConfig config) {
            if (config != null) {
                masterVolume = config.SfxMasterVolume;
                sfxVolume = config.SfxVolume;
            }

            CacheSource();
            ClampSettings();
        }

        public void PlayPlayerShoot() {
            Play(playerShootClip);
        }

        public void PlayInvaderShoot() {
            if (!CanPlay(ref lastInvaderShootTime, repeatCooldown)) {
                return;
            }

            Play(invaderShootClip);
        }

        public void PlayInvaderKilled() {
            Play(invaderKilledClip);
        }

        public void PlayPlayerHit() {
            Play(playerHitClip);
        }

        public void PlayPlayerDeath() {
            Play(playerDeathClip);
        }

        public void PlayUfoKilled() {
            Play(ufoKilledClip);
        }

        public void PlayWaveClear() {
            Play(waveClearClip);
        }

        public void PlayGameOver() {
            Play(gameOverClip);
        }

        public void PlayFleetStep() {
            if (!CanPlay(ref lastFleetStepTime, fleetStepCooldown)) {
                return;
            }

            Play(fleetStepClip);
        }

        private void Awake() {
            CacheSource();
            ClampSettings();
        }

        private void OnValidate() {
            CacheSource();
            ClampSettings();
        }

        private void Play(AudioClip clip) {
            if (clip == null || source == null) {
                return;
            }

            float volume = Mathf.Clamp01(masterVolume) * Mathf.Clamp01(sfxVolume);
            if (volume <= 0.0f) {
                return;
            }

            source.PlayOneShot(clip, volume);
        }

        private static bool CanPlay(ref float lastTime, float cooldown) {
            if (Time.unscaledTime - lastTime < cooldown) {
                return false;
            }

            lastTime = Time.unscaledTime;
            return true;
        }

        private void CacheSource() {
            if (source == null) {
                source = GetComponent<AudioSource>();
            }

            if (source != null) {
                source.playOnAwake = false;
                source.loop = false;
            }
        }

        private void ClampSettings() {
            masterVolume = Mathf.Clamp01(masterVolume);
            sfxVolume = Mathf.Clamp01(sfxVolume);
            repeatCooldown = Mathf.Max(0.0f, repeatCooldown);
            fleetStepCooldown = Mathf.Max(0.0f, fleetStepCooldown);
        }
    }
}
