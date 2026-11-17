using System.Windows;

namespace MagickCrop.Helpers;

/// <summary>
/// Derives shape corners from construction lines. A corner is the intersection of two
/// lines, so the user places points along an edge — where they are easy to place
/// precisely — and the corner falls out, even when it lands outside the image.
///
/// Pure static math over <see cref="Point"/>; no WPF elements, no control dependencies.
/// </summary>
public static class ConstructionSolver
{
    /// <summary>
    /// Minimum angle between two lines for their intersection to be usable. Below this
    /// the crossing point slides wildly for a sub-pixel change in either line.
    /// </summary>
    private const double MinAngleSine = 0.0872; // sin(5 degrees)

    /// <summary>A line shorter than this has no meaningful direction.</summary>
    private const double MinLineLength = 1e-6;

    /// <summary>
    /// Relative slack when testing whether a candidate corner lies inside a half-plane.
    /// A real corner sits exactly on two of the lines, so it must survive its own
    /// boundary test through floating-point error.
    /// </summary>
    private const double InsideEpsilon = 1e-6;

    /// <summary>Below this a ring has collapsed to a sliver and is not a usable shape.</summary>
    private const double MinShapeArea = 1.0;

    public enum SolveStatus
    {
        Solved,
        NotEnoughLines,
        NoUsableCorners,
        SelfIntersecting,
        Degenerate
    }

    public class SolveResult
    {
        public SolveStatus Status { get; init; }

        /// <summary>Corners in ring order. Empty unless <see cref="Status"/> is Solved.</summary>
        public IReadOnlyList<Point> Ring { get; init; } = [];

        public bool IsSolved => Status == SolveStatus.Solved;
    }

    private readonly record struct Candidate(Point Position, Guid LineA, Guid LineB);

    /// <summary>
    /// Intersects two lines given two points on each. Returns false when the lines are
    /// too close to parallel, or when either pair of points is too close together to
    /// define a direction.
    /// </summary>
    /// <remarks>
    /// The homogeneous cross product's w component scales with the input segment
    /// lengths, so it cannot be epsilon-tested directly — a short edge and a long edge
    /// at the same angle produce very different w. Testing the sine of the angle between
    /// the normalized directions instead makes the threshold mean the same thing at every
    /// scale.
    /// </remarks>
    public static bool TryIntersect(Point a1, Point b1, Point a2, Point b2, out Point intersection)
    {
        intersection = default;

        Vector d1 = b1 - a1;
        Vector d2 = b2 - a2;

        if (d1.Length < MinLineLength || d2.Length < MinLineLength)
            return false;

        d1.Normalize();
        d2.Normalize();

        double cross = (d1.X * d2.Y) - (d1.Y * d2.X);
        if (Math.Abs(cross) < MinAngleSine)
            return false;

        // Homogeneous line coefficients: l = (A.Y - B.Y, B.X - A.X, A.X*B.Y - B.X*A.Y)
        double l1a = a1.Y - b1.Y;
        double l1b = b1.X - a1.X;
        double l1c = (a1.X * b1.Y) - (b1.X * a1.Y);

        double l2a = a2.Y - b2.Y;
        double l2b = b2.X - a2.X;
        double l2c = (a2.X * b2.Y) - (b2.X * a2.Y);

        double w = (l1a * l2b) - (l2a * l1b);
        if (Math.Abs(w) < double.Epsilon)
            return false;

        double x = ((l1b * l2c) - (l2b * l1c)) / w;
        double y = ((l2a * l1c) - (l1a * l2c)) / w;

        if (double.IsNaN(x) || double.IsNaN(y) || double.IsInfinity(x) || double.IsInfinity(y))
            return false;

        intersection = new Point(x, y);
        return true;
    }

    /// <summary>
    /// Solves the shape formed by a set of construction lines. Order-independent: the
    /// user can draw the edges in any sequence.
    /// </summary>
    /// <param name="lines">Each line as its id and two defining points.</param>
    /// <param name="constructionPoints">
    /// Every placed point. Used to size the region where real corners can live, which is
    /// how vanishing points get rejected.
    /// </param>
    public static SolveResult Solve(
        IReadOnlyList<(Guid Id, Point Start, Point End)> lines,
        IReadOnlyList<Point> constructionPoints)
    {
        if (lines is null || lines.Count < 3)
            return new SolveResult { Status = SolveStatus.NotEnoughLines };

        List<Candidate> candidates = CollectCandidates(lines, constructionPoints);

        // Every line must contribute exactly two corners, and every corner must sit on
        // exactly two lines. An angular sort will happily produce a plausible-looking
        // ring out of the wrong pairs; these counts are what catch that.
        if (candidates.Count != lines.Count)
            return new SolveResult { Status = candidates.Count < lines.Count ? SolveStatus.NoUsableCorners : SolveStatus.SelfIntersecting };

        foreach ((Guid id, _, _) in lines)
        {
            int usage = candidates.Count(c => c.LineA == id || c.LineB == id);
            if (usage != 2)
                return new SolveResult { Status = SolveStatus.SelfIntersecting };
        }

        List<Point> ring = OrderAsRing([.. candidates.Select(c => c.Position)]);

        if (GeometryMathHelper.PolygonArea(ring) < MinShapeArea)
            return new SolveResult { Status = SolveStatus.Degenerate };

        return new SolveResult { Status = SolveStatus.Solved, Ring = ring };
    }

    /// <summary>
    /// A line as an inward-facing half-plane: points p with <c>n · p + c &gt;= 0</c> are on
    /// the shape's side of it. The normal is a unit vector, so the expression is a signed
    /// distance in pixels.
    /// </summary>
    private readonly record struct HalfPlane(Guid LineId, double Nx, double Ny, double C)
    {
        public double SignedDistance(Point p) => (Nx * p.X) + (Ny * p.Y) + C;
    }

    /// <summary>
    /// Intersects every pair of lines and keeps only the crossings that are actual
    /// corners of the region the lines bound.
    /// </summary>
    /// <remarks>
    /// Four edge lines produce six crossings: four corners plus the two vanishing points
    /// of the opposite-edge pairs. Distance from the construction does not separate them
    /// reliably — a trapezoid in perspective has strongly converging sides, so its
    /// vanishing point can sit closer than a legitimate corner does.
    ///
    /// Treating each line as an inward half-plane does separate them exactly: a real
    /// corner satisfies every other line's constraint, while a vanishing point always
    /// falls outside at least one. This assumes a convex shape, which also matches the
    /// angular ring ordering below.
    /// </remarks>
    private static List<Candidate> CollectCandidates(
        IReadOnlyList<(Guid Id, Point Start, Point End)> lines,
        IReadOnlyList<Point> constructionPoints)
    {
        Point centre = Centroid(constructionPoints);

        double radius = 0;
        foreach (Point point in constructionPoints)
            radius = Math.Max(radius, GeometryMathHelper.Distance(point, centre));

        double epsilon = InsideEpsilon * Math.Max(1.0, radius);

        List<HalfPlane> halfPlanes = BuildHalfPlanes(lines, centre);
        List<Candidate> candidates = [];

        for (int i = 0; i < lines.Count; i++)
        {
            for (int j = i + 1; j < lines.Count; j++)
            {
                if (!TryIntersect(lines[i].Start, lines[i].End, lines[j].Start, lines[j].End, out Point crossing))
                    continue;

                if (!IsInsideAllOtherLines(crossing, halfPlanes, lines[i].Id, lines[j].Id, epsilon))
                    continue;

                candidates.Add(new Candidate(crossing, lines[i].Id, lines[j].Id));
            }
        }

        return candidates;
    }

    private static List<HalfPlane> BuildHalfPlanes(
        IReadOnlyList<(Guid Id, Point Start, Point End)> lines,
        Point centre)
    {
        List<HalfPlane> halfPlanes = [];

        foreach ((Guid id, Point start, Point end) in lines)
        {
            Vector direction = end - start;
            if (direction.Length < MinLineLength) continue;

            direction.Normalize();

            // Normal to the line, flipped if needed so the construction's middle is on
            // the positive side.
            double nx = -direction.Y;
            double ny = direction.X;
            double c = -((nx * start.X) + (ny * start.Y));

            if ((nx * centre.X) + (ny * centre.Y) + c < 0)
            {
                nx = -nx;
                ny = -ny;
                c = -c;
            }

            halfPlanes.Add(new HalfPlane(id, nx, ny, c));
        }

        return halfPlanes;
    }

    private static bool IsInsideAllOtherLines(
        Point candidate,
        List<HalfPlane> halfPlanes,
        Guid lineA,
        Guid lineB,
        double epsilon)
    {
        foreach (HalfPlane halfPlane in halfPlanes)
        {
            // The candidate lies exactly on the two lines that made it.
            if (halfPlane.LineId == lineA || halfPlane.LineId == lineB) continue;

            if (halfPlane.SignedDistance(candidate) < -epsilon)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Sorts corners into ring order by their angle about their own centroid. For a
    /// convex shape this is the polygon boundary, independent of the order the user
    /// drew the edges in.
    /// </summary>
    private static List<Point> OrderAsRing(IReadOnlyList<Point> corners)
    {
        Point centre = Centroid(corners);

        return [.. corners.OrderBy(corner => Math.Atan2(corner.Y - centre.Y, corner.X - centre.X))];
    }

    public static Point Centroid(IReadOnlyList<Point> points)
    {
        if (points is null || points.Count == 0)
            return default;

        double x = 0;
        double y = 0;
        foreach (Point point in points)
        {
            x += point.X;
            y += point.Y;
        }

        return new Point(x / points.Count, y / points.Count);
    }

    /// <summary>
    /// True when the ring winds consistently — every turn in the same rotational
    /// direction. A ring that changes direction is a bowtie.
    /// </summary>
    public static bool IsConvexRing(IReadOnlyList<Point> ring)
    {
        if (ring is null || ring.Count < 3) return false;

        bool sawPositive = false;
        bool sawNegative = false;

        for (int i = 0; i < ring.Count; i++)
        {
            Point a = ring[i];
            Point b = ring[(i + 1) % ring.Count];
            Point c = ring[(i + 2) % ring.Count];

            double cross = ((b.X - a.X) * (c.Y - b.Y)) - ((b.Y - a.Y) * (c.X - b.X));

            if (cross > 0) sawPositive = true;
            else if (cross < 0) sawNegative = true;

            if (sawPositive && sawNegative) return false;
        }

        return true;
    }

    /// <summary>
    /// Rotates a ring so the corner nearest the top-left starts it, and orients it
    /// clockwise. <see cref="QuadrilateralDetector.DetectedQuadrilateral"/> labels
    /// corners by x+y / x-y extremes, which mislabels a strongly rotated quad; feeding
    /// it a consistently wound ring keeps the labels honest.
    /// </summary>
    public static List<Point> NormalizeWinding(IReadOnlyList<Point> ring)
    {
        if (ring is null || ring.Count < 3) return [.. ring ?? []];

        List<Point> ordered = [.. ring];

        // Shoelace sign gives the winding direction. Y grows downward on a canvas, so a
        // positive signed area is counter-clockwise on screen; flip it to clockwise.
        double signedArea = 0;
        for (int i = 0; i < ordered.Count; i++)
        {
            Point a = ordered[i];
            Point b = ordered[(i + 1) % ordered.Count];
            signedArea += (a.X * b.Y) - (b.X * a.Y);
        }

        if (signedArea > 0)
            ordered.Reverse();

        int startIndex = 0;
        double best = double.MaxValue;
        for (int i = 0; i < ordered.Count; i++)
        {
            double score = ordered[i].X + ordered[i].Y;
            if (score >= best) continue;

            best = score;
            startIndex = i;
        }

        return [.. ordered.Skip(startIndex), .. ordered.Take(startIndex)];
    }
}
