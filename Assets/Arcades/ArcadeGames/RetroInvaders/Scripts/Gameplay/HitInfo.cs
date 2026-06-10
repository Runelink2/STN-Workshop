using UnityEngine;

namespace RetroInvaders {
    public struct HitInfo {
        public readonly int Damage;
        public readonly Team AttackerTeam;
        public readonly Vector3 Point;
        public readonly Vector3 Direction;
        public readonly GameObject Source;

        public HitInfo(int damage, Team attackerTeam, Vector3 point, Vector3 direction, GameObject source) {
            Damage = Mathf.Max(0, damage);
            AttackerTeam = attackerTeam;
            Point = point;
            Direction = direction;
            Source = source;
        }
    }
}
