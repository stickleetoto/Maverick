namespace EaglePhysicalAI.Controls
{
    public enum MaverickFlapState
    {
        Retracted = 0,
        Combat = 1,
        Takeoff = 2,
        Landing = 3
    }

    public enum MaverickEngineAssistMode
    {
        Auto = 0,
        Manual = 1
    }

    public enum MaverickWTViewMode
    {
        ThirdPerson = 0,
        FreeLook = 1,
        TargetingPod = 2
    }

    public enum MaverickWTPrimaryWeapon
    {
        Cannon = 0,
        SensorMark = 1
    }

    public enum MaverickWTSecondaryWeapon
    {
        AbstractCAS = 0,
        TrainingBomb = 1,
        DesignatorOnly = 2
    }
}
