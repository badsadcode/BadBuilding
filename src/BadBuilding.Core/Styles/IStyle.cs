using BadBuilding.Generation;

namespace BadBuilding.Styles;

/// <summary>
/// An architectural family: how it infers what the map does not say (storeys, storey height), its materials, and how
/// it builds massing → facade rhythm → structural details → small details (Noor §3).
/// </summary>
public interface IStyle
{
    string Id { get; }
    float Plinth { get; }
    float RoofExtra { get; }
    int InferLevels(BuildingGenerator.Context c);
    float StoreyHeight(BuildingGenerator.Context c);
    /// <summary>The roof when the map says nothing (an OSM roof:shape name: flat, gabled, hipped, pyramidal, skillion).</summary>
    string InferRoof(BuildingGenerator.Context c);
    /// <summary>Pitch in degrees for a pitched roof of this style (the map's roof:height wins).</summary>
    float RoofPitch(BuildingGenerator.Context c);
    void Materials(BuildingGenerator.Context c);
    void Build(BuildingGenerator.Context c);
}
