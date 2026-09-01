namespace ChallengeShow
{
    /// <summary>Lifecycle of a single attempt by a single unit.</summary>
    public enum ChallengeState
    {
        Waiting,
        Running,
        Ragdoll,
        /// <summary>Ragdoll has settled and the unit is standing back up for another go.</summary>
        Recovering,
        Passed,
        Failed
    }

    /// <summary>Why an attempt ended. Recorded for the episode log.</summary>
    public enum ChallengeOutcomeReason
    {
        None,
        ReachedFinish,
        FellOutOfArena,
        /// <summary>Durability exhausted by repeated arm strikes.</summary>
        Overwhelmed,
        TimedOut,
        /// <summary>Shoved but not knocked down, then pinned with nowhere to go.</summary>
        Stalled,
        /// <summary>Ragdoll came to rest somewhere it cannot stand back up from.</summary>
        Unrecoverable
    }
}
