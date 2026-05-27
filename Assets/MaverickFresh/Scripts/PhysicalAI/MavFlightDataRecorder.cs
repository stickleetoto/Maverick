using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace MaverickFresh
{
    /// <summary>
    /// Physical AI step 1:
    /// Records human/player flight state and control outputs to CSV.
    /// This is the dataset foundation for later imitation learning.
    /// </summary>
    [DisallowMultipleComponent]
    public class MavFlightDataRecorder : MonoBehaviour
    {
        [Header("References")]
        public MavMouseFlightJet jet;
        public MavMouseFlightRig rig;
        public Rigidbody rb;

        [Header("Recording")]
        public bool recordOnStart = false;
        public KeyCode toggleRecordingKey = KeyCode.F9;
        public float sampleRateHz = 20f;
        public string folderName = "MaverickFresh/FlightLogs";
        public string sessionPrefix = "mav_flight";

        [Header("Runtime")]
        public bool isRecording;
        public int samplesWritten;
        public string currentPath;
        public string lastEvent = "ready";

        private StreamWriter writer;
        private float nextSampleTime;

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
            if (MavFreshInput.GetKeyDown(toggleRecordingKey))
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
            WriteHeader();

            samplesWritten = 0;
            nextSampleTime = Time.time;
            isRecording = true;
            lastEvent = "recording_started";
            Debug.Log("MavFlightDataRecorder started: " + currentPath);
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
            if (!string.IsNullOrEmpty(currentPath))
                Debug.Log("MavFlightDataRecorder stopped: " + currentPath);
        }

        [ContextMenu("Open Log Folder Path")]
        public void PrintLogFolder()
        {
            string dir = Path.Combine(Application.persistentDataPath, folderName);
            Debug.Log("MaverickFresh flight log folder: " + dir);
        }

        private void Resolve()
        {
            if (jet == null) jet = GetComponent<MavMouseFlightJet>();
            if (rig == null) rig = FindObjectOfType<MavMouseFlightRig>();
            if (rb == null) rb = GetComponent<Rigidbody>();
        }

        private void WriteHeader()
        {
            writer.WriteLine(
                "time,dt," +
                "pos_x,pos_y,pos_z,rot_x,rot_y,rot_z,rot_w," +
                "vel_x,vel_y,vel_z,angvel_x,angvel_y,angvel_z," +
                "speed,mach,g,bank,pitch_angle,throttle,effective_throttle," +
                "cmd_pitch,cmd_yaw,cmd_roll," +
                "mouse_x,mouse_y,mouse_roll_cmd,target_bank,bank_hold_cmd," +
                "angle_off_target,turn_band,pitch_authority,roll_authority," +
                "instructor_pitch,instructor_yaw,instructor_roll,instructor_throttleIntent," +
                "aoaEstimateDeg,aosEstimateDeg,verticalSpeed,localVelocityX,localVelocityY,localVelocityZ," +
                "coordinatedYawAssistOutput,state,instructor_state"
            );
        }

        private void WriteSample()
        {
            if (writer == null || jet == null || rb == null)
                return;

            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;
            Vector3 vel = rb.linearVelocity;
            Vector3 av = rb.angularVelocity;

            Vector2 cursor = rig != null ? rig.cursorViewport : new Vector2(0.5f, 0.5f);
            float mouseRoll = rig != null ? rig.screenRollCommand : 0f;

            string line = string.Format(CultureInfo.InvariantCulture,
                "{0:F4},{1:F4}," +
                "{2:F4},{3:F4},{4:F4},{5:F6},{6:F6},{7:F6},{8:F6}," +
                "{9:F4},{10:F4},{11:F4},{12:F4},{13:F4},{14:F4}," +
                "{15:F4},{16:F4},{17:F4},{18:F4},{19:F4},{20:F4},{21:F4}," +
                "{22:F4},{23:F4},{24:F4}," +
                "{25:F4},{26:F4},{27:F4},{28:F4},{29:F4}," +
                "{30:F4},{31:F4},{32:F4},{33:F4}," +
                "{34:F4},{35:F4},{36:F4},{37:F4}," +
                "{38:F4},{39:F4},{40:F4},{41:F4},{42:F4},{43:F4}," +
                "{44:F4},{45},{46}",
                Time.time, Time.deltaTime,
                pos.x, pos.y, pos.z, rot.x, rot.y, rot.z, rot.w,
                vel.x, vel.y, vel.z, av.x, av.y, av.z,
                jet.speed, jet.machEstimate, jet.gEstimate, jet.signedBankAngle, jet.signedPitchAngle, jet.throttle, jet.effectiveThrottle,
                jet.pitch, jet.yaw, jet.roll,
                cursor.x, cursor.y, mouseRoll, jet.targetBankAngle, jet.bankHoldRollCommand,
                jet.angleOffTarget, jet.turnBandFactor, jet.pitchAuthorityFactor, jet.rollAuthorityFactor,
                jet.pitch, jet.yaw, jet.roll, jet.instructor != null ? jet.instructor.throttleIntent : jet.throttle,
                jet.aoaEstimateDeg, jet.aosEstimateDeg, jet.verticalSpeed,
                jet.localVelocity.x, jet.localVelocity.y, jet.localVelocity.z,
                jet.coordinatedYawAssistOutput, Safe(jet.state), Safe(jet.instructorState)
            );

            writer.WriteLine(line);
            samplesWritten++;
        }

        private string Safe(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace(",", "_").Replace("\n", " ").Replace("\r", " ");
        }
    }
}
