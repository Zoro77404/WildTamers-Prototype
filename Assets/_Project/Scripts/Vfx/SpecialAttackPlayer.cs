using UnityEngine;
using WildTamers.Animals;
using WildTamers.Audio;

namespace WildTamers.Vfx
{
    /// <summary>
    /// Shared rules for showing an animal's special attack (used by the battle and by the "Preview Special" button):
    /// where the effect goes, which way it faces, when it has to start so its hit lines up with the impact, and its sound.
    /// </summary>
    public static class SpecialAttackPlayer
    {
        /// <summary>Longest wind-up a special may get while its effect builds up (seconds).</summary>
        public const float MaxCharge = 2.2f;

        /// <summary>Real seconds from spawning the effect until its hit moment.</summary>
        public static float HitLead(SpecialAttackFx fx) =>
            fx == null || fx.vfx == null ? 0f : fx.vfx.hitTime / Mathf.Max(0.05f, fx.speed);

        /// <summary>Real seconds the effect stays on screen.</summary>
        public static float Duration(SpecialAttackFx fx) =>
            fx == null || fx.vfx == null ? 0f : fx.vfx.Duration / Mathf.Max(0.05f, fx.speed);

        /// <summary>
        /// Plans a special attack whose impact comes <paramref name="lunge"/> seconds after its charge-up ends.
        /// Returns the charge-up time (at least <paramref name="minCharge"/>, longer if the effect needs time to build before its hit)
        /// and, in <paramref name="spawnAt"/>, the seconds after the attack starts at which to spawn the effect.
        /// </summary>
        public static float PlanCharge(SpecialAttackFx fx, float minCharge, float lunge, out float spawnAt)
        {
            float charge = minCharge;
            spawnAt = 0f;
            if (fx == null || fx.vfx == null) return charge;
            spawnAt = charge + lunge - HitLead(fx) + fx.delay;
            if (spawnAt < 0f)
            {
                // The effect needs longer than the normal wind-up: the animal charges longer instead of the effect starting late.
                float extra = Mathf.Min(-spawnAt, MaxCharge - minCharge);
                charge += extra;
                spawnAt = Mathf.Max(0f, spawnAt + extra);
            }
            return charge;
        }

        /// <summary>Anchor point for an effect.</summary>
        public static Vector3 Anchor(SpecialAttackFx fx, Vector3 attackerFeet, Vector3 targetBody, Vector3 targetFeet)
        {
            switch (fx.spawnAt)
            {
                case VfxAnchor.Attacker: return attackerFeet;
                case VfxAnchor.Target: return targetBody;
                default: return targetFeet;
            }
        }

        /// <summary>Height (m) of the animal an effect's base size is made for; bigger or smaller animals scale it.</summary>
        public const float ReferenceHeight = 2.4f;

        /// <summary>Size multiplier for an effect placed on an animal <paramref name="height"/> meters tall (the boss is shown much bigger).</summary>
        public static float SizeFor(float height) => Mathf.Clamp(height / ReferenceHeight, 0.75f, 1.9f);

        /// <summary>
        /// Spawns the effect. Facing: toward the camera for flat effects, else along the attack. <paramref name="sizeScale"/> fits it to
        /// the animal it sits on (see <see cref="SizeFor"/>).
        /// </summary>
        public static VfxInstance Spawn(SpecialAttackFx fx, Vector3 attackerFeet, Vector3 targetBody, Vector3 targetFeet, Camera camera,
            float sizeScale = 1f, bool manualTick = false)
        {
            if (fx == null || fx.vfx == null) return null;
            var along = targetFeet - attackerFeet;
            along.y = 0f;
            var attackFacing = along.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(along.normalized, Vector3.up) : Quaternion.identity;
            var rotation = fx.vfx.faceCamera ? VfxInstance.FacingCamera(camera, attackFacing) : attackFacing;
            var position = Anchor(fx, attackerFeet, targetBody, targetFeet) + rotation * (fx.offset * sizeScale);
            return VfxInstance.Spawn(fx.vfx, position, rotation, fx.scale * sizeScale, fx.color, fx.colorStrength, fx.speed, manualTick);
        }

        /// <summary>The special's impact sound (falls back to the Sound Library's heavy hit).</summary>
        public static void PlayImpactSound(SpecialAttackFx fx)
        {
            var audio = AudioManager.Instance;
            if (audio == null) return;
            if (fx != null && fx.sound != null) audio.PlayClip(fx.sound, fx.soundVolume, fx.soundPitch, 0.03f);
            else audio.PlaySfx(Sfx.Heavy);
        }
    }
}
