using System.Collections.Generic;
using Elements.Geometry;
using System;
using Elements.Geometry.Solids;
using System.Linq;
using Newtonsoft.Json;
using LayoutFunctionCommon;
using SpacePlanning;

namespace Elements
{
    public partial class SpaceBoundary
    {

        public List<Line> AdjacentCorridorEdges { get; set; } = null;

        public Line AlignmentEdge { get; set; } = null;
        public double AvailableLength { get; set; } = 0;
        public Transform ToAlignmentEdge = null;
        public Transform FromAlignmentEdge = null;

        public Vector3? IndividualCentroid { get; set; } = null;
        public Vector3? ParentCentroid { get; set; } = null;

        public bool AutoPlaced { get; set; } = false;

        public int CountPlaced { get; set; } = 0;

        public int SpaceCount { get; set; } = 1;

        // Identity properties

        [JsonProperty("Level Add Id")]
        public string LevelAddId { get; set; }

        [JsonProperty("Relative Position")]
        public Vector3 RelativePosition { get; set; }

        [JsonProperty("Original Boundary")]
        public Polygon OriginalBoundary { get; set; }

        [JsonProperty("Original Voids")]
        public List<Polygon> OriginalVoids { get; set; }

        // end identity properties

        [JsonIgnore]
        public LevelVolume LevelVolume { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public LevelElements LevelElements { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        public ProgramRequirement FulfilledProgramRequirement = null;
        public static void SetRequirements(IEnumerable<ProgramRequirement> reqs)
        {
            Requirements = new Dictionary<string, ProgramRequirement>();
            foreach (var req in reqs)
            {
                if (Requirements.ContainsKey(req.QualifiedProgramName))
                {
                    Requirements[req.QualifiedProgramName].TotalArea += req.TotalArea;
                    Requirements[req.QualifiedProgramName].SpaceCount += req.SpaceCount;
                }
                else
                {
                    Requirements.Add(req.QualifiedProgramName, req);
                }
            }
            foreach (var kvp in Requirements)
            {
                var color = kvp.Value.Color ?? Colors.Aqua; // this shouldn't ever actually be null, but just in case...
                color.Alpha = 0.5;
                MaterialDict[kvp.Key] = new Material(kvp.Value.ProgramName, color, doubleSided: false);
            }
        }

        /// <summary>
        /// Static properties can persist across executions! need to reset to defaults w/ every execution.
        /// </summary>
        public static void Reset()
        {
            random = new Random(11);
            Requirements.Clear();
            MaterialDict = new Dictionary<string, Material>(materialDefaults);
        }

        public bool Match(SpacesIdentity identity)
        {
            var lcs = this.LevelVolume?.LocalCoordinateSystem ?? new Transform();
            var levelMatch = identity.LevelAddId == this.LevelAddId;
            var boundaryMatch = true;
            if (identity.OriginalBoundary != null && this.OriginalBoundary != null)
            {
                boundaryMatch = identity.OriginalBoundary.IsAlmostEqualTo(this.OriginalBoundary, 1.0);
            }
            var returnVal = boundaryMatch && levelMatch && this.Boundary.Contains(lcs.OfPoint(identity.RelativePosition));
            return returnVal;
        }

        public static bool TryGetRequirementsMatch(string nameToFind, out ProgramRequirement fullRequirement)
        {
            if (Requirements.TryGetValue(nameToFind ?? "unspecified", out fullRequirement))
            {
                return true;
            }
            else
            {
                var keyMatch = Requirements.Keys.FirstOrDefault(k => k.EndsWith($" - {nameToFind}"));
                if (keyMatch != null)
                {
                    fullRequirement = Requirements[keyMatch];
                    return true;
                }
            }
            return false;
        }
        public static Dictionary<string, ProgramRequirement> Requirements { get; private set; } = new Dictionary<string, ProgramRequirement>();

        private static Dictionary<string, Material> materialDefaults = new Dictionary<string, Material> {
            {"unspecified", new Material("Unspecified Space Type", new Color(0.8, 0.8, 0.8, 0.3), doubleSided: false)},
            {"Unassigned Space Type", new Material("Unspecified Space Type", new Color(0.8, 0.8, 0.8, 0.3), doubleSided: false)},
            {"unrecognized", new Material("Unspecified Space Type", new Color(0.8, 0.8, 0.2, 0.3), doubleSided: false)},
            {"Circulation", new Material("Circulation", new Color(0.996,0.965,0.863,0.5), doubleSided: false)}, //✅
            {"Open Office", new Material("Open Office", new Color(0.435,0.627,0.745,0.5), doubleSided: false)}, //✅  https://hypar-content-catalogs.s3-us-west-2.amazonaws.com/35cb4053-4d39-47ef-9673-2dccdae1433b/SteelcaseOpenOffice-35cb4053-4d39-47ef-9673-2dccdae1433b.json
            {"Private Office", new Material("Private Office", new Color(0.122,0.271,0.361,0.5), doubleSided: false)}, //✅ https://hypar-content-catalogs.s3-us-west-2.amazonaws.com/69be76de-aaa1-4097-be0c-a97eb44d62e6/Private+Office-69be76de-aaa1-4097-be0c-a97eb44d62e6.json
            {"Lounge", new Material("Lounge", new Color(1.000,0.584,0.196,0.5), doubleSided: false)}, //✅ https://hypar-content-catalogs.s3-us-west-2.amazonaws.com/52df2dc8-3107-43c9-8a9f-e4b745baca1c/Steelcase-Lounge-52df2dc8-3107-43c9-8a9f-e4b745baca1c.json
            {"Classroom", new Material("Classroom", new Color(0.796,0.914,0.796,0.5), doubleSided: false)}, //✅ https://hypar-content-catalogs.s3-us-west-2.amazonaws.com/b23810e9-f565-4845-9b08-d6beb6223bea/Classroom-b23810e9-f565-4845-9b08-d6beb6223bea.json
            {"Pantry", new Material("Pantry", new Color(0.5,0.714,0.745,0.5), doubleSided: false)}, //✅ https://hypar-content-catalogs.s3-us-west-2.amazonaws.com/599d1640-2584-42f7-8de1-e988267c360a/Pantry-599d1640-2584-42f7-8de1-e988267c360a.json
            {"Meeting Room", new Material("Meeting Room", new Color(0.380,0.816,0.608,0.5), doubleSided: false)}, //✅ https://hypar-content-catalogs.s3-us-west-2.amazonaws.com/251d637c-c570-43bd-ab33-f59f337506bb/Catalog-251d637c-c570-43bd-ab33-f59f337506bb.json
            {"Phone Booth", new Material("Phone Booth", new Color(0.976,0.788,0.129,0.5), doubleSided: false)},  //✅ https://hypar-content-catalogs.s3-us-west-2.amazonaws.com/deacf056-2d7e-4396-8bdf-f30d581f2747/Phone+Booths-deacf056-2d7e-4396-8bdf-f30d581f2747.json
            {"Support", new Material("Support", new Color(0.447,0.498,0.573,0.5), doubleSided: false)},
            {"Reception", new Material("Reception", new Color(0.576,0.463,0.753,0.5), doubleSided: false)}, //✅ https://hypar-content-catalogs.s3-us-west-2.amazonaws.com/8762e4ec-7ddd-49b1-bcca-3f303f69f453/Reception-8762e4ec-7ddd-49b1-bcca-3f303f69f453.json
            {"Open Collaboration", new Material("Open Collaboration", new Color(209.0/255, 224.0/255, 178.0/255, 0.5), doubleSided: false)},
            {"Data Hall", new Material("Data Hall", new Color(0.46,0.46,0.48,0.5), doubleSided: false)},
            {"Parking", new Material("Parking", new Color(0.447,0.498,0.573,0.5), doubleSided: false)}
        };

        internal void ComputeRelativePosition()
        {
            var lcs = this.LevelVolume?.LocalCoordinateSystem ?? new Transform();
            var internalPoint = this.Boundary.Perimeter.PointInternal();
            // TODO: add this to Elements as Profile.PointInternal().
            if (this.Boundary.Voids.Any())
            {
                var perimeterPoints = this.Boundary.Perimeter.Vertices.ToList();
                var voidPoints = this.Boundary.Voids.SelectMany(v => v.Vertices).ToList();
                var currVoidPoint = 0;
                var currPerimeterPoint = 0;
                while (true)
                {
                    var ptA = perimeterPoints[currPerimeterPoint];
                    var ptB = voidPoints[currVoidPoint];
                    var midPt = (ptA + ptB) / 2;
                    if (this.Boundary.Contains(midPt) && !this.Boundary.Voids.Any(v => v.Contains(midPt)))
                    {
                        internalPoint = midPt;
                        break;
                    }
                    currVoidPoint++;
                    if (currVoidPoint >= voidPoints.Count)
                    {
                        currVoidPoint = 0;
                        currPerimeterPoint++;
                    }
                    if (currPerimeterPoint >= perimeterPoints.Count)
                    {
                        // we give up. There was no diagonal between any void
                        // vertex and any perimeter vertex that was inside the
                        // boundary. This shouldn't be possible, I think?
                        break;
                    }
                }
            }
            this.RelativePosition = lcs.Inverted().OfPoint(internalPoint);
        }

        public static Dictionary<string, Material> MaterialDict { get; private set; } = new Dictionary<string, Material>(materialDefaults);

        public string ProgramName
        {
            get
            {
                return ProgramType;
            }
            set
            {
                ProgramType = value;
            }
        }
        private static Random random = new Random(11);

        [JsonProperty("Room View")]
        public ViewScope RoomView { get; set; }

        public override void UpdateRepresentations()
        {
            var innerProfile = Boundary;
            // offset the inner profile ever so slightly so that we don't get z
            // fighting. This used to be a bad thing because snaps were
            // generated from representation, but now we don't even serialize
            // it, so it's safe.
            try
            {
                innerProfile = innerProfile.ThickenedInteriorProfile();
                var offset = innerProfile.Offset(-0.001);
                innerProfile = offset.FirstOrDefault();
            }
            catch
            {
                // just swallow an offset failure.
            }
            var extrude = new Extrude(innerProfile.Transformed(new Transform(0, 0, 0.001)), Height, Vector3.ZAxis)
            {
                ReverseWinding = true
            };
            var repInstance = new RepresentationInstance(new SolidRepresentation(extrude), this.Material);
            var linesInstance = new RepresentationInstance(new CurveRepresentation(innerProfile.Perimeter, false), BuiltInMaterials.Black);
            this.RepresentationInstances = new List<RepresentationInstance> { repInstance, linesInstance };
            var bbox = new BBox3(this);
            bbox = new BBox3(bbox.Min, bbox.Max - (0, 0, 0.1));
            RoomView = new ViewScope()
            {
                BoundingBox = bbox,
                Camera = new Camera((0, 0, -1), null, null),
                ClipWithBoundingBox = true
            };
        }
        public static SpaceBoundary Make(Profile profile, string fullyQualifiedName, Transform xform, double height, Vector3? parentCentroid = null, Vector3? individualCentroid = null, IEnumerable<Line> corridorSegments = null)
        {
            if (profile.Perimeter.IsClockWise())
            {
                profile = profile.Reversed();
            }
            MaterialDict.TryGetValue(fullyQualifiedName ?? "unspecified", out var material);
            var hasReqMatch = TryGetRequirementsMatch(fullyQualifiedName, out var fullReq);
            var name = hasReqMatch ? fullReq.HyparSpaceType : fullyQualifiedName;
            if (name == "unspecified")
            {
                name = "Unassigned Space Type";
            }
            if (profile.GetEdgeThickness() == null)
            {
                if (fullReq != null && fullReq.Enclosed == true)
                {
                    profile.SetEdgeThickness(Units.InchesToMeters(3), Units.InchesToMeters(3));
                }
            }
            var sb = new SpaceBoundary
            {
                Boundary = profile,
                Cells = new List<Polygon> { profile.Perimeter },
                Area = profile.Area(),
                Height = height,
                Transform = xform,
                Material = material ?? MaterialDict["unrecognized"],
                Name = name,
                OriginalBoundary = profile.Perimeter,
                OriginalVoids = profile.Voids.ToList()
            };
            profile.Name = name;
            sb.HyparSpaceType = name;
            profile.AdditionalProperties["Color"] = sb.Material.Color;
            if (hasReqMatch)
            {
                fullReq.CountPlaced++;
                sb.FulfilledProgramRequirement = fullReq;
                sb.ProgramGroup = fullReq.ProgramGroup;
            }
            sb.ProgramName = fullyQualifiedName;
            sb.ParentCentroid = parentCentroid ?? xform.OfPoint(profile.Perimeter.Centroid());
            sb.IndividualCentroid = individualCentroid ?? xform.OfPoint(profile.Perimeter.Centroid());

            if (corridorSegments != null)
            {
                sb.AdjacentCorridorEdges = WallGeneration.FindAllEdgesAdjacentToSegments(profile.RoomEdges(), corridorSegments, out var otherSegments).Select(re => re.Line).ToList();
            }
            return sb;
        }

        public void Remove()
        {
            if (this.LevelElements?.Elements != null && this.LevelElements.Elements.Contains(this))
            {
                this.LevelElements.Elements.Remove(this);
            }
            if (this.FulfilledProgramRequirement != null)
            {
                this.FulfilledProgramRequirement.CountPlaced--;
            }
        }
        public void SetProgram(string displayName)
        {
            if (displayName == null)
            {
                return;
            }
            if (!MaterialDict.TryGetValue(displayName ?? "unrecognized", out var material))
            {
                var color = random.NextColor();
                color.Alpha = 0.5;
                MaterialDict[displayName] = new Material(displayName, color, doubleSided: false);
                material = MaterialDict[displayName];
            }
            this.Boundary.AdditionalProperties["Color"] = material.Color;
            this.Boundary.Name = displayName;
            this.Material = material;
            this.ProgramName = displayName;
            if (this.FulfilledProgramRequirement != null)
            {
                this.FulfilledProgramRequirement.CountPlaced--;
            }
            var hasReqMatch = TryGetRequirementsMatch(displayName, out var fullReq);
            // TODO: make the name use the fully qualified name once all functions use `HyparSpaceType`.
            this.Name = hasReqMatch ? fullReq.HyparSpaceType : displayName;
            this.HyparSpaceType = hasReqMatch ? fullReq.HyparSpaceType : displayName;
            if (hasReqMatch)
            {
                fullReq.CountPlaced++;
                this.FulfilledProgramRequirement = fullReq;
                this.ProgramRequirement = fullReq.Id;
                if(fullReq.Enclosed == true && this.Boundary.GetEdgeThickness() == null)
                {
                    this.Boundary.SetEdgeThickness(Units.InchesToMeters(3), Units.InchesToMeters(3));
                } 
            }
            this.ProgramType = displayName;
        }

        public void SetLevelProperties(LevelVolume volume)
        {
            this.AdditionalProperties["Building Name"] = volume.BuildingName;
            this.AdditionalProperties["Level Name"] = volume.Name;
            this.Level = volume.Id;
        }

        public SpaceBoundary Update(SpacesOverride edit, List<LevelLayout> levelLayouts)
        {
            var matchingLevelLayout =
                levelLayouts.FirstOrDefault(ll => edit.Value?.Level?.AddId != null && ll.LevelVolume.AddId == edit.Value?.Level?.AddId) ??
                levelLayouts.FirstOrDefault(ll => ll.LevelVolume.Name == edit.Value?.Level?.Name) ??
                levelLayouts.FirstOrDefault(ll => ll.Id == LevelLayout);
            matchingLevelLayout.UpdateSpace(this, edit.Value.Boundary, edit.Value.ProgramType);

            return this;
        }

        public static SpaceBoundary Create(SpacesOverrideAddition add, List<LevelLayout> levelLayouts)
        {
            var matchingLevelLayout =
                levelLayouts.FirstOrDefault(ll => add.Value?.Level?.AddId != null && ll.LevelVolume.AddId == add.Value?.Level?.AddId) ??
                levelLayouts.FirstOrDefault(ll => ll.LevelVolume.Name == add.Value.Level?.Name) ??
                // TODO: Remove LevelLayout property when the SampleProject template data is updated and the "Level Layout" property is completely replaced by "Level"
                levelLayouts.FirstOrDefault(ll => add.Value?.LevelLayout?.AddId != null && ll.LevelVolume.AddId + "-layout" == add.Value?.LevelLayout?.AddId) ??
                levelLayouts.FirstOrDefault(ll => ll.LevelVolume.Name + " Layout" == add.Value.LevelLayout?.Name);
            var sb = matchingLevelLayout.CreateSpace(add.Value.Boundary);
            sb?.SetProgram(add.Value.ProgramType);
            return sb;
        }
    }
}