using System;
using System.IO;
using UnityEngine;
using EaglePhysicalAI.Aircraft;
using EaglePhysicalAI.Mission;
using EaglePhysicalAI.PhysicalAI;
using EaglePhysicalAI.SensorAI;
using EaglePhysicalAI.CAS;

namespace EaglePhysicalAI.Training
{
    [Serializable]
    public class PolicyEvaluationEpisode
    {
        public int episodeIndex;
        public float duration;
        public string flightMode;
        public string sensorMode;
        public float rewardReturn;
        public float curriculumScore;
        public int successfulStrikes;
        public int abortedStrikes;
        public int friendlyFireIncidents;
        public bool crashed;
        public bool missionComplete;
        public bool missionFailed;
    }

    /// <summary>
    /// Simple in-scene evaluator. It can run timed episodes with selected AI modes and write JSONL summaries.
    /// This is intended for quick regression checks before using a real training framework.
    /// </summary>
    public class PolicyEvaluationRunner : MonoBehaviour
    {
        public bool autoRun;
        public int maxEpisodes = 5;
        public float episodeSeconds = 120f;
        public PhysicalAIControlMode flightMode = PhysicalAIControlMode.LinearPolicy;
        public SensorAIControlMode sensorMode = SensorAIControlMode.LinearPolicy;

        [Header("References")]
        public AircraftPhysicsController aircraft;
        public Rigidbody aircraftRigidbody;
        public PhysicalAIRuntimeAgent physicalRuntime;
        public SensorAIRuntimeAgent sensorRuntime;
        public PhysicalAIRewardTracker rewardTracker;
        public CurriculumMissionManager curriculum;
        public AbstractStrikeSystem strikeSystem;

        [Header("Reset Point")]
        public Transform spawnPoint;
        public Vector3 fallbackSpawnPosition = new Vector3(0f, 300f, 0f);
        public Vector3 fallbackSpawnEuler = new Vector3(0f, 0f, 0f);
        public float initialForwardSpeed = 170f;

        [Header("Debug")]
        public int currentEpisode;
        public float episodeTime;
        public bool running;
        public string outputPath;

        private StreamWriter _writer;
        private int _startSuccessful;
        private int _startAborted;
        private int _startFriendlyFire;

        private void Awake()
        {
            if (aircraft == null) aircraft = FindObjectOfType<AircraftPhysicsController>();
            if (aircraft != null && aircraftRigidbody == null) aircraftRigidbody = aircraft.rb;
            if (physicalRuntime == null) physicalRuntime = FindObjectOfType<PhysicalAIRuntimeAgent>();
            if (sensorRuntime == null) sensorRuntime = FindObjectOfType<SensorAIRuntimeAgent>();
            if (rewardTracker == null) rewardTracker = FindObjectOfType<PhysicalAIRewardTracker>();
            if (curriculum == null) curriculum = FindObjectOfType<CurriculumMissionManager>();
            if (strikeSystem == null) strikeSystem = FindObjectOfType<AbstractStrikeSystem>();
        }

        private void Start()
        {
            if (autoRun) StartEvaluation();
        }

        private void Update()
        {
            if (MaverickInput.GetKeyDown(KeyCode.F12) && !running) StartEvaluation();
            if (!running) return;

            episodeTime += Time.deltaTime;
            if (episodeTime >= episodeSeconds || IsTerminal())
            {
                FinishEpisode();
                if (currentEpisode >= maxEpisodes) StopEvaluation();
                else BeginEpisode();
            }
        }

        [ContextMenu("Start Evaluation")]
        public void StartEvaluation()
        {
            string dir = Path.Combine(Application.persistentDataPath, "EaglePhysicalAILab", "evaluations");
            Directory.CreateDirectory(dir);
            outputPath = Path.Combine(dir, "policy_eval_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".jsonl");
            _writer = new StreamWriter(outputPath, append: false);
            currentEpisode = 0;
            running = true;
            BeginEpisode();
        }

        public void StopEvaluation()
        {
            running = false;
            if (_writer != null)
            {
                _writer.Flush();
                _writer.Dispose();
                _writer = null;
            }
        }

        private void BeginEpisode()
        {
            currentEpisode++;
            episodeTime = 0f;
            ResetAircraft();
            if (physicalRuntime != null) physicalRuntime.SetMode(flightMode);
            if (sensorRuntime != null) sensorRuntime.SetMode(sensorMode);
            if (rewardTracker != null) rewardTracker.ResetEpisode();
            if (curriculum != null)
            {
                curriculum.stage = CurriculumStage.FreeFlight;
                curriculum.stageTime = 0f;
                curriculum.missionTime = 0f;
                curriculum.missionFailed = false;
                curriculum.missionComplete = false;
                curriculum.curriculumScore = 0f;
            }
            if (strikeSystem != null)
            {
                _startSuccessful = strikeSystem.successfulStrikes;
                _startAborted = strikeSystem.abortedStrikes;
                _startFriendlyFire = strikeSystem.friendlyFireIncidents;
            }
        }

        private void FinishEpisode()
        {
            if (_writer == null) return;
            var row = new PolicyEvaluationEpisode
            {
                episodeIndex = currentEpisode,
                duration = episodeTime,
                flightMode = physicalRuntime != null ? physicalRuntime.controlMode.ToString() : flightMode.ToString(),
                sensorMode = sensorRuntime != null ? sensorRuntime.controlMode.ToString() : sensorMode.ToString(),
                rewardReturn = rewardTracker != null ? rewardTracker.episodeReturn : 0f,
                curriculumScore = curriculum != null ? curriculum.curriculumScore : 0f,
                successfulStrikes = strikeSystem != null ? strikeSystem.successfulStrikes - _startSuccessful : 0,
                abortedStrikes = strikeSystem != null ? strikeSystem.abortedStrikes - _startAborted : 0,
                friendlyFireIncidents = strikeSystem != null ? strikeSystem.friendlyFireIncidents - _startFriendlyFire : 0,
                crashed = aircraft != null && aircraft.IsCrashed,
                missionComplete = curriculum != null && curriculum.missionComplete,
                missionFailed = curriculum != null && curriculum.missionFailed
            };
            _writer.WriteLine(JsonUtility.ToJson(row));
            _writer.Flush();
        }

        private bool IsTerminal()
        {
            if (aircraft != null && aircraft.IsCrashed) return true;
            if (curriculum != null && (curriculum.missionComplete || curriculum.missionFailed)) return true;
            return false;
        }

        private void ResetAircraft()
        {
            if (aircraft == null) return;
            Vector3 pos = spawnPoint != null ? spawnPoint.position : fallbackSpawnPosition;
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.Euler(fallbackSpawnEuler);
            aircraft.transform.position = pos;
            aircraft.transform.rotation = rot;
            aircraft.SetCrashed(false);
            aircraft.targetThrottle = 0.75f;
            aircraft.throttle = 0.75f;
            if (aircraftRigidbody == null) aircraftRigidbody = aircraft.rb;
            if (aircraftRigidbody != null)
            {
                aircraftRigidbody.linearVelocity = aircraft.transform.forward * initialForwardSpeed;
                aircraftRigidbody.angularVelocity = Vector3.zero;
            }
        }
    }
}
