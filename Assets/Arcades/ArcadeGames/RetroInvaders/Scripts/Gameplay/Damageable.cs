using System;
using UnityEngine;

namespace RetroInvaders {
    public sealed class Damageable : MonoBehaviour, IDamageable {
        [SerializeField] private Team team = Team.Neutral;
        [SerializeField] private int maxHealth = 1;
        [SerializeField] private int currentHealth = 1;
        [SerializeField] private bool deactivateOnDeath = true;
        [SerializeField] private bool isInvulnerable;

        public event Action<Damageable, HitInfo> OnDamaged;
        public event Action<Damageable, HitInfo> OnKilled;

        public Team Team => team;
        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public int CurrentHitPoints => currentHealth;
        public bool IsInvulnerable => isInvulnerable;
        public bool IsAlive => currentHealth > 0;

        public void Configure(Team ownerTeam, int maximumHitPoints) {
            Configure(ownerTeam, maximumHitPoints, deactivateOnDeath);
        }

        public void Configure(Team ownerTeam, int maximumHitPoints, bool shouldDeactivateOnDeath) {
            team = ownerTeam;
            maxHealth = Mathf.Max(1, maximumHitPoints);
            deactivateOnDeath = shouldDeactivateOnDeath;
            ResetHealth();
        }

        public void ResetHealth() {
            currentHealth = Mathf.Max(1, maxHealth);
        }

        public void SetInvulnerable(bool invulnerable) {
            isInvulnerable = invulnerable;
        }

        public bool CanBeDamagedBy(Team attackerTeam) {
            return CanReceiveDamage(new HitInfo(1, attackerTeam, transform.position, Vector3.zero, null));
        }

        public bool CanReceiveDamage(HitInfo hit) {
            if (!IsAlive || isInvulnerable || hit.Damage <= 0 || hit.AttackerTeam == Team.Neutral) {
                return false;
            }

            return team == Team.Neutral || team != hit.AttackerTeam;
        }

        public bool ApplyDamage(int amount, Team attackerTeam) {
            return ApplyDamage(new HitInfo(amount, attackerTeam, transform.position, Vector3.zero, null));
        }

        public bool ApplyDamage(HitInfo hit) {
            if (!CanReceiveDamage(hit)) {
                return false;
            }

            currentHealth = Mathf.Max(0, currentHealth - hit.Damage);
            OnDamaged?.Invoke(this, hit);

            if (currentHealth == 0) {
                OnKilled?.Invoke(this, hit);

                if (deactivateOnDeath) {
                    gameObject.SetActive(false);
                }
            }

            return true;
        }

        private void OnEnable() {
            ResetHealth();
        }

        private void OnValidate() {
            maxHealth = Mathf.Max(1, maxHealth);
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }
    }
}
