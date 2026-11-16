using MagickCrop.Models.Construction;
using System.Windows;

namespace MagickCrop.Helpers;

/// <summary>
/// Finds every bounded cell in the planar arrangement formed by a set of construction
/// lines — every enclosed region the lines carve out, not just the single outer shape
/// <see cref="ConstructionSolver"/> solves. Three lines crossing pairwise bound one
/// triangle; a grid of lines bounds one cell per square. Clicking adjacent cells and
/// merging them is how a user builds an irregular polygon out of straight construction
/// edges.
///
/// Pure static math over <see cref="Point"/>; no WPF elements, no control dependencies.
/// </summary>
public static class ConstructionFaceSolver
{
    /// <summary>Distance below which two computed vertices are treated as the same point.</summary>
    private const double VertexMergeEpsilon = 0.75;

    /// <summary>Below this a cell has collapsed to a sliver and is not a usable shape.</summary>
    private const double MinFaceArea = 9.0;

    /// <summary>Slack when testing whether a crossing falls within both segments.</summary>
    private const double SegmentParamEpsilon = 1e-6;

    /// <summary>
    /// Finds every bounded face formed by treating each line as infinite and clipping it
    /// to <paramref name="bounds"/>. Clipping turns "infinite" into "a long segment" so the
    /// arrangement stays finite; any face that still touches the clip edge is the
    /// unbounded outside of the arrangement leaking in, not a real enclosed cell, and is
    /// dropped.
    /// </summary>
    public static List<ConstructionFace> SolveFaces(
        IReadOnlyList<(Guid Id, Point Start, Point End)> lines,
        Rect bounds)
    {
        if (lines is null || lines.Count < 3 || bounds.Width <= 0 || bounds.Height <= 0)
            return [];

        List<(Point A, Point B)> segments = [];
        foreach ((Guid _, Point start, Point end) in lines)
        {
            if (TryClipLineToBounds(start, end, bounds, out Point a, out Point b))
                segments.Add((a, b));
        }

        if (segments.Count < 3)
            return [];

        List<Point> vertexPositions = [];
        List<bool> vertexTouchesClipBounds = [];

        int FindOrAddVertex(Point p, bool isClipBoundary)
        {
            for (int i = 0; i < vertexPositions.Count; i++)
            {
                if (GeometryMathHelper.Distance(vertexPositions[i], p) <= VertexMergeEpsilon)
                {
                    vertexTouchesClipBounds[i] |= isClipBoundary;
                    return i;
                }
            }

            vertexPositions.Add(p);
            vertexTouchesClipBounds.Add(isClipBoundary);
            return vertexPositions.Count - 1;
        }

        HashSet<(int A, int B)> edgeSet = [];

        for (int i = 0; i < segments.Count; i++)
        {
            (Point a, Point b) = segments[i];

            List<(double T, int VertexIndex)> onLine =
            [
                (0, FindOrAddVertex(a, isClipBoundary: true)),
                (1, FindOrAddVertex(b, isClipBoundary: true))
            ];

            for (int j = 0; j < segments.Count; j++)
            {
                if (i == j) continue;

                (Point c, Point d) = segments[j];
                if (TrySegmentIntersect(a, b, c, d, out Point crossing, out double t))
                    onLine.Add((t, FindOrAddVertex(crossing, isClipBoundary: false)));
            }

            onLine.Sort((x, y) => x.T.CompareTo(y.T));

            for (int k = 0; k < onLine.Count - 1; k++)
            {
                int u = onLine[k].VertexIndex;
                int v = onLine[k + 1].VertexIndex;
                if (u == v) continue;

                edgeSet.Add(u < v ? (u, v) : (v, u));
            }
        }

        if (edgeSet.Count == 0)
            return [];

        List<int>[] sortedNeighbors = BuildSortedNeighbors(vertexPositions, edgeSet);

        List<ConstructionFace> faces = [];
        HashSet<(int U, int V)> visited = [];

        foreach ((int a, int b) in edgeSet)
        {
            TryTraceFace(a, b, vertexPositions, vertexTouchesClipBounds, sortedNeighbors, visited, faces);
            TryTraceFace(b, a, vertexPositions, vertexTouchesClipBounds, sortedNeighbors, visited, faces);
        }

        return faces;
    }

    /// <summary>
    /// Merges the faces the caller has selected into one or more outer boundaries. An edge
    /// shared by two selected faces is internal to the union and cancels out; an edge
    /// belonging to only one selected face is on the merged shape's outline. Tracing those
    /// surviving edges into loops is the same face-walk used to find the faces themselves,
    /// just run over this smaller edge set.
    /// </summary>
    public static List<List<Point>> UnionFaces(IReadOnlyList<ConstructionFace> faces, IEnumerable<int> selectedIndices)
    {
        Dictionary<(PointKey A, PointKey B), int> edgeCounts = [];
        Dictionary<(PointKey A, PointKey B), (Point A, Point B)> edgeLookup = [];

        foreach (int index in selectedIndices)
        {
            if (index < 0 || index >= faces.Count) continue;

            foreach ((Point a, Point b) in faces[index].Edges)
            {
                PointKey ka = new(a);
                PointKey kb = new(b);
                (PointKey, PointKey) key = ka.CompareTo(kb) <= 0 ? (ka, kb) : (kb, ka);

                edgeCounts[key] = edgeCounts.GetValueOrDefault(key) + 1;
                edgeLookup[key] = (a, b);
            }
        }

        List<(Point A, Point B)> boundary =
            [.. edgeCounts.Where(kv => kv.Value % 2 != 0).Select(kv => edgeLookup[kv.Key])];

        return TraceLoops(boundary);
    }

    #region Face arrangement

    private static List<int>[] BuildSortedNeighbors(List<Point> vertexPositions, HashSet<(int A, int B)> edgeSet)
    {
        List<int>[] adjacency = new List<int>[vertexPositions.Count];
        for (int i = 0; i < adjacency.Length; i++)
            adjacency[i] = [];

        foreach ((int u, int v) in edgeSet)
        {
            adjacency[u].Add(v);
            adjacency[v].Add(u);
        }

        List<int>[] sorted = new List<int>[vertexPositions.Count];
        for (int v = 0; v < vertexPositions.Count; v++)
        {
            Point origin = vertexPositions[v];
            List<int> neighbors = adjacency[v];
            neighbors.Sort((n1, n2) => AngleTo(origin, vertexPositions[n1]).CompareTo(AngleTo(origin, vertexPositions[n2])));
            sorted[v] = neighbors;
        }

        return sorted;
    }

    private static double AngleTo(Point from, Point to) => Math.Atan2(to.Y - from.Y, to.X - from.X);

    /// <summary>
    /// Walks the face that starts by leaving <paramref name="startU"/> toward
    /// <paramref name="startV"/>: at each vertex, turn to the next line in angular order
    /// after the one just arrived on. This is the standard planar-graph face trace — every
    /// directed edge belongs to exactly one face, so running it from every directed edge
    /// enumerates all of them, bounded and unbounded alike.
    /// </summary>
    private static void TryTraceFace(
        int startU,
        int startV,
        List<Point> vertexPositions,
        List<bool> vertexTouchesClipBounds,
        List<int>[] sortedNeighbors,
        HashSet<(int U, int V)> visited,
        List<ConstructionFace> faces)
    {
        (int U, int V) start = (startU, startV);
        if (visited.Contains(start))
            return;

        List<int> faceVertices = [];
        bool touchesClipBounds = false;
        bool closed = false;

        (int U, int V) current = start;
        int maxSteps = sortedNeighbors.Sum(n => n.Count) + 4;

        for (int step = 0; step < maxSteps; step++)
        {
            visited.Add(current);
            faceVertices.Add(current.V);
            touchesClipBounds |= vertexTouchesClipBounds[current.V];

            List<int> neighborsOfV = sortedNeighbors[current.V];
            int idx = neighborsOfV.IndexOf(current.U);
            if (idx < 0)
                return; // Malformed graph; abandon this trace rather than loop forever.

            int next = neighborsOfV[(idx + 1) % neighborsOfV.Count];
            (int U, int V) nextEdge = (current.V, next);

            if (nextEdge == start)
            {
                closed = true;
                break;
            }

            current = nextEdge;
        }

        // Ran out of steps without returning to the start half-edge: something is
        // topologically off (should not happen for a valid planar graph). Discard rather
        // than risk treating an open walk as a closed polygon.
        if (!closed || touchesClipBounds || faceVertices.Count < 3)
            return;

        double area = GeometryMathHelper.PolygonArea(faceVertices.Select(i => vertexPositions[i]).ToList());
        if (area < MinFaceArea)
            return;

        List<Point> ring = [.. faceVertices.Select(i => vertexPositions[i])];
        List<(Point, Point)> edges = [];
        for (int k = 0; k < ring.Count; k++)
            edges.Add((ring[k], ring[(k + 1) % ring.Count]));

        faces.Add(new ConstructionFace(ring, edges));
    }

    #endregion

    #region Union tracing

    /// <summary>
    /// Traces closed loops out of an unordered bag of edges, the same way
    /// <see cref="TryTraceFace"/> traces a face, but without the clip-bounds check — this
    /// edge set has no clip artefacts to filter, since it only ever contains edges copied
    /// from already-solved faces. Two loops emerge per real boundary (clockwise and
    /// counter-clockwise); duplicate windings are dropped by keeping only positive signed
    /// area, which also happens to be exactly the filter that separates a genuine outer
    /// loop from a hole if the selection has one.
    /// </summary>
    private static List<List<Point>> TraceLoops(List<(Point A, Point B)> edges)
    {
        if (edges.Count == 0)
            return [];

        List<Point> vertices = [];
        Dictionary<PointKey, int> keyToIndex = [];

        int IndexOf(Point p)
        {
            PointKey key = new(p);
            if (keyToIndex.TryGetValue(key, out int existing))
                return existing;

            vertices.Add(p);
            int index = vertices.Count - 1;
            keyToIndex[key] = index;
            return index;
        }

        HashSet<(int A, int B)> edgeIndexSet = [];
        foreach ((Point a, Point b) in edges)
        {
            int ia = IndexOf(a);
            int ib = IndexOf(b);
            if (ia == ib) continue;

            edgeIndexSet.Add(ia < ib ? (ia, ib) : (ib, ia));
        }

        List<int>[] sortedNeighbors = BuildSortedNeighbors(vertices, edgeIndexSet);

        HashSet<(int U, int V)> visited = [];
        List<List<Point>> loops = [];

        foreach ((int a, int b) in edgeIndexSet)
        {
            TryTraceLoop(a, b, vertices, sortedNeighbors, visited, loops);
            TryTraceLoop(b, a, vertices, sortedNeighbors, visited, loops);
        }

        return loops;
    }

    private static void TryTraceLoop(
        int startU,
        int startV,
        List<Point> vertices,
        List<int>[] sortedNeighbors,
        HashSet<(int U, int V)> visited,
        List<List<Point>> loops)
    {
        (int U, int V) start = (startU, startV);
        if (visited.Contains(start))
            return;

        List<int> loopVertices = [];
        (int U, int V) current = start;
        int maxSteps = sortedNeighbors.Sum(n => n.Count) + 4;
        bool closed = false;

        for (int step = 0; step < maxSteps; step++)
        {
            visited.Add(current);
            loopVertices.Add(current.V);

            List<int> neighborsOfV = sortedNeighbors[current.V];
            int idx = neighborsOfV.IndexOf(current.U);
            if (idx < 0)
                return;

            int next = neighborsOfV[(idx + 1) % neighborsOfV.Count];
            (int U, int V) nextEdge = (current.V, next);

            if (nextEdge == start)
            {
                closed = true;
                break;
            }

            current = nextEdge;
        }

        if (!closed || loopVertices.Count < 3)
            return;

        List<Point> ring = [.. loopVertices.Select(i => vertices[i])];

        // Keeping only one winding direction both drops the mirror-image duplicate every
        // loop produces and, for a selection with a hole, keeps the outer boundary over
        // the inner one — the two are wound oppositely.
        if (SignedArea(ring) <= 0)
            return;

        if (GeometryMathHelper.PolygonArea(ring) < MinFaceArea)
            return;

        loops.Add(ring);
    }

    private static double SignedArea(IReadOnlyList<Point> ring)
    {
        double area = 0;
        for (int i = 0; i < ring.Count; i++)
        {
            Point a = ring[i];
            Point b = ring[(i + 1) % ring.Count];
            area += (a.X * b.Y) - (b.X * a.Y);
        }

        return area * 0.5;
    }

    #endregion

    #region Line clipping and intersection

    /// <summary>
    /// Clips the infinite line through <paramref name="p1"/>/<paramref name="p2"/> to
    /// <paramref name="bounds"/> by first extending it far past the bounds in both
    /// directions, then clipping that long segment against the rectangle.
    /// </summary>
    private static bool TryClipLineToBounds(Point p1, Point p2, Rect bounds, out Point a, out Point b)
    {
        a = default;
        b = default;

        Vector direction = p2 - p1;
        if (direction.Length < 1e-9)
            return false;

        direction.Normalize();

        double diagonal = Math.Sqrt((bounds.Width * bounds.Width) + (bounds.Height * bounds.Height)) + 1;
        Point far1 = p1 - (direction * diagonal * 4);
        Point far2 = p1 + (direction * diagonal * 4);

        return TryLiangBarskyClip(far1, far2, bounds, out a, out b);
    }

    private static bool TryLiangBarskyClip(Point p0, Point p1, Rect rect, out Point clippedStart, out Point clippedEnd)
    {
        clippedStart = default;
        clippedEnd = default;

        double t0 = 0;
        double t1 = 1;
        double dx = p1.X - p0.X;
        double dy = p1.Y - p0.Y;

        Span<double> p = [-dx, dx, -dy, dy];
        Span<double> q = [p0.X - rect.Left, rect.Right - p0.X, p0.Y - rect.Top, rect.Bottom - p0.Y];

        for (int i = 0; i < 4; i++)
        {
            if (Math.Abs(p[i]) < 1e-12)
            {
                if (q[i] < 0) return false; // Parallel to this edge and outside it.
                continue;
            }

            double t = q[i] / p[i];
            if (p[i] < 0) t0 = Math.Max(t0, t);
            else t1 = Math.Min(t1, t);
        }

        if (t0 > t1)
            return false;

        clippedStart = new Point(p0.X + (t0 * dx), p0.Y + (t0 * dy));
        clippedEnd = new Point(p0.X + (t1 * dx), p0.Y + (t1 * dy));
        return true;
    }

    /// <summary>
    /// True segment-segment intersection (unlike <see cref="ConstructionSolver.TryIntersect"/>,
    /// which treats its inputs as infinite lines) — the arrangement is built from segments
    /// already clipped to the construction bounds, so a crossing only counts here if it
    /// falls within both of them.
    /// </summary>
    private static bool TrySegmentIntersect(Point a1, Point a2, Point b1, Point b2, out Point point, out double paramOnFirst)
    {
        point = default;
        paramOnFirst = 0;

        double d1x = a2.X - a1.X;
        double d1y = a2.Y - a1.Y;
        double d2x = b2.X - b1.X;
        double d2y = b2.Y - b1.Y;

        double denom = (d1x * d2y) - (d1y * d2x);
        if (Math.Abs(denom) < 1e-9)
            return false;

        double t = (((b1.X - a1.X) * d2y) - ((b1.Y - a1.Y) * d2x)) / denom;
        double u = (((b1.X - a1.X) * d1y) - ((b1.Y - a1.Y) * d1x)) / denom;

        if (t < -SegmentParamEpsilon || t > 1 + SegmentParamEpsilon ||
            u < -SegmentParamEpsilon || u > 1 + SegmentParamEpsilon)
            return false;

        point = new Point(a1.X + (t * d1x), a1.Y + (t * d1y));
        paramOnFirst = t;
        return true;
    }

    #endregion

    /// <summary>Rounded coordinates so shared vertices compare equal despite float noise.</summary>
    private readonly record struct PointKey(long X, long Y) : IComparable<PointKey>
    {
        public PointKey(Point p) : this((long)Math.Round(p.X * 100), (long)Math.Round(p.Y * 100)) { }

        public int CompareTo(PointKey other)
        {
            int xCompare = X.CompareTo(other.X);
            return xCompare != 0 ? xCompare : Y.CompareTo(other.Y);
        }
    }
}
