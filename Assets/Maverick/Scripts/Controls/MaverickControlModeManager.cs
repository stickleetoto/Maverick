using UnityEngine;
using EaglePhysicalAI.PhysicalAI;

namespace EaglePhysicalAI.Controls
{
    public class MaverickControlModeManager : MonoBehaviour
    {
        public MaverickInstructor instructor;
        public PhysicalAIRuntimeAgent physicalAgent;

        [Header("Keys")]
        public KeyCode mouseAimKey = KeyCode.F5;
        public KeyCode assistedKey = KeyCode.F6;
        public KeyCode realisticKey = KeyCode.F7;
        public KeyCode aiManagedKey = KeyCode.F8;

        [Header("Debug")]
        public MaverickControlMode currentMode;

        private void Awake()
        {
            if (instructor == null) instructor = GetComponent<MaverickInstructor>();
            if (physicalAgent == null) physicalAgent = GetComponent<PhysicalAIRuntimeAgent>();
        }

        private void Update()
        {
            if (MaverickInput.GetKeyDown(mouseAimKey)) SetMode(MaverickControlMode.MouseAim);
            if (MaverickInput.GetKeyDown(assistedKey)) SetMode(MaverickControlMode.AssistedDirect);
            if (MaverickInput.GetKeyDown(realisticKey)) SetMode(MaverickControlMode.RealisticDirect);
            if (MaverickInput.GetKeyDown(aiManagedKey)) SetMode(MaverickControlMode.AIManaged);
        }

        public void SetMode(MaverickControlMode mode)
        {
            currentMode = mode;
            if (instructor != null)
            {
                instructor.controlMode = mode;
                instructor.inputEnabled = mode != MaverickControlMode.AIManaged;
            }

            if (physicalAgent != null && mode == MaverickControlMode.AIManaged)
                physicalAgent.SetMode(PhysicalAIControlMode.RulePilot);
        }
    }
}
