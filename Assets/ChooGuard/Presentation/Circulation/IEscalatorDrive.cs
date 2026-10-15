namespace ChooGuard.Circulation
{
    /// <summary>One operating state shared by the belt parts, handrails and rider volumes.</summary>
    public interface IEscalatorDrive
    {
        bool Running { get; }
        /// <summary>Current belt speed in metres per second; zero while stopped.</summary>
        float DriveSpeed { get; }
        /// <summary>Accumulated belt travel in metres. Stopping does not reset this phase.</summary>
        double TravelDistance { get; }
    }
}
