using DotLiquid.Tags;
using Elements;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Microsoft.CodeAnalysis.CSharp;
using Newtonsoft.Json;
using SpacePlanning;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Elements
{
    public partial class LevelLayout : Element
    {

        [JsonIgnore]
        public LevelVolume LevelVolume { get; set; }

        [JsonIgnore]
        public Level Level { get; set; }

        [JsonIgnore]
        public List<SpaceBoundary> SpaceBoundaries { get; set; } = new List<SpaceBoundary>();

        // Old pathways made this the overridable property — user edits profiles, we make spaces from those. New pathway should keep this synced with the SpaceBoundaries list.
        [JsonProperty("Profiles")]
        public List<Profile> Profiles { get; set; }

        [JsonProperty("Add Id")]
        public string AddId { get; set; }

        private void CleanSpaceBoundaryProfiles()
        {
            var profiles = this.SpaceBoundaries.Select(sb => sb.Boundary);
            var cleaned = profiles.Cleaned();

        }

        private static Random random = new Random(11);

        public LevelLayout(LevelVolume levelVolume, List<Level> levels, (
            Dictionary<string, List<CirculationSegment>> circulationSegmentsByLevel,
            Dictionary<string, List<VerticalCirculationElement>> verticalCirculationByLevel,
            Dictionary<string, List<ServiceCore>> coresByLevel,
            Dictionary<string, List<Wall>> wallsByLevel) levelGroupedElements)
        {
            this.Levels = new List<Guid> { levelVolume.Level.Value };
            this.LevelVolume = levelVolume;
            this.Level = levels.SingleOrDefault(x => x.Id == levelVolume.Level);
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
                    try
                    {
                        return Profile.Difference(new[] { p }, subtractedProfiles);
                    }
                    catch
                    {
                        var diff = new List<Profile> { p };
                        foreach (var subtractedProfile in subtractedProfiles)
                        {
                            try
                            {
                                diff = Profile.Difference(diff, new[] { subtractedProfile });
                            }
                            catch
                            {
                                // skip and print
                                Elements.Serialization.JSON.JsonInheritanceConverter.ElementwiseSerialization = true;
                                var json = JsonConvert.SerializeObject(new { profiles = diff, subtractedProfile });
                                Console.WriteLine("⚠️ Unable to subtract profiles");
                                Console.WriteLine(json);
                                // System.IO.File.WriteAllText($"/Users/andrewheumann/Dev/Function-SpacePlanning/profile-error_{++counter}.json", json);
                                Elements.Serialization.JSON.JsonInheritanceConverter.ElementwiseSerialization = false;
                            }
                        }
                        return diff;
                        // var subUnion = Profile.UnionAll(subtractedProfiles);
                        // return Profile.Difference(new[] { p }, subUnion);
                        // return new List<Profile> { p };
                    }
                }).ToList();
            }

            this.Profiles = this.Profiles.Cleaned();
        }

        public LevelLayout(Level level, List<Profile> spaceBoundaryProfiles, (
            Dictionary<string, List<CirculationSegment>> circulationSegmentsByLevel,
            Dictionary<string, List<VerticalCirculationElement>> verticalCirculationByLevel,
            Dictionary<string, List<ServiceCore>> coresByLevel,
            Dictionary<string, List<Wall>> wallsByLevel) levelGroupedElements)
        {
            this.Levels = new List<Guid> { level.Id };
            this.Level = level;
            this.AddId = (level.AddId ?? level.Name) + "-layout";
            this.Profiles = spaceBoundaryProfiles;
            this.Name = level.Name + " Layout";
            var levelVolumeBoundary = new Profile(ConvexHull.FromPoints(this.Profiles.SelectMany(p => p.Perimeter.Vertices).ToList()));
            this.LevelVolume = new LevelVolume(levelVolumeBoundary, level.Height.Value, levelVolumeBoundary.Area(), "Free Building", level.Id, null, level.PlanView);

            var (subtractedProfiles, enclosedRooms) = GetSubtractionProfiles(level, levelGroupedElements);

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
                    try
                    {
                        return Profile.Difference(new[] { p }, subtractedProfiles);
                    }
                    catch
                    {
                        var diff = new List<Profile> { p };
                        foreach (var subtractedProfile in subtractedProfiles)
                        {
                            try
                            {
                                diff = Profile.Difference(diff, new[] { subtractedProfile });
                            }
                            catch
                            {
                                // skip and print
                                Elements.Serialization.JSON.JsonInheritanceConverter.ElementwiseSerialization = true;
                                var json = JsonConvert.SerializeObject(new { profiles = diff, subtractedProfile });
                                Console.WriteLine("⚠️ Unable to subtract profiles");
                                Console.WriteLine(json);
                                // System.IO.File.WriteAllText($"/Users/andrewheumann/Dev/Function-SpacePlanning/profile-error_{++counter}.json", json);
                                Elements.Serialization.JSON.JsonInheritanceConverter.ElementwiseSerialization = false;
                            }
                        }
                        return diff;
                        // var subUnion = Profile.UnionAll(subtractedProfiles);
                        // return Profile.Difference(new[] { p }, subUnion);
                        // return new List<Profile> { p };
                    }
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
                    return Array.Empty<Profile>();
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
                if (levelGroupedElements.wallsByLevel.TryGetValue(levelVolume.Id.ToString(), out var walls) || (levelGroupedElements.wallsByLevel.TryGetValue("ungrouped", out walls)))
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

            ExcludeInnerPolygons(enclosedRooms);
            return (subtractedProfiles, enclosedRooms);
        }

        private (List<Profile> subtractedProfiles, List<Profile> enclosedRooms) GetSubtractionProfiles(Level level, (Dictionary<string, List<CirculationSegment>> circulationSegmentsByLevel, Dictionary<string, List<VerticalCirculationElement>> verticalCirculationByLevel, Dictionary<string, List<ServiceCore>> coresByLevel, Dictionary<string, List<Wall>> wallsByLevel) levelGroupedElements)
        {
            var subtractedProfiles = new List<Profile>();
            if (levelGroupedElements.circulationSegmentsByLevel.TryGetValue(level.Id.ToString(), out var circulationSegments))
            {
                foreach (var circulationSegment in circulationSegments)
                {
                    subtractedProfiles.Add(circulationSegment.Profile);
                }
            }
            if (levelGroupedElements.verticalCirculationByLevel.TryGetValue(level.Id.ToString(), out var verticalCirculationElements))
            {
                //     subtractedProfiles.AddRange(verticalCirculationElements.SelectMany(vce =>
                //    {
                //        return Profile.UnionAll(vce.IntersectWithPlane(levelPlane).Select(p => new Profile(p.IsClockWise() ? p.Reversed() : p)));
                //    }));
                subtractedProfiles.AddRange(verticalCirculationElements.SelectMany(vce =>
                {
                    if (vce.ProfilesAtLevel.TryGetValue(level.Id, out var pgon))
                    {
                        return new[] { new Profile(pgon.TransformedPolygon(vce.Transform)) };
                    }
                    return Array.Empty<Profile>();
                }));
            }
            if (levelGroupedElements.coresByLevel.TryGetValue(level.Id.ToString(), out var cores))
            {
                foreach (var core in cores)
                {
                    subtractedProfiles.Add(core.Profile);
                }
            }
            var enclosedRooms = new List<Profile>();
            try
            {
                if (levelGroupedElements.wallsByLevel.TryGetValue(level.Id.ToString(), out var walls) || (levelGroupedElements.wallsByLevel.TryGetValue("ungrouped", out walls)))
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

            ExcludeInnerPolygons(enclosedRooms);
            return (subtractedProfiles, enclosedRooms);
        }

        private static void ExcludeInnerPolygons(List<Profile> profiles)
        {
            var orderedByAreaProfiles = profiles.OrderByDescending(p => p.Area()).ToList();

            for (int i = 0; i < orderedByAreaProfiles.Count; i++)
            {
                var outerCandidate = orderedByAreaProfiles[i];
                for (int j = i + 1; j < orderedByAreaProfiles.Count; j++)
                {
                    var innerCandidate = orderedByAreaProfiles[j];
                    if (outerCandidate.Perimeter.Covers(innerCandidate.Perimeter))
                    {
                        outerCandidate.Voids.Add(innerCandidate.Perimeter.Reversed());
                    }
                }
            }
        }

        private LevelElements _levelElements = null;

        private LevelElements LevelElements
        {
            get
            {
                _levelElements ??= new LevelElements
                {
                    Name = LevelVolume.Name,
                    Elements = new List<Element>(),
                    Level = LevelVolume.Id
                };
                return _levelElements;
            }
        }

        public IEnumerable<SpaceBoundary> CreateSpacesFromProfiles()
        {
            var allProfiles = new List<Profile>(Profiles);
            Profiles.Clear();
            return allProfiles.Select((p, i) =>
            {
                return this.CreateSpace(p);
            });
        }

        public SpaceBoundary CreateSpace(Profile p)
        {
            var programName = "unspecified";
            if (p.AdditionalProperties.ContainsKey("Legacy Program Assignment"))
            {
                programName = p.AdditionalProperties["Legacy Program Assignment"] as string;
            }
            else if (LevelVolume.PrimaryUseCategory != null)
            {
                programName = LevelVolume.PrimaryUseCategory;
            }
            var projectedProfile = p.Project(Plane.XY);
            Profiles.Add(projectedProfile);
            projectedProfile.AdditionalProperties = p.AdditionalProperties;
            projectedProfile.Name = p.Name;
            var spaceBoundary = SpaceBoundary.Make(projectedProfile, programName, LevelVolume.Transform, LevelVolume.Height);
            spaceBoundary.Boundary.AdditionalProperties["SpaceBoundary"] = spaceBoundary.Id;
            spaceBoundary.SetLevelProperties(LevelVolume);
            spaceBoundary.LevelElements = LevelElements;
            spaceBoundary.LevelAddId = this.Level.Id.ToString();
            spaceBoundary.LevelVolume = LevelVolume;
            spaceBoundary.ComputeRelativePosition();
            spaceBoundary.LevelLayout = this.Id;
            spaceBoundary.Level = this.Level.Id;
            this.SpaceBoundaries.Add(spaceBoundary);
            return spaceBoundary;
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
            this.SpaceBoundaries.AddRange(group);
        }

        public List<ModelLines> CreateModelLines()
        {
            var list = new List<ModelLines>();
            var lines = Profiles.SelectMany(p => p.Segments());

            var modelLines = new ModelLines(lines.ToList(), BuiltInMaterials.Black, LevelVolume.Transform);
            modelLines.SetSelectable(false);
            list.Add(modelLines);

            return list;
        }

        internal void UpdateSpace(SpaceBoundary spaceBoundary, Profile boundary, string programType)
        {
            var boundaryProjected = boundary?.Project(Plane.XY) ?? spaceBoundary.Boundary;
            var profileToReplace = Profiles.FindIndex(p => p.Id == spaceBoundary.Boundary.Id);
            if (profileToReplace != -1)
            {
                Profiles[profileToReplace] = boundaryProjected;
            }
            else
            {

            }
            spaceBoundary.Boundary = boundaryProjected;
            spaceBoundary.SetProgram(programType);
        }

        public void RemoveSpace(SpacesOverrideRemoval removal)
        {
            var match = this.SpaceBoundaries.FirstOrDefault(sb => sb.Match(removal.Identity));
            if (match != null)
            {
                this.SpaceBoundaries.Remove(match);
                this.Profiles.RemoveAll(p => p.Id == match.Boundary.Id);
            }
        }
    }
}