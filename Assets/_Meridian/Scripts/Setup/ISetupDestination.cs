namespace Meridian
{
    /// <summary>Readiness means generated visuals, collision and input state are all prepared.</summary>
    public interface ISetupDestination
    {
        bool IsReady { get; }
        bool GenerationFailed { get; }
        string FailureMessage { get; }
        void SetInteraction(bool enabled);
    }

    public interface ISetupLoading
    {
        SurfaceLoadProgress.Snapshot LoadingProgress { get; }
        void CancelLoading();
    }
}
