namespace EaglePhysicalAI.FAM
{
    /// <summary>
    /// Flight-only behavior vocabulary shared with the FAM research project.
    /// This bridge intentionally excludes weapon/engagement actions.
    /// </summary>
    public enum FamBehaviorToken
    {
        HOLD,
        CONTINUE,
        TURN_LEFT,
        TURN_RIGHT,
        CLIMB,
        DESCEND,
        ACCELERATE,
        DECELERATE,
        FOLLOW,
        REJOIN,
        HOLD_FORMATION,
        CLOSE_DISTANCE,
        OPEN_DISTANCE,
        STABILIZE,
        AVOID,
        RETURN
    }
}
