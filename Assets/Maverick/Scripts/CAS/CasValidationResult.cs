namespace EaglePhysicalAI.CAS
{
    [System.Serializable]
    public struct CasValidationResult
    {
        public bool allowed;
        public string reason;
        public float targetConfidence;
        public float friendlyRisk;
        public float geometryScore;

        public static CasValidationResult Allow(string reason, float targetConfidence, float friendlyRisk, float geometryScore)
        {
            return new CasValidationResult
            {
                allowed = true,
                reason = reason,
                targetConfidence = targetConfidence,
                friendlyRisk = friendlyRisk,
                geometryScore = geometryScore
            };
        }

        public static CasValidationResult Deny(string reason, float targetConfidence = 0f, float friendlyRisk = 1f, float geometryScore = 0f)
        {
            return new CasValidationResult
            {
                allowed = false,
                reason = reason,
                targetConfidence = targetConfidence,
                friendlyRisk = friendlyRisk,
                geometryScore = geometryScore
            };
        }
    }
}
