using System;
using System.Collections.Generic;
using System.Linq;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Newtonsoft.Json;

namespace Elements
{
    public class VerticalCirculationElement : GeometricElement
    {
        [JsonProperty("Profiles At Level")]
        public Dictionary<Guid, Polygon> ProfilesAtLevel = new Dictionary<Guid, Polygon>();
        public double Width { get; set; }
        public double Length { get; set; }

        [JsonProperty("Shaft Boundary")]
        public Polygon ShaftBoundary { get; set; }

        [JsonProperty("Served Levels")]
        public List<Guid> Levels { get; set; }


        [JsonProperty("Path Of Travel")]
        public Polyline PathOfTravel { get; set; }

        [JsonProperty("Shaft Id")]
        public string ShaftId { get; set; }

        [JsonProperty("Entry Location")]
        public Vector3 EntryLocation { get; set; }

        private List<Solid> _solids;

        public List<Solid> Solids
        {
            get
            {
                if (_solids == null)
                {
                    _solids = Representation.SolidOperations.Where(s => !s.IsVoid).Select(s => s.Solid).ToList();
                }
                return _solids;
            }
        }

        public IEnumerable<Polygon> IntersectWithPlane(Plane p)
        {
            var xy = new Plane((0, 0, 0), (0, 0, 1));
            var inverse = Transform.Inverted();
            var planeInverted = new Plane(inverse.OfPoint(p.Origin), inverse.OfVector(p.Normal));
            return Solids.SelectMany(s =>
            {
                if (s.Intersects(planeInverted, out var polygons))
                {
                    return polygons.Select(p => p.TransformedPolygon(Transform).Project(xy));
                }
                else
                {
                    return new Polygon[] { };
                }
            });
        }

    }
}