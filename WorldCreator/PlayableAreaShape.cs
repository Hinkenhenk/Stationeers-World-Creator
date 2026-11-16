using System;
using System.Collections.Generic;
using System.Linq;

namespace WorldCreator;

/// <summary>Edits map-normalised playable shapes and rasterises exact game region colours.</summary>
internal sealed class PlayableAreaShape
{
    internal enum ShapeKind { Polygon, Square, Circle }
    internal const int Resolution = 1024;
    internal const int WarningPixels = 16;
    internal const int MaximumCorners = 128;
    internal const double Margin = (WarningPixels + 2.0) / Resolution;
    private const double MinimumSize = 4.0 / Resolution;
    private readonly List<Point> _corners = new List<Point> { new Point(.125, .125), new Point(.875, .125), new Point(.875, .875), new Point(.125, .875) };
    private Point _squareCentre = new Point(.5, .5);
    private Point _circleCentre = new Point(.5, .5);
    private double _squareHalfSize = .375;
    private double _circleRadius = .375;
    internal ShapeKind Kind { get; set; }
    internal IReadOnlyList<Point> Corners => _corners;
    internal double Radius => Kind == ShapeKind.Circle ? _circleRadius : _squareHalfSize;
    internal Point Centre => Kind == ShapeKind.Circle ? _circleCentre : Kind == ShapeKind.Square ? _squareCentre :
        new Point((_corners.Min(p => p.X) + _corners.Max(p => p.X)) / 2, (_corners.Min(p => p.Y) + _corners.Max(p => p.Y)) / 2);

    /// <summary>Starts from an existing rectangular playable mask instead of assuming a blank-world boundary.</summary>
    internal bool SetReferenceRectangle(Point minimum, Point maximum)
    {
        minimum = Bounded(minimum);
        maximum = Bounded(maximum);
        if (maximum.X - minimum.X < 2 * MinimumSize || maximum.Y - minimum.Y < 2 * MinimumSize) return false;
        _corners.Clear();
        _corners.AddRange(new[] { minimum, new Point(maximum.X, minimum.Y), maximum, new Point(minimum.X, maximum.Y) });
        _squareCentre = _circleCentre = new Point((minimum.X + maximum.X) / 2, (minimum.Y + maximum.Y) / 2);
        _squareHalfSize = _circleRadius = Math.Min(maximum.X - minimum.X, maximum.Y - minimum.Y) / 2;
        return true;
    }

    internal readonly struct Point
    {
        internal readonly double X;
        internal readonly double Y;
        /// <summary>Stores one point with the map's upper-left corner as the origin.</summary>
        internal Point(double x, double y) { X = x; Y = y; }
    }

    /// <summary>Returns the active outline vertices; squares keep equal sides while polygon vertices stay independent.</summary>
    internal IReadOnlyList<Point> Outline => Kind == ShapeKind.Polygon ? _corners : new[] {
        new Point(Centre.X - Radius, Centre.Y - Radius), new Point(Centre.X + Radius, Centre.Y - Radius),
        new Point(Centre.X + Radius, Centre.Y + Radius), new Point(Centre.X - Radius, Centre.Y + Radius) };

    /// <summary>Moves one polygon corner, rejecting folded edges or a collapsed area without changing other corners.</summary>
    internal bool TryMoveCorner(int index, Point point)
    {
        if (index < 0 || index >= _corners.Count) return false;
        point = Bounded(point);
        var previous = _corners[index];
        _corners[index] = point;
        if (IsValidPolygon()) return true;
        _corners[index] = previous;
        return false;
    }

    /// <summary>Inserts a corner on a chosen edge, with a finite limit to keep mask generation responsive.</summary>
    internal bool TryAddCorner(int edge, Point point)
    {
        if (edge < 0 || edge >= _corners.Count || _corners.Count >= MaximumCorners) return false;
        _corners.Insert(edge + 1, Bounded(point));
        if (IsValidPolygon()) return true;
        _corners.RemoveAt(edge + 1);
        return false;
    }

    /// <summary>Removes a selected polygon corner while retaining at least three vertices and a valid outline.</summary>
    internal bool TryRemoveCorner(int index)
    {
        if (_corners.Count <= 3 || index < 0 || index >= _corners.Count) return false;
        var previous = _corners[index];
        _corners.RemoveAt(index);
        if (IsValidPolygon()) return true;
        _corners.Insert(index, previous);
        return false;
    }

    /// <summary>Clamps circle or square resizing so its warning band cannot leave the map.</summary>
    internal void SetRadius(double radius)
    {
        RequireFinite(radius);
        var centre = Centre;
        var maximum = Math.Min(Math.Min(centre.X, centre.Y) - Margin, 1 - Margin - Math.Max(centre.X, centre.Y));
        radius = Math.Max(MinimumSize, Math.Min(maximum, radius));
        if (Kind == ShapeKind.Circle) _circleRadius = radius;
        else if (Kind == ShapeKind.Square) _squareHalfSize = radius;
    }

    /// <summary>Translates the active shape as a whole, preserving its geometry and warning-band clearance.</summary>
    internal void MoveBy(double x, double y)
    {
        RequireFinite(x);
        RequireFinite(y);
        var outline = Outline;
        x = Math.Max(Margin - outline.Min(p => p.X), Math.Min(1 - Margin - outline.Max(p => p.X), x));
        y = Math.Max(Margin - outline.Min(p => p.Y), Math.Min(1 - Margin - outline.Max(p => p.Y), y));
        if (Kind == ShapeKind.Circle) _circleCentre = new Point(_circleCentre.X + x, _circleCentre.Y + y);
        else if (Kind == ShapeKind.Square) _squareCentre = new Point(_squareCentre.X + x, _squareCentre.Y + y);
        else
            for (var i = 0; i < _corners.Count; i++)
                _corners[i] = new Point(_corners[i].X + x, _corners[i].Y + y);
    }

    /// <summary>Centres the active marker without changing its size or corner arrangement.</summary>
    internal void CentreOnMap() => MoveBy(.5 - Centre.X, .5 - Centre.Y);

    /// <summary>Tests the circle, square or simple polygon, including its edge.</summary>
    internal bool Contains(Point point)
    {
        if (Kind == ShapeKind.Circle) return SquaredDistance(point, _circleCentre) <= _circleRadius * _circleRadius;
        if (Kind == ShapeKind.Square) return Math.Abs(point.X - _squareCentre.X) <= _squareHalfSize && Math.Abs(point.Y - _squareCentre.Y) <= _squareHalfSize;
        var inside = false;
        for (var i = 0; i < _corners.Count; i++)
        {
            var a = _corners[i];
            var b = _corners[(i + 1) % _corners.Count];
            if ((a.Y > point.Y) != (b.Y > point.Y) && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Finds a polygon edge for inserting a new handle or selecting the nearest edge.</summary>
    internal int NearestEdge(Point point)
    {
        var edge = 0;
        var minimum = double.MaxValue;
        for (var i = 0; i < _corners.Count; i++)
        {
            var distance = SegmentDistanceSquared(point, _corners[i], _corners[(i + 1) % _corners.Count]);
            if (distance >= minimum) continue;
            minimum = distance;
            edge = i;
        }
        return edge;
    }

    /// <summary>Measures the outside warning band from the actual shape, including angled polygon edges.</summary>
    internal double DistanceToBoundary(Point point)
    {
        if (Kind == ShapeKind.Circle)
            return Math.Abs(Math.Sqrt(SquaredDistance(point, _circleCentre)) - _circleRadius);
        if (Kind == ShapeKind.Square)
        {
            var dx = Math.Max(0, Math.Abs(point.X - _squareCentre.X) - _squareHalfSize);
            var dy = Math.Max(0, Math.Abs(point.Y - _squareCentre.Y) - _squareHalfSize);
            return Math.Sqrt(dx * dx + dy * dy);
        }
        var minimum = double.MaxValue;
        for (var i = 0; i < _corners.Count; i++)
            minimum = Math.Min(minimum, SegmentDistanceSquared(point, _corners[i], _corners[(i + 1) % _corners.Count]));
        return Math.Sqrt(minimum);
    }

    /// <summary>Creates bottom-up RGB24 pixels; the preview and PNG share the same map coordinates.</summary>
    internal byte[] CreateRgb()
    {
        if (!Enum.IsDefined(typeof(ShapeKind), Kind)) throw new InvalidOperationException("Select a supported area shape.");
        var pixels = new byte[Resolution * Resolution * 3];
        for (var y = 0; y < Resolution; y++)
        {
            for (var x = 0; x < Resolution; x++)
            {
                var point = new Point((x + .5) / Resolution, 1 - (y + .5) / Resolution);
                var channel = Contains(point) ? 1 : DistanceToBoundary(point) <= (double)WarningPixels / Resolution ? 2 : 0;
                pixels[(y * Resolution + x) * 3 + channel] = 255;
            }
        }
        return pixels;
    }

    /// <summary>Rejects intersecting non-neighbour edges, tiny edges and polygons without usable area.</summary>
    private bool IsValidPolygon()
    {
        var twiceArea = 0.0;
        for (var i = 0; i < _corners.Count; i++)
        {
            var a = _corners[i];
            var b = _corners[(i + 1) % _corners.Count];
            if (SquaredDistance(a, b) < MinimumSize * MinimumSize) return false;
            var previous = _corners[(i + _corners.Count - 1) % _corners.Count];
            if (Math.Abs(Cross(previous, a, b)) < 1e-12 && (previous.X - a.X) * (b.X - a.X) + (previous.Y - a.Y) * (b.Y - a.Y) > 0)
                return false;
            twiceArea += a.X * b.Y - b.X * a.Y;
            for (var j = i + 2; j < _corners.Count; j++)
            {
                if (i == 0 && j == _corners.Count - 1) continue;
                if (SegmentsIntersect(a, b, _corners[j], _corners[(j + 1) % _corners.Count])) return false;
            }
        }
        return Math.Abs(twiceArea) >= 2 * MinimumSize * MinimumSize;
    }

    /// <summary>Detects both crossed segments and non-neighbour edges that touch or overlap.</summary>
    private static bool SegmentsIntersect(Point a, Point b, Point c, Point d)
    {
        if (Math.Max(a.X, b.X) < Math.Min(c.X, d.X) || Math.Max(c.X, d.X) < Math.Min(a.X, b.X) ||
            Math.Max(a.Y, b.Y) < Math.Min(c.Y, d.Y) || Math.Max(c.Y, d.Y) < Math.Min(a.Y, b.Y)) return false;
        return Cross(a, b, c) * Cross(a, b, d) <= 0 && Cross(c, d, a) * Cross(c, d, b) <= 0;
    }

    /// <summary>Returns a segment projection distance without allocating temporary geometry.</summary>
    private static double SegmentDistanceSquared(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var length = dx * dx + dy * dy;
        var t = length == 0 ? 0 : Math.Max(0, Math.Min(1, ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / length));
        return SquaredDistance(p, new Point(a.X + dx * t, a.Y + dy * t));
    }

    /// <summary>Measures the turn direction of three points for segment intersection tests.</summary>
    private static double Cross(Point a, Point b, Point c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
    /// <summary>Measures squared point distance for hit testing and circle membership.</summary>
    private static double SquaredDistance(Point a, Point b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
    /// <summary>Restricts corner input to finite map coordinates with warning-band clearance.</summary>
    private static Point Bounded(Point point)
    {
        RequireFinite(point.X);
        RequireFinite(point.Y);
        return new Point(Math.Max(Margin, Math.Min(1 - Margin, point.X)), Math.Max(Margin, Math.Min(1 - Margin, point.Y)));
    }
    /// <summary>Stops non-finite input from corrupting geometry or freezing rasterisation.</summary>
    private static void RequireFinite(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value), "Enter a finite position or size.");
    }
}
