using System.Windows;

namespace MagickCrop.Models.Construction;

/// <summary>
/// One bounded cell of the planar arrangement formed by every construction line crossing
/// every other — not just the single outer shape <see cref="Helpers.ConstructionSolver"/>
/// solves, but every enclosed region the lines carve out, down to a single triangle.
/// </summary>
/// <param name="Ring">Corners in winding order.</param>
/// <param name="Edges">
/// The ring's edges as consecutive point pairs. Kept alongside <see cref="Ring"/> rather
/// than derived on demand because union merges faces by cancelling out edges two selected
/// faces share, and that needs the edges in exactly this per-face form.
/// </param>
public sealed record ConstructionFace(IReadOnlyList<Point> Ring, IReadOnlyList<(Point A, Point B)> Edges);
