using System.Collections.Generic;
using System.Linq;
using Elements.Geometry;

namespace Elements
{
    public static class ProfileUtils
    {

        public static void PropagateProperties(this IEnumerable<Profile> profiles, Profile source)
        {
            foreach (var p in profiles)
            {
                p.AdditionalProperties = source.AdditionalProperties;
            }
        }
        public static List<Profile> CleanProfiles(IEnumerable<Profile> profiles)
        {
            var cleaned = new List<Profile>();
            // var firstVertex = profiles.First().Perimeter.Vertices.First();
            // var octree = new PointOctree<Vector3>(10, firstVertex, 0.1);
            var points = new List<Vector3>();
            Dictionary<Vector3, int> pointIndexMap = new Dictionary<Vector3, int>();
            var profilesWithBridgesRemoved = new List<Profile>();
            foreach (var p in profiles)
            {
                try
                {
                    var offsetsIn = Profile.Offset(new[] { p }, -0.01);
                    var offsetsOut = Profile.Offset(offsetsIn, 0.01);
                    offsetsOut.PropagateProperties(p);
                    profilesWithBridgesRemoved.AddRange(offsetsOut);
                }
                catch
                {
                    profilesWithBridgesRemoved.Add(p);
                }
            }
            foreach (var profile in profilesWithBridgesRemoved)
            {
                try
                {
                    profile.Perimeter = profile.Perimeter.CollinearPointsRemoved(0.0001);
                }
                catch
                {
                    // leave it alone
                }
                foreach (var vertex in profile.Perimeter.Vertices)
                {
                    var indexOfFirstPointWithinDistance = points.FindIndex(p => p.DistanceTo(vertex) < 0.01);
                    if (indexOfFirstPointWithinDistance == -1)
                    {
                        points.Add(vertex);
                        pointIndexMap[vertex] = points.Count - 1;
                    }
                    else
                    {
                        pointIndexMap[vertex] = indexOfFirstPointWithinDistance;
                    }
                }
            }
            foreach (var profile in profilesWithBridgesRemoved)
            {
                var perimeter = profile.Perimeter;
                Polygon cinchPolygonToVertices(Polygon polygon)
                {
                    var pointIndexList = new List<int>();
                    foreach (var segment in polygon.Segments())
                    {
                        var pointsAlongSegment = points.Where(p => p.DistanceTo(segment) < 0.01).OrderBy(p => p.Dot(segment.Direction())).ToList();
                        foreach (var pt in pointsAlongSegment)
                        {
                            var index = pointIndexMap[pt];
                            if (!pointIndexList.Contains(index))
                            {
                                pointIndexList.Add(index);
                            }
                        }
                    }
                    return new Polygon(pointIndexList.Select(i => points[i]).ToList());
                }
                try
                {
                    var newPerimeter = cinchPolygonToVertices(profile.Perimeter);
                    var newVoids = profile.Voids.Select(cinchPolygonToVertices).ToList();
                    cleaned.Add(new Profile(newPerimeter, newVoids) { AdditionalProperties = profile.AdditionalProperties });
                }
                catch
                {
                    // swallow bad profiles
                }
            }

            return cleaned;
        }
    }
}