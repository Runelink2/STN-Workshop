using System.Collections.Generic;
using UnityEngine;

namespace RetroInvaders {
    public sealed class VfxSpawner : MonoBehaviour {
        [SerializeField] private GameConfig config;
        [SerializeField] private VfxFlash flashPrefab;
        [SerializeField] private Material playerMuzzleMaterial;
        [SerializeField] private Material invaderMuzzleMaterial;
        [SerializeField] private Material invaderExplosionMaterial;
        [SerializeField] private Material ufoExplosionMaterial;
        [SerializeField] private Material playerHitMaterial;
        [SerializeField] private int prewarmCount = 12;
        [SerializeField] private int maxEffects = 48;

        private readonly List<VfxFlash> effects = new List<VfxFlash>(48);
        private bool prewarmed;

        public void Configure(
            GameConfig gameConfig,
            VfxFlash prefab,
            Material playerMuzzle,
            Material invaderMuzzle,
            Material invaderExplosion,
            Material ufoExplosion,
            Material playerHit
        ) {
            config = gameConfig;
            flashPrefab = prefab;
            playerMuzzleMaterial = playerMuzzle;
            invaderMuzzleMaterial = invaderMuzzle;
            invaderExplosionMaterial = invaderExplosion;
            ufoExplosionMaterial = ufoExplosion;
            playerHitMaterial = playerHit;
            prewarmed = false;
            ClampSettings();
        }

        public void SpawnPlayerMuzzleFlash(Vector3 position) {
            Spawn(position, new Vector3(0.28f, 0.28f, 0.08f), GetMuzzleDuration(), playerMuzzleMaterial);
        }

        public void SpawnInvaderMuzzleFlash(Vector3 position) {
            Spawn(position, new Vector3(0.22f, 0.22f, 0.08f), GetMuzzleDuration(), invaderMuzzleMaterial);
        }

        public void SpawnInvaderExplosion(Vector3 position) {
            Spawn(position, new Vector3(0.62f, 0.62f, 0.1f), GetExplosionDuration(), invaderExplosionMaterial);
        }

        public void SpawnUfoExplosion(Vector3 position) {
            Spawn(position, new Vector3(0.9f, 0.9f, 0.12f), GetExplosionDuration(), ufoExplosionMaterial);
        }

        public void SpawnPlayerHit(Vector3 position) {
            Spawn(position, new Vector3(0.78f, 0.78f, 0.12f), GetExplosionDuration(), playerHitMaterial);
        }

        public void Clear() {
            Prewarm();

            for (int i = 0; i < effects.Count; i++) {
                if (effects[i] != null) {
                    effects[i].Hide();
                }
            }
        }

        private void Awake() {
            ClampSettings();
        }

        private void Start() {
            Prewarm();
        }

        private void OnValidate() {
            ClampSettings();
        }

        private void Spawn(Vector3 position, Vector3 scale, float duration, Material material) {
            if (flashPrefab == null) {
                return;
            }

            Prewarm();

            VfxFlash effect = GetInactiveEffect();
            if (effect == null && effects.Count < maxEffects) {
                effect = CreateEffect();
            }

            if (effect != null) {
                effect.Play(position, scale, duration, material);
            }
        }

        private void Prewarm() {
            if (prewarmed) {
                return;
            }

            RegisterExistingEffects();

            while (effects.Count < prewarmCount && effects.Count < maxEffects && flashPrefab != null) {
                CreateEffect();
            }

            prewarmed = true;
        }

        private void RegisterExistingEffects() {
            for (int i = 0; i < transform.childCount; i++) {
                VfxFlash effect = transform.GetChild(i).GetComponent<VfxFlash>();
                if (effect != null && !effects.Contains(effect)) {
                    effect.Hide();
                    effects.Add(effect);
                }
            }
        }

        private VfxFlash GetInactiveEffect() {
            for (int i = 0; i < effects.Count; i++) {
                VfxFlash effect = effects[i];
                if (effect != null && !effect.IsActive) {
                    return effect;
                }
            }

            return null;
        }

        private VfxFlash CreateEffect() {
            VfxFlash effect = Instantiate(flashPrefab, transform);
            effect.name = "VfxFlash_" + effects.Count.ToString("00");
            effect.Hide();
            effects.Add(effect);
            return effect;
        }

        private float GetMuzzleDuration() {
            return config != null ? config.MuzzleFlashDuration : 0.08f;
        }

        private float GetExplosionDuration() {
            return config != null ? config.ExplosionDuration : 0.22f;
        }

        private void ClampSettings() {
            prewarmCount = Mathf.Max(0, prewarmCount);
            maxEffects = Mathf.Max(prewarmCount, maxEffects);
        }
    }
}
