using System;
using UnityEngine;

namespace CarWash.Data
{
    /// <summary>All tunable numbers (single asset). Source: Data/Source/balance.json. Never hardcode balance in code.</summary>
    public class BalanceData : ScriptableObject
    {
        public PlayerTuning player = new PlayerTuning();
        public GrabTuning grab = new GrabTuning();
        public EconomyTuning economy = new EconomyTuning();
        public DirtTuning dirt = new DirtTuning();
        public CritterTuning critter = new CritterTuning();
        public ToolTuning tools = new ToolTuning();

        [Serializable]
        public class PlayerTuning
        {
            public float walkSpeed = 4.5f, sprintSpeed = 7f, crouchSpeed = 2.5f, jumpSpeed = 4.8f;
            public float accel = 40f, airAccel = 8f, friction = 10f, foamFriction = 1.2f, gravity = 18f;
            public float knockbackDecay = 6f, knockdownSpeed = 9f, knockedTime = 1.5f;
            public float soakedSlow = 0.1f, soakedTime = 3f, stungSlow = 0.3f, stungTime = 3f;
            public float interactRange = 2.5f, lookSensitivity = 0.1f, fov = 90f;
            public float respawnY = -20f, pvpKnockbackScale = 0.4f;
        }

        [Serializable]
        public class GrabTuning
        {
            public float holdStiffness = 15f, maxMass = 40f, throwImpulse = 14f, chargeTime = 1f, holdDistance = 1.6f, grabRange = 2.5f;
        }

        [Serializable]
        public class EconomyTuning
        {
            public int startMoney = 50;
            public float[] cleanThresholds = { 0.6f, 0.8f, 0.95f };
            public float[] cleanMults = { 0f, 0.6f, 1.0f, 1.3f };
            public float fastFinishFraction = 0.5f, fastMult = 1.2f, overtimePenaltyPerSec = 0.01f, minSpeedMult = 0.5f;
            public float waxTip = 0.2f, cleanSurpriseTip = 0.1f;
            public int damagePerHit = 5; public float damageCap = 0.3f;
            public float repStart = 3f, repMax = 5f, repGain = 0.1f, repLoss = 0.3f;
            public float arrivalSlow = 30f, arrivalFast = 12f;
            public float weeklyRentIncrease = 0.4f;
            public float patience = 45f, dayLength = 240f, maxOvertime = 60f, inspectTime = 2f;
            public int vendingPerCustomer = 2;
            [Tooltip("GDD §8 player_scale = 1 + this·(players−1) (design gap filled in T2.3)")] public float arrivalScalePerPlayer = 0.25f;
        }

        [Serializable]
        public class DirtTuning
        {
            public Vector3 weights = new Vector3(0.5f, 0.3f, 0.2f);
            public float trashPenalty = 0.02f, foamDuration = 20f, spotsFraction = 0.4f;
            public float readbackInterval = 0.5f, strokeSendRate = 10f;
            public float brushMinRadius = 0.15f, brushMaxRadius = 0.4f;
        }

        [Serializable]
        public class CritterTuning
        {
            public float wetnessDecay = 5f, hpScalePerPlayer = 0.25f;
            public int captureBonus = 15;
            public float slimeSplitWetness = 50f; public int slimeMaxGen = 3; public float slimeSplitScale = 0.6f;
        }

        [Serializable]
        public class ToolTuning
        {
            public float hoseLength = 8f, hosePull = 12f, towelRadius = 1f, vacRadius = 2f, vacForce = 25f, netRange = 2.5f;
            public float sprayTickRate = 30f;
        }
    }
}
