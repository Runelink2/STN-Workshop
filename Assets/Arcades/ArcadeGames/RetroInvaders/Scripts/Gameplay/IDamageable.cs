namespace RetroInvaders {
    public interface IDamageable {
        Team Team { get; }
        int MaxHealth { get; }
        int CurrentHealth { get; }
        bool IsAlive { get; }
        bool IsInvulnerable { get; }
        bool CanReceiveDamage(HitInfo hit);
        bool ApplyDamage(HitInfo hit);
    }
}
