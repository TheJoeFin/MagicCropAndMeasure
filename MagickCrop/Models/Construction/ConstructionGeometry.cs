using System.Windows;

namespace MagickCrop.Models.Construction;

/// <summary>Where a point's position comes from.</summary>
public enum ConstructionPointSource
{
    /// <summary>Placed by the user; the stored position is the truth.</summary>
    Free,

    /// <summary>Where two lines cross. Re-fitted whenever either line moves.</summary>
    LineIntersection,

    /// <summary>The centre of a circle. Re-fitted whenever the circle moves.</summary>
    CircleCenter
}

/// <summary>
/// A point in the construction. Lines and circles are defined by these, so moving one
/// moves everything that references it.
///
/// A point is either free — placed by the user, storing its own position — or derived,
/// in which case the stored position is a cache re-fitted from
/// <see cref="ParentAId"/>/<see cref="ParentBId"/> on every refresh. A derived point
/// only exists here at all because the user chose to keep it.
/// </summary>
public class ConstructionPoint
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Point Position { get; set; }

    public ConstructionPointSource Source { get; set; } = ConstructionPointSource.Free;

    /// <summary>First parent: a line for an intersection, the circle for a centre.</summary>
    public Guid ParentAId { get; set; }

    /// <summary>Second line of an intersection; unused by a circle centre.</summary>
    public Guid ParentBId { get; set; }

    public bool IsDerived => Source != ConstructionPointSource.Free;

    /// <summary>Detaches the point from its parents, leaving it where it last sat.</summary>
    public void Release()
    {
        Source = ConstructionPointSource.Free;
        ParentAId = Guid.Empty;
        ParentBId = Guid.Empty;
    }
}

/// <summary>
/// A position the construction implies but does not own — a crossing or a centre that
/// is offered to the user, and only becomes a real <see cref="ConstructionPoint"/> if
/// they keep it.
/// </summary>
public readonly record struct DerivedPointCandidate(
    ConstructionPointSource Source,
    Guid ParentAId,
    Guid ParentBId,
    Point Position);

/// <summary>
/// A line through two construction points. Points are referenced by <see cref="Guid"/>
/// rather than index so removing a point cannot silently repoint a line.
/// </summary>
public class ConstructionLine
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid StartPointId { get; set; }
    public Guid EndPointId { get; set; }

    /// <summary>
    /// When true the line is drawn past its two points to the construction bounds,
    /// so the corner it forms with a neighbouring edge is visible. Defaults to true
    /// because derived corners are the whole point of the feature.
    /// </summary>
    public bool IsExtended { get; set; } = true;

    /// <summary>
    /// When true the line's length is drawn beside it. Off by default because a
    /// construction is usually several lines and labelling every one at once buries the
    /// shape they define.
    /// </summary>
    public bool ShowMeasurement { get; set; }
}

/// <summary>
/// A circle through three construction points. Like a line it stores only the point
/// references — centre and radius are derived, so moving any of the three re-fits the
/// circle rather than dragging a stored one around.
/// </summary>
public class ConstructionCircle
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid PointAId { get; set; }
    public Guid PointBId { get; set; }
    public Guid PointCId { get; set; }

    /// <summary>
    /// When true the circle's radius, circumference, and area are drawn at its centre,
    /// without having to select it first.
    /// </summary>
    public bool ShowMeasurement { get; set; }

    public IEnumerable<Guid> PointIds
    {
        get
        {
            yield return PointAId;
            yield return PointBId;
            yield return PointCId;
        }
    }
}

/// <summary>
/// The point/line/circle graph behind a parametric construction. Pure model: no WPF
/// elements, no rendering, no solving. Corners are derived on demand by
/// <see cref="Helpers.ConstructionSolver"/> and deliberately never stored here.
/// </summary>
public class ConstructionGeometry
{
    private readonly List<ConstructionPoint> points = [];
    private readonly List<ConstructionLine> lines = [];
    private readonly List<ConstructionCircle> circles = [];

    public IReadOnlyList<ConstructionPoint> Points => points;
    public IReadOnlyList<ConstructionLine> Lines => lines;
    public IReadOnlyList<ConstructionCircle> Circles => circles;

    public Guid AddPoint(Point position)
    {
        ConstructionPoint point = new() { Position = position };
        points.Add(point);
        return point.Id;
    }

    /// <summary>
    /// Adds a point that already has an identity. Used when restoring from a DTO so
    /// the saved line references stay valid.
    /// </summary>
    public void AddPoint(
        Guid id,
        Point position,
        ConstructionPointSource source = ConstructionPointSource.Free,
        Guid parentAId = default,
        Guid parentBId = default) =>
        points.Add(new ConstructionPoint
        {
            Id = id,
            Position = position,
            Source = source,
            ParentAId = parentAId,
            ParentBId = parentBId
        });

    /// <summary>
    /// Promotes an offered crossing or centre into a point the construction owns. It
    /// stays derived — it keeps tracking its parents — but now survives them.
    /// </summary>
    public Guid KeepDerivedPoint(DerivedPointCandidate candidate)
    {
        ConstructionPoint point = new()
        {
            Position = candidate.Position,
            Source = candidate.Source,
            ParentAId = candidate.ParentAId,
            ParentBId = candidate.ParentBId
        };
        points.Add(point);
        return point.Id;
    }

    public Guid AddLine(Guid startPointId, Guid endPointId, bool isExtended = true)
    {
        ConstructionLine line = new()
        {
            StartPointId = startPointId,
            EndPointId = endPointId,
            IsExtended = isExtended
        };
        lines.Add(line);
        return line.Id;
    }

    public void AddLine(Guid id, Guid startPointId, Guid endPointId, bool isExtended, bool showMeasurement = false)
    {
        lines.Add(new ConstructionLine
        {
            Id = id,
            StartPointId = startPointId,
            EndPointId = endPointId,
            IsExtended = isExtended,
            ShowMeasurement = showMeasurement
        });
    }

    public Guid AddCircle(Guid pointAId, Guid pointBId, Guid pointCId)
    {
        ConstructionCircle circle = new()
        {
            PointAId = pointAId,
            PointBId = pointBId,
            PointCId = pointCId
        };
        circles.Add(circle);
        return circle.Id;
    }

    public void AddCircle(Guid id, Guid pointAId, Guid pointBId, Guid pointCId, bool showMeasurement = false)
    {
        circles.Add(new ConstructionCircle
        {
            Id = id,
            PointAId = pointAId,
            PointBId = pointBId,
            PointCId = pointCId,
            ShowMeasurement = showMeasurement
        });
    }

    /// <summary>
    /// Removes a point and every line or circle that referenced it — either one missing
    /// a defining point has no meaning, so neither can be left behind.
    /// </summary>
    public void RemovePoint(Guid pointId)
    {
        lines.RemoveAll(line => line.StartPointId == pointId || line.EndPointId == pointId);
        circles.RemoveAll(circle => circle.PointIds.Contains(pointId));
        points.RemoveAll(point => point.Id == pointId);
    }

    public void RemoveLine(Guid lineId) => lines.RemoveAll(line => line.Id == lineId);

    public void RemoveCircle(Guid circleId) => circles.RemoveAll(circle => circle.Id == circleId);

    public void Clear()
    {
        points.Clear();
        lines.Clear();
        circles.Clear();
    }

    public ConstructionPoint? FindPoint(Guid pointId) =>
        points.FirstOrDefault(point => point.Id == pointId);

    public ConstructionLine? FindLine(Guid lineId) =>
        lines.FirstOrDefault(line => line.Id == lineId);

    /// <summary>
    /// The line joining two points, in either direction, or null when they are not
    /// connected. Used to decide whether a pair of selected points still needs a line.
    /// </summary>
    public ConstructionLine? FindLineBetween(Guid pointA, Guid pointB) =>
        lines.FirstOrDefault(line =>
            (line.StartPointId == pointA && line.EndPointId == pointB) ||
            (line.StartPointId == pointB && line.EndPointId == pointA));

    public ConstructionCircle? FindCircle(Guid circleId) =>
        circles.FirstOrDefault(circle => circle.Id == circleId);

    /// <summary>
    /// The circle defined by three points in any order, or null when they do not already
    /// define one. Order-insensitive because the user picks the three in any sequence.
    /// </summary>
    public ConstructionCircle? FindCircleThrough(Guid pointA, Guid pointB, Guid pointC)
    {
        HashSet<Guid> wanted = [pointA, pointB, pointC];
        return circles.FirstOrDefault(circle => wanted.SetEquals(circle.PointIds));
    }

    public int IndexOfPoint(Guid pointId) =>
        points.FindIndex(point => point.Id == pointId);

    public bool MovePoint(Guid pointId, Point position)
    {
        ConstructionPoint? point = FindPoint(pointId);
        if (point is null) return false;

        point.Position = position;
        return true;
    }

    /// <summary>
    /// Finds the nearest point within <paramref name="tolerance"/>, or null.
    /// Callers must pass a tolerance already divided by the canvas zoom so the
    /// grab radius stays constant in screen space.
    /// </summary>
    public ConstructionPoint? FindPointNear(Point position, double tolerance, Guid? exclude = null)
    {
        ConstructionPoint? best = null;
        double bestDistance = tolerance;

        foreach (ConstructionPoint point in points)
        {
            if (exclude is Guid excluded && point.Id == excluded) continue;

            double distance = Helpers.GeometryMathHelper.Distance(point.Position, position);
            if (distance > bestDistance) continue;

            best = point;
            bestDistance = distance;
        }

        return best;
    }

    /// <summary>
    /// Resolves each line to its two endpoint positions, skipping any line whose
    /// endpoints have gone missing.
    /// </summary>
    public List<(Guid Id, Point Start, Point End)> GetResolvedLines()
    {
        List<(Guid, Point, Point)> resolved = [];

        foreach (ConstructionLine line in lines)
        {
            ConstructionPoint? start = FindPoint(line.StartPointId);
            ConstructionPoint? end = FindPoint(line.EndPointId);
            if (start is null || end is null) continue;

            resolved.Add((line.Id, start.Position, end.Position));
        }

        return resolved;
    }

    /// <summary>
    /// Positions the construction implies but does not own: where each pair of lines
    /// crosses, and the centre of each circle. Anything already sitting under a real
    /// point is left out — a crossing at a shared endpoint is not somewhere new to
    /// click, and offering one there would put a faint dot on every vertex.
    /// </summary>
    /// <param name="mergeTolerance">
    /// How close counts as the same place. Callers divide by the canvas zoom so the
    /// threshold means the same on screen at any magnification.
    /// </param>
    public List<DerivedPointCandidate> GetDerivedCandidates(double mergeTolerance)
    {
        List<DerivedPointCandidate> candidates = [];
        List<(Guid Id, Point Start, Point End)> resolved = GetResolvedLines();

        for (int i = 0; i < resolved.Count; i++)
        {
            for (int j = i + 1; j < resolved.Count; j++)
            {
                if (!Helpers.ConstructionSolver.TryIntersect(
                        resolved[i].Start, resolved[i].End,
                        resolved[j].Start, resolved[j].End,
                        out Point crossing))
                    continue;

                TryOfferCandidate(candidates, new DerivedPointCandidate(
                    ConstructionPointSource.LineIntersection,
                    resolved[i].Id,
                    resolved[j].Id,
                    crossing), mergeTolerance);
            }
        }

        foreach ((Guid id, Point center, double _) in GetResolvedCircles())
        {
            TryOfferCandidate(candidates, new DerivedPointCandidate(
                ConstructionPointSource.CircleCenter, id, Guid.Empty, center), mergeTolerance);
        }

        return candidates;
    }

    private void TryOfferCandidate(
        List<DerivedPointCandidate> candidates,
        DerivedPointCandidate candidate,
        double tolerance)
    {
        // Already a real point — kept earlier, or placed by hand.
        if (FindPointNear(candidate.Position, tolerance) is not null) return;

        // Several pairs of lines can cross at one spot; offer it once.
        foreach (DerivedPointCandidate offered in candidates)
        {
            if (Helpers.GeometryMathHelper.Distance(offered.Position, candidate.Position) <= tolerance)
                return;
        }

        candidates.Add(candidate);
    }

    /// <summary>
    /// Re-fits every kept derived point from its parents. A point whose parents are gone
    /// is released rather than deleted: the user kept it precisely so it would outlive
    /// them, so it stays where it last sat as an ordinary point.
    /// </summary>
    public void RefreshDerivedPoints()
    {
        foreach (ConstructionPoint point in points)
        {
            if (!point.IsDerived) continue;

            if (!HasLivingParents(point))
            {
                point.Release();
                continue;
            }

            // Parents still there but momentarily unsolvable — two lines dragged
            // parallel, or a circle's points gone collinear. Hold the last position so
            // dragging back restores the fit instead of permanently breaking it.
            if (TryResolveDerivedPosition(point, out Point position))
                point.Position = position;
        }
    }

    private bool HasLivingParents(ConstructionPoint point) => point.Source switch
    {
        ConstructionPointSource.LineIntersection =>
            FindLine(point.ParentAId) is not null && FindLine(point.ParentBId) is not null,
        ConstructionPointSource.CircleCenter =>
            FindCircle(point.ParentAId) is not null,
        _ => false
    };

    private bool TryResolveDerivedPosition(ConstructionPoint point, out Point position)
    {
        position = point.Position;

        if (point.Source == ConstructionPointSource.CircleCenter)
        {
            foreach ((Guid id, Point center, double _) in GetResolvedCircles())
            {
                if (id != point.ParentAId) continue;

                position = center;
                return true;
            }

            return false;
        }

        if (point.Source != ConstructionPointSource.LineIntersection)
            return false;

        ConstructionLine? lineA = FindLine(point.ParentAId);
        ConstructionLine? lineB = FindLine(point.ParentBId);
        if (lineA is null || lineB is null) return false;

        ConstructionPoint? a1 = FindPoint(lineA.StartPointId);
        ConstructionPoint? a2 = FindPoint(lineA.EndPointId);
        ConstructionPoint? b1 = FindPoint(lineB.StartPointId);
        ConstructionPoint? b2 = FindPoint(lineB.EndPointId);
        if (a1 is null || a2 is null || b1 is null || b2 is null) return false;

        return Helpers.ConstructionSolver.TryIntersect(
            a1.Position, a2.Position, b1.Position, b2.Position, out position);
    }

    /// <summary>
    /// Resolves each circle to the centre and radius its three points imply, skipping
    /// any whose points have gone missing or fallen into a straight line.
    /// </summary>
    public List<(Guid Id, Point Center, double Radius)> GetResolvedCircles()
    {
        List<(Guid, Point, double)> resolved = [];

        foreach (ConstructionCircle circle in circles)
        {
            ConstructionPoint? a = FindPoint(circle.PointAId);
            ConstructionPoint? b = FindPoint(circle.PointBId);
            ConstructionPoint? c = FindPoint(circle.PointCId);
            if (a is null || b is null || c is null) continue;

            if (!Helpers.GeometryMathHelper.TryGetCircumcircle(
                    a.Position, b.Position, c.Position, out Point center, out double radius))
                continue;

            resolved.Add((circle.Id, center, radius));
        }

        return resolved;
    }
}
