using Elements;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Newtonsoft.Json;
using SpacePlanning;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Elements
{
    public partial class LevelLayout : Element
    {
        private static readonly Plane XY = new Plane((0, 0), (0, 0, 1));

        [JsonIgnore]
        public List<LevelVolume> LevelVolumes { get; set; }
        public List<Profile> Profiles { get; set; }

        [JsonProperty("Add Id")]
        public string AddId { get; set; }

        public LevelLayout Update(LevelLayoutOverride edit, (
            Dictionary<string, List<CirculationSegment>> circulationSegmentsByLevel,
            Dictionary<string, List<VerticalCirculationElement>> verticalCirculationByLevel,
            Dictionary<string, List<ServiceCore>> coresByLevel,
            Dictionary<string, List<Wall>> wallsByLevel) levelGroupedElements)
        {
            var profiles = edit.Value.Profiles?.Select(p =>
            {
                var projected = p.Project(XY);
                projected.AdditionalProperties = p.AdditionalProperties;
                projected.Name = p.Name;
                return projected;
            });
            // assume that all levels for this layout have the same circulation.
            // Circ + core probably need to become distinguishing features of
            // the level layout.
            var (subtractedProfiles, enclosedRooms) = GetSubtractionProfiles(LevelVolumes.First(), levelGroupedElements);
            if (profiles.Count() > 0 && subtractedProfiles.Count > 0)
            {
                // subtract indivudually to avoid merging
                profiles = profiles.SelectMany((p) =>
                {
                    try
                    {
                        return Profile.Difference(new[] { p }, subtractedProfiles);
                    }
                    catch
                    {
                        return new List<Profile> { p };
                    }
                }).ToList();
            }
            Profiles = profiles?.Cleaned() ?? Profiles;
            Identity.AddOverrideIdentity(this, edit);
            return this;
        }

        private static Random random = new Random(11);

        public LevelLayout(LevelVolume levelVolume, (
            Dictionary<string, List<CirculationSegment>> circulationSegmentsByLevel,
            Dictionary<string, List<VerticalCirculationElement>> verticalCirculationByLevel,
            Dictionary<string, List<ServiceCore>> coresByLevel,
            Dictionary<string, List<Wall>> wallsByLevel) levelGroupedElements)
        {
            this.Levels = new List<Guid> { levelVolume.Id };
            this.LevelVolumes = new List<LevelVolume> { levelVolume };
            var levelPlane = new Plane(levelVolume.Transform.Origin + (0, 0, 1), (0, 0, 1));
            this.AddId = (levelVolume.AddId ?? levelVolume.Name) + "-layout";
            this.Profiles = new List<Profile>();
            this.Name = levelVolume.Name + " Layout";

            var levelBoundary = levelVolume.Profile;
            var (subtractedProfiles, enclosedRooms) = GetSubtractionProfiles(levelVolume, levelGroupedElements);
            if (levelBoundary != null)
            {
                this.Profiles.Add(levelBoundary);
            }
            // if there are any rooms, subtract them from the level boundary and add them in as additional profiles
            if (enclosedRooms.Count > 0)
            {
                if (this.Profiles.Count > 0)
                {
                    this.Profiles = Profile.Difference(this.Profiles, enclosedRooms);
                }
                this.Profiles.AddRange(enclosedRooms);
            }
            if (this.Profiles.Count > 0 && subtractedProfiles.Count > 0)
            {
                // subtract indivudually to avoid merging
                this.Profiles = this.Profiles.SelectMany((p) =>
                {
                    return Profile.Difference(new[] { p }, subtractedProfiles);
                }).ToList();
            }

            this.Profiles = this.Profiles.Cleaned();
        }

        private (List<Profile> subtractedProfiles, List<Profile> enclosedRooms) GetSubtractionProfiles(LevelVolume levelVolume, (Dictionary<string, List<CirculationSegment>> circulationSegmentsByLevel, Dictionary<string, List<VerticalCirculationElement>> verticalCirculationByLevel, Dictionary<string, List<ServiceCore>> coresByLevel, Dictionary<string, List<Wall>> wallsByLevel) levelGroupedElements)
        {
            var subtractedProfiles = new List<Profile>();
            if (levelGroupedElements.circulationSegmentsByLevel.TryGetValue(levelVolume.Id.ToString(), out var circulationSegments))
            {
                foreach (var circulationSegment in circulationSegments)
                {
                    subtractedProfiles.Add(circulationSegment.Profile);
                }
            }
            if (levelGroupedElements.verticalCirculationByLevel.TryGetValue(levelVolume.Id.ToString(), out var verticalCirculationElements))
            {
                //     subtractedProfiles.AddRange(verticalCirculationElements.SelectMany(vce =>
                //    {
                //        return Profile.UnionAll(vce.IntersectWithPlane(levelPlane).Select(p => new Profile(p.IsClockWise() ? p.Reversed() : p)));
                //    }));
                subtractedProfiles.AddRange(verticalCirculationElements.SelectMany(vce =>
                {
                    if (vce.ProfilesAtLevel.TryGetValue(levelVolume.Level ?? Guid.Empty, out var pgon))
                    {
                        return new[] { new Profile(pgon.TransformedPolygon(vce.Transform)) };
                    }
                    return new Profile[] { };
                }));
            }
            if (levelGroupedElements.coresByLevel.TryGetValue(levelVolume.Id.ToString(), out var cores))
            {
                foreach (var core in cores)
                {
                    subtractedProfiles.Add(core.Profile);
                }
            }
            var enclosedRooms = new List<Profile>();
            try
            {
                if (levelGroupedElements.wallsByLevel.TryGetValue(levelVolume.Id.ToString(), out var walls) || (LevelVolumes.Count <= 1 && levelGroupedElements.wallsByLevel.TryGetValue("ungrouped", out walls)))
                {
                    var network = Search.Network<Wall>.FromSegmentableItems(walls, (wall) => wall.GetCenterline(), out var allNodeLocations, out var allIntersections);
                    var roomCandidates = network.FindAllClosedRegions(allNodeLocations);

                    foreach (var roomCandidate in roomCandidates)
                    {
                        var roomBoundary = new Polygon(roomCandidate.Select(i => allNodeLocations[i]).ToList());
                        if (roomBoundary.IsClockWise())
                        {
                            continue;
                        }
                        enclosedRooms.Add(new Profile(roomBoundary));
                    }

                }
            }
            catch
            {
                // swallow 
            }
            return (subtractedProfiles, enclosedRooms);
        }

        public bool Match(LevelLayoutIdentity identity)
        {
            return identity.AddId == this.AddId;
        }

        public IEnumerable<SpaceBoundary> CreateSpaces()
        {
            return LevelVolumes.SelectMany((levelVolume) =>
            {
                // this is weird legacy pre-relationships stuff:

                var levelElements = new LevelElements
                {
                    Name = levelVolume.Name,
                    Elements = new List<Element>(),
                    Level = levelVolume.Id
                };


                random = new Random(11);
                return Profiles.Select((p) =>
                {
                    var programName = "unspecified";
                    if (p.AdditionalProperties.ContainsKey("Legacy Program Assignment"))
                    {
                        programName = p.AdditionalProperties["Legacy Program Assignment"] as string;
                    }
                    else if (levelVolume.PrimaryUseCategory != null)
                    {
                        programName = levelVolume.PrimaryUseCategory;
                    }
                    var projectedProfile = p.Project(XY);
                    projectedProfile.AdditionalProperties = p.AdditionalProperties;
                    projectedProfile.Name = p.Name;
                    var spaceBoundary = SpaceBoundary.Make(p.Project(XY), programName, levelVolume.Transform, levelVolume.Height - Units.FeetToMeters(1));
                    spaceBoundary.SetLevelProperties(levelVolume);
                    spaceBoundary.LevelElements = levelElements;
                    spaceBoundary.LevelAddId = levelVolume.AddId ?? levelVolume.Name;
                    spaceBoundary.LevelVolume = levelVolume;
                    spaceBoundary.UnmodifiedProfile = p;
                    spaceBoundary.ComputeRelativePosition();
                    spaceBoundary.LevelLayout = this.Id;
                    return spaceBoundary;
                });
            }).ToList();
        }

        public void AddExistingSpaces(IEnumerable<SpaceBoundary> group)
        {
            var profiles = group.Select(sb =>
            {
                var profile = sb.Boundary;
                profile.AdditionalProperties["Legacy Program Assignment"] = sb.ProgramType;
                profile.AdditionalProperties["Legacy Hypar Program Type"] = sb.Name;
                return profile;
            });
            var cleaned = ProfileUtils.CleanProfiles(profiles);
            this.Profiles = cleaned;
        }

        public List<ModelLines> CreateModelLines()
        {
            var list = new List<ModelLines>();
            var lines = Profiles.SelectMany(p => p.Segments());
            foreach (var level in LevelVolumes)
            {
                var modelLines = new ModelLines(lines.ToList(), BuiltInMaterials.Black, level.Transform);
                modelLines.SetSelectable(false);
                list.Add(modelLines);
            }
            return list;
        }
    }
}