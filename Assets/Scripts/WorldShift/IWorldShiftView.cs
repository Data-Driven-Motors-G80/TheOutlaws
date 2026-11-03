namespace Outlaws.WorldShift
{
    // Rendering visits visual states only; it must never advance gameplay twice.
    public interface IWorldShiftView
    {
        bool SplitViewValidation { get; }
        bool IsMerged { get; }
        ShiftWorld World { get; }
        float JoinedViewSplit { get; }
        float MergeStart { get; }
        float Duration { get; }
        void PresentWorldForCamera(ShiftWorld world);
    }
}
