using FaaTools.Core.Synagogue.Model;

namespace FaaTools.Core.Synagogue;

/// <summary>One requested "snapshot" (e.g. "Existing Building", "Recommended Building") to render.</summary>
public sealed record SnapshotData(
    string Title,
    SpaceAllocationOption Option,
    IReadOnlyList<SpaceSnapshotNode> Roots,
    double NetArea,
    double GrossArea);
