using System;
using UnityEngine;

namespace ChooGuard.App.Fps.Emergency
{
    /// <summary>What an extinguishing stream is made of: dry powder (the twin's ABC extinguishers), the wet chemical of a K-class extinguisher, or plain water from a hydrant.</summary>
    public enum ExtinguishAgent { Powder, WetChemical, Water }

    /// <summary>
    /// A fire in cooking oil (a fryer, a pan on a gas ring) behaves differently from the fires of the same class: water thrown
    /// onto burning oil flares it up and splashes it; dry powder knocks the flames down but the oil is still far above its
    /// ignition point, so they come back and the fire never goes out; only the wet chemical of a K-class extinguisher cools
    /// the oil and seals it with a soap film (fire-authority guidance, 소방청 식용유 화재: 물 금지·K급). Other fires ignore all of this.
    /// </summary>
    public sealed partial class FireHazard
    {
        /// <summary>Intensity powder cannot push a hot-oil fire below (the flames die down and stay).</summary>
        private const float OilFloor = .18f;

        /// <summary>Burning cooking oil.</summary>
        public bool Oil { get; set; }
        /// <summary>The flames cannot spread wider than the pan or the fryer tank (metres of flame radius).</summary>
        public float Footprint { get; set; } = float.PositiveInfinity;
        /// <summary>A K-class extinguisher has cooled and sealed the oil: the fire can now go out for good.</summary>
        public bool Cooled { get; private set; }
        /// <summary>Water was thrown onto the burning oil: the fire flared and splashed (raised once, for what stands close).</summary>
        public event Action<FireHazard> Splashed;
        private bool splashed;

        /// <summary>Applies <paramref name="agent"/> for a frame; <paramref name="quality"/> is the aim quality 0..1.</summary>
        public void SuppressWith(ExtinguishAgent agent, float quality, float deltaSeconds)
        {
            if (Extinguished) return;
            if (!Oil)
            {
                Suppress(quality, deltaSeconds, agent == ExtinguishAgent.Water);
                return;
            }
            switch (agent)
            {
                case ExtinguishAgent.Water:
                    // 끓는 기름에 물: 수증기가 기름을 사방으로 튀기며 불길이 솟는다.
                    Grow(.45f * quality * deltaSeconds);
                    if (!splashed) { splashed = true; Splashed?.Invoke(this); }
                    break;
                case ExtinguishAgent.WetChemical:
                    Cooled = true;
                    Suppress(quality * 1.4f, deltaSeconds);
                    break;
                default:
                    float drop = .08f * quality * (Intensity > 1f ? .15f : 1f) * deltaSeconds;
                    if (Cooled || Intensity - drop > OilFloor) Suppress(quality, deltaSeconds);
                    else Suppress(0, deltaSeconds);
                    break;
            }
        }
    }
}
