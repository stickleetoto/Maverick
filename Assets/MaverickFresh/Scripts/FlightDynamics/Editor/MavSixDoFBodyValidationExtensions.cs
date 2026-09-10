#if UNITY_EDITOR
namespace MaverickFresh.FlightDynamics.EditorTools
{
    /// <summary>
    /// Editor-only compatibility shim for the integration rigs. The runtime MavSixDoFBody no
    /// longer exposes a caller-controlled physics-step token; only validation code can invoke the
    /// deterministic seam.
    /// </summary>
    public static class MavSixDoFBodyValidationExtensions
    {
        public static bool StepPhysics(
            this MaverickFresh.FlightDynamics.MavSixDoFBody body,
            float fixedDeltaTime,
            float fixedTime)
        {
            return body != null && body.StepPhysicsForValidation(fixedDeltaTime, fixedTime);
        }
    }
}
#endif
