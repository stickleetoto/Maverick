using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Logs reward-like signals for future imitation/RL training.
    /// This is not training by itself. It creates measurable feedback columns.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavPhysicalAIRewardLogger : MonoBehaviour
    {
        [Header("References")]
        public MavPhysicalAIController ai;
        public MavMouseFlightJet jet;
        public MavCASWeaponSystem weapons;
        public Rigidbody rb;

        [Header("Logging")]
        public bool recordOnStart = false;
        public KeyCode toggleKey = KeyCode.F4;
        public float sampleRateHz = 10f;
        public string folderName = "MaverickFresh/PhysicalAILogs";
        public string sessionPrefix = "mav_physical_ai_reward";

        [Header("Runtime")]
        public bool isRecording;
        public int samplesWritten;
        public string currentPath;
        public float totalReward;
        public float currentReward;
        public string lastEvent = "ready";

        private StreamWriter writer;
        private float nextSampleTime;
        private int previousKills;
        private int previousHits;

        private void Awake()
        {
            Resolve();
        }

        private void Start()
        {
            if (recordOnStart)
                StartRecording();
        }

        private void Update()
        {
            if (MavFreshInput.GetKeyDown(toggleKey))
            {
                if (isRecording) StopRecording();
                else StartRecording();
            }

            if (!isRecording)
                return;

            if (Time.time >= nextSampleTime)
            {
                WriteSample();
                nextSampleTime = Time.time + 1f / Mathf.Max(1f, sampleRateHz);
            }
        }

        private void OnDisable()
        {
            StopRecording();
        }

        public void Resolve()
        {
            if (ai == null) ai = GetComponent<MavPhysicalAIController>();
            if (jet == null) jet = GetComponent<MavMouseFlightJet>();
            if (weapons == null) weapons = GetComponent<MavCASWeaponSystem>();
            if (rb == null) rb = GetComponent<Rigidbody>();
        }

        [ContextMenu("Start Recording")]
        public void StartRecording()
        {
            Resolve();

            if (isRecording)
                return;

            string dir = Path.Combine(Application.persistentDataPath, folderName);
            Directory.CreateDirectory(dir);

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            currentPath = Path.Combine(dir, sessionPrefix + "_" + timestamp + ".csv");

            writer = new StreamWriter(currentPath, false, Encoding.UTF8);
            writer.WriteLine(
                "time,mode,ai_state,speed,altitude,target_distance,target_angle,ground_distance," +
                "pitch,roll,bank,pitch_angle,g,throttle,reward,total_reward,hits,kills,last_decision"
            );

            totalReward = 0f;
            currentReward = 0f;
            previousKills = weapons != null ? weapons.destroyedCount : 0;
            previousHits = weapons != null ? weapons.hitCount : 0;
            samplesWritten = 0;
            nextSampleTime = Time.time;
            isRecording = true;
            lastEvent = "recording_started";

            Debug.Log("MavPhysicalAIRewardLogger started: " + currentPath);
        }

        [ContextMenu("Stop Recording")]
        public void StopRecording()
        {
            if (!isRecording && writer == null)
                return;

            isRecording = false;

            if (writer != null)
            {
                writer.Flush();
                writer.Close();
                writer = null;
            }

            lastEvent = "recording_stopped";
        }

        [ContextMenu("Open Log Folder Path")]
        public void PrintLogFolder()
        {
            string dir = Path.Combine(Application.persistentDataPath, folderName);
            Debug.Log("MaverickFresh Physical AI log folder: " + dir);
        }

        private void WriteSample()
        {
            Resolve();

            if (writer == null || ai == null || jet == null)
                return;

            currentReward = ComputeReward();
            totalReward += currentReward;

            int hits = weapons != null ? weapons.hitCount : 0;
            int kills = weapons != null ? weapons.destroyedCount : 0;

            string line = string.Format(CultureInfo.InvariantCulture,
                "{0:F4},{1},{2},{3:F3},{4:F3},{5:F3},{6:F3},{7:F3}," +
                "{8:F3},{9:F3},{10:F3},{11:F3},{12:F3},{13:F3},{14:F4},{15:F4},{16},{17},{18}",
                Time.time,
                ai.mode,
                Safe(ai.aiState),
                ai.speed,
                ai.altitude,
                ai.targetDistance,
                ai.targetAngle,
                ai.groundDistance,
                jet.pitch,
                jet.roll,
                jet.signedBankAngle,
                jet.signedPitchAngle,
                jet.gEstimate,
                jet.throttle,
                currentReward,
                totalReward,
                hits,
                kills,
                Safe(ai.lastDecision)
            );

            writer.WriteLine(line);
            samplesWritten++;
        }

        private float ComputeReward()
        {
            if (ai == null || jet == null)
                return 0f;

            float reward = 0f;

            // Energy management
            float speedScore = 1f - Mathf.Abs(ai.speed - ai.desiredSpeed) / Mathf.Max(1f, ai.desiredSpeed);
            reward += Mathf.Clamp(speedScore, -1f, 1f) * 0.04f;

            // Avoid ground
            if (ai.altitude < ai.minimumAltitude)
                reward -= 0.25f;

            if (ai.groundDistance > 0f && ai.groundDistance < ai.minimumAltitude * 0.75f)
                reward -= 0.35f;

            // Target alignment
            if (ai.targetDistance > 1f)
            {
                float alignScore = 1f - Mathf.Clamp01(ai.targetAngle / 90f);
                reward += alignScore * 0.05f;
            }

            // Penalize excessive G and unstable attitude
            if (Mathf.Abs(jet.gEstimate) > jet.hardGLimit)
                reward -= 0.08f;

            if (Mathf.Abs(jet.signedBankAngle) > 120f)
                reward -= 0.04f;

            // Combat events
            if (weapons != null)
            {
                if (weapons.hitCount > previousHits)
                    reward += (weapons.hitCount - previousHits) * 0.35f;

                if (weapons.destroyedCount > previousKills)
                    reward += (weapons.destroyedCount - previousKills) * 1.25f;

                previousHits = weapons.hitCount;
                previousKills = weapons.destroyedCount;
            }

            return reward;
        }

        private string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace(",", "_").Replace("\n", " ").Replace("\r", " ");
        }
    }
}
