namespace MaverickFresh.Combat
{
    /// <summary>
    /// A request to the combat system. Not an action: nothing here fires anything, and a command
    /// source has no idea whether the request will be honoured.
    /// </summary>
    public enum MavCombatCommand
    {
        None = 0,
        FirePrimary = 1,
        FireSecondary = 2,
        SelectNextWeapon = 3,
        SelectPreviousWeapon = 4,
        QuickSelectMissile = 5,
        QuickSelectBomb = 6,
        DesignateTarget = 7,
        CycleTarget = 8,
        ClearTarget = 9,
    }

    /// <summary>
    /// Something that asks the combat system to do things: a human at a keyboard, an AI pilot, a FAM
    /// policy, a replay, or a test.
    ///
    /// WHY THIS EXISTS. Today's weapon system reads `MavFreshInput.GetKey(firePrimaryKey)` inside its
    /// own Update, which makes the keyboard the only possible trigger. Nothing else can fire without
    /// either synthesising key presses or reaching into the weapon system's internals, so AI and FAM
    /// firing has no way in that does not also mean "pretend to be a human".
    ///
    /// The split is deliberate. <see cref="IsCommandHeld"/> is for continuous actions - holding the
    /// gun trigger - and <see cref="WasCommandPressed"/> is for discrete ones, so a source that
    /// cannot express one of them is not forced to fake it.
    ///
    /// A command source must not simulate. It reports intent for the current frame and nothing else:
    /// no ammunition accounting, no cooldowns, no spawning, no damage.
    /// </summary>
    public interface IMavCombatCommandSource
    {
        /// <summary>Whether this source can currently be consulted at all.</summary>
        bool IsCommandSourceActive { get; }

        /// <summary>Continuous intent: is this command being held right now.</summary>
        bool IsCommandHeld(MavCombatCommand command);

        /// <summary>Discrete intent: did this command begin this frame.</summary>
        bool WasCommandPressed(MavCombatCommand command);
    }
}
