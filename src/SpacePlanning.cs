using Elements;
using Elements.Geometry;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpacePlanning
{
    public static class SpacePlanning
    {
        /// <summary>
        /// The SpacePlanning function.
        /// </summary>
        /// <param name="model">The input model.</param>
        /// <param name="input">The arguments to the execution.</param>
        /// <returns>A SpacePlanningOutputs instance containing computed results and the model with any new elements.</returns>
        public static SpacePlanningOutputs Execute(Dictionary<string, Model> inputModels, SpacePlanningInputs input)
        {
            var output = new SpacePlanningOutputs();
            MessageManager.Initialize(output);
            inputModels.TryGetValue("Floors", out var floorsModel);
            inputModels.TryGetValue("Conceptual Mass", out var conceptualMassModel);
            var levelVolumes = conceptualMassModel?.AllElementsOfType<LevelVolume>().ToList() ?? new List<LevelVolume>();
            inputModels.TryGetValue("Levels", out var levelsModel);
            levelVolumes.AddRange(levelsModel?.AllElementsOfType<LevelVolume>().ToList() ?? new List<LevelVolume>());
            if (levelVolumes.Count == 0)
            {
                var levels = levelsModel?.AllElementsOfType<Level>();
                if (levels != null && levels.Any())
                {
                    // TODO: handle separate level groups
                    var levelsOrdered = levels.OrderBy(l => l.Elevation);
                    for (int i = 0; i < levelsOrdered.Count() - 1; i++)
                    {
                        var currLevel = levelsOrdered.ElementAt(i);
                        var nextLevel = levelsOrdered.ElementAt(i + 1);
                        var levelVolume = new LevelVolume
                        {
                            Level = currLevel.Id,
                            Height = nextLevel.Elevation - currLevel.Elevation,
                            Transform = new Transform(0, 0, currLevel.Elevation),
                            Name = currLevel.Name ?? $"Level {i + 1}",
                            AddId = currLevel.Name ?? $"Level {i + 1}"
                        };
                        levelVolumes.Add(levelVolume);
                    }
                }
            }
            foreach (var lv in levelVolumes)
            {
                if (lv.Mass.HasValue && conceptualMassModel?.Elements.TryGetValue(lv.Mass.Value, out var massElem) == true && massElem is ConceptualMass mass)
                {
                    lv.LocalCoordinateSystem = mass.LocalCoordinateSystem;
                }
                else if (lv.Envelope.HasValue && conceptualMassModel?.Elements.TryGetValue(lv.Envelope.Value, out var envElem) == true && envElem is ConceptualMass env)
                {
                    lv.LocalCoordinateSystem = env.LocalCoordinateSystem;
                }
                if (floorsModel == null)
                {
                    // force a bump down so that spaces don't penetrate the next level's scope box
                    lv.Height -= Units.FeetToMeters(1);
                }
                else
                {
                    // if we have floors, we assume we have `levels from floors` which already handles subtracting the thickness. ugh, TODO: clean up everything in the world.
                }
            }


            var levelGroupedElements = MapElementsToLevels(inputModels, levelVolumes);

            var defaultLevelHeight = Units.FeetToMeters(14);
            if (levelGroupedElements.wallsByLevel.TryGetValue("ungrouped", out var ungroupedWalls) && ungroupedWalls.Count > 0)
            {
                defaultLevelHeight = ungroupedWalls.Max(w => w.GetHeight());
            }

            // Get program requirements
            var hasProgramRequirements = inputModels.TryGetValue("Program Requirements", out var programReqsModel);
            var programReqs = programReqsModel?.AllElementsOfType<ProgramRequirement>();

            // Reset static properties on SpaceBoundary
            SpaceBoundary.Reset();

            // Populate SpaceBoundary's program requirement dictionary with loaded requirements
            if (programReqs != null && programReqs.Any())
            {
                SpaceBoundary.SetRequirements(programReqs);
            }

            var levelLayouts = new List<LevelLayout>();
            foreach (var levelVolume in levelVolumes)
            {
                if (levelVolume.PrimaryUseCategory == "Residential")
                {
                    continue;
                }
                var proxy = levelVolume.Proxy("Unit Layout");
                var levelLayout = new LevelLayout(levelVolume, levelGroupedElements);
                proxy.AdditionalProperties["Unit Layout"] = levelLayout.Id;
                levelLayouts.Add(levelLayout);
                output.Model.AddElement(proxy);
            }

            if (levelVolumes.Count == 0)
            {
                // If there was no conceptual mass dependency, we might have no levels.
                // Create a new Level Layout.
                var dummyLevelVolume = new LevelVolume()
                {
                    Height = defaultLevelHeight,
                    AddId = "dummy-level-volume"
                };
                var levelLayout = new LevelLayout(dummyLevelVolume, levelGroupedElements);
                levelLayouts.Add(levelLayout);
            }

            if (inputModels.TryGetValue("Space Planning Zone Hints", out var hintModel))
            {
                var hintSpaces = hintModel.AllElementsOfType<SpaceBoundary>();
                var hintSpacesByLevel = hintSpaces.GroupBy(s => s.Level);
                foreach (var group in hintSpacesByLevel)
                {
                    var levelVolumeForGroup = levelVolumes.FirstOrDefault(lv => lv.Id == group.Key);
                    // Legacy requirement of a level elements for layout functions to work
                    var le = new LevelElements
                    {
                        Name = levelVolumeForGroup?.Name,
                        Elements = new List<Element>(),
                        Level = levelVolumeForGroup?.Id ?? System.Guid.Empty
                    };
                    foreach (var sb in group)
                    {
                        // TODO: get rid of this when all layout functions
                        // support HyparSpaceType as the governing value over Name.
                        sb.Name = sb.HyparSpaceType;
                        if (le.Level != System.Guid.Empty)
                        {
                            sb.LevelElements = le;
                        }
                    }
                    var levelLayout = levelLayouts.FirstOrDefault(l => l.LevelVolume.Id == group.Key);
                    levelLayout?.AddExistingSpaces(group.ToList());
                }
            }


            var spaces = levelLayouts.SelectMany(lul => lul.CreateSpacesFromProfiles()).ToList();
            var levelElements = spaces.Select(s => s.LevelElements).Distinct().ToList();
            RemoveUnmatchedOverrides(input.Overrides.Spaces, input.Overrides.Additions.Spaces, levelLayouts);
            spaces = input.Overrides.Spaces.CreateElements(
                input.Overrides.Additions.Spaces,
                input.Overrides.Removals.Spaces,
                (add) => SpaceBoundary.Create(add, levelLayouts),
                (sb, identity) => sb.Match(identity),
                (sb, edit) => sb.Update(edit, levelLayouts),
                spaces);
            RemoveAutoGeneratedSpacesContainedByRemovals(spaces, input.Overrides.Removals.Spaces);

            foreach (var removal in input.Overrides.Removals.Spaces)
            {
                foreach (var lul in levelLayouts)
                {
                    lul.RemoveSpace(removal);
                }
            }

            // TODO: clean profiles

            foreach (var space in spaces)
            {
                var spaceLevelElements = space.LevelElements;
                spaceLevelElements.Elements.Add(space);
                // weird legacy stuff, TODO remove when layout functions don't depend on it
                space.LevelElements = null;
                space.LevelVolume = null;
                output.Model.AddElement(space);
            }
            output.Model.AddElements(levelLayouts);
            output.Model.AddElements(levelLayouts.SelectMany(lul => lul.CreateModelLines()));
            output.Model.AddElements(levelElements);
            output.Model.AddElements(spaces);

            return output;
        }

        private static void RemoveAutoGeneratedSpacesContainedByRemovals(List<SpaceBoundary> spaces, IList<SpacesOverrideRemoval> removals)
        {
            var removedAreasByLevel = removals.GroupBy(r => r.Identity.LevelAddId).ToDictionary(grp => grp.Key, grp => grp.Select(r => new Profile(r.Identity.OriginalBoundary, r.Identity.OriginalVoids)).ToList());

            var allUnmodifiedSpaces = spaces.Where(s => !s.AdditionalProperties.ContainsKey("associatedIdentities")).ToList();
            for (int i = 0; i < allUnmodifiedSpaces.Count; i++)
            {
                var space = allUnmodifiedSpaces[i];
                var spaceLevel = space.LevelAddId;
                if (removedAreasByLevel.TryGetValue(spaceLevel, out var removedAreas))
                {
                    var nonNullRemovedAreas = removedAreas.Where(a => a != null && a.Perimeter != null);
                    if (!nonNullRemovedAreas.Any())
                    {
                        continue;
                    }
                    var difference = Profile.Difference(new[] { space.Boundary }, nonNullRemovedAreas);
                    if (difference.Count == 0)
                    {
                        spaces.Remove(space);
                    }
                    else
                    {
                        var largestSpace = difference.OrderByDescending(p => Math.Abs(p.Area())).First();
                        space.Boundary = largestSpace;
                    }
                }
            }
        }

        private static void RemoveUnmatchedOverrides(IList<SpacesOverride> edits, IList<SpacesOverrideAddition> additions, List<LevelLayout> levelLayouts)
        {
            // If we created a single dummy level layout, just assume all spaces belong to that and don't bother filtering.
            if (levelLayouts.Any(l => l.Name.Contains("dummy")))
            {
                return;
            }
            foreach (var edit in new List<SpacesOverride>(edits))
            {
                var matchingLevelLayout =
                    levelLayouts.FirstOrDefault(ll => ll.AddId == edit.Identity.LevelAddId + "-layout") ?? // IDK where this is coming from
                    levelLayouts.FirstOrDefault(ll => edit.Value?.Level?.AddId != null && ll.LevelVolume.AddId == edit.Value?.Level?.AddId) ??
                    levelLayouts.FirstOrDefault(ll => ll.LevelVolume.Name == edit.Value?.Level?.Name);
                if (matchingLevelLayout == null)
                {
                    edits.Remove(edit);
                }
            }

            foreach (var addition in new List<SpacesOverrideAddition>(additions))
            {
                var matchingLevelLayout =
                    levelLayouts.FirstOrDefault(ll => addition.Value?.Level?.AddId != null && ll.LevelVolume.AddId == addition.Value?.Level?.AddId) ??
                    levelLayouts.FirstOrDefault(ll => ll.LevelVolume.Name == addition.Value?.Level?.Name) ??
                    // TODO: Remove LevelLayout property when the SampleProject template data is updated and the "Level Layout" property is completely replaced by "Level"
                    levelLayouts.FirstOrDefault(ll => addition.Value?.LevelLayout?.AddId != null && ll.LevelVolume.AddId + "-layout" == addition.Value?.LevelLayout?.AddId) ??
                    levelLayouts.FirstOrDefault(ll => ll.LevelVolume.Name + " Layout" == addition.Value?.LevelLayout?.Name);
                if (matchingLevelLayout == null)
                {
                    var levelName = addition.Value.Level?.Name ?? "Unknown Level";
                    MessageManager.AddWarning($"Some spaces assigned to {levelName} were not created because the level was not found.");
                    additions.Remove(addition);
                }
            }
        }

        private static (
          Dictionary<string, List<CirculationSegment>> circulationSegmentsByLevel,
          Dictionary<string, List<VerticalCirculationElement>> verticalCirculationByLevel,
          Dictionary<string, List<ServiceCore>> coresByLevel,
          Dictionary<string, List<Wall>> wallsByLevel
          ) MapElementsToLevels(Dictionary<string, Model> inputModels, IEnumerable<LevelVolume> levelVolumes)
        {
            var circulationSegmentsByLevel = new Dictionary<string, List<CirculationSegment>>();
            var verticalCirculationByLevel = new Dictionary<string, List<VerticalCirculationElement>>();
            var coresByLevel = new Dictionary<string, List<ServiceCore>>();
            var wallsByLevel = new Dictionary<string, List<Wall>>();

            if (inputModels.TryGetValue("Circulation", out var circModel))
            {
                var circSegments = circModel.AllElementsOfType<CirculationSegment>();
                foreach (var circSegment in circSegments)
                {
                    if (!circulationSegmentsByLevel.TryGetValue(circSegment.Level.ToString(), out var segments))
                    {
                        segments = new List<CirculationSegment>();
                        circulationSegmentsByLevel.Add(circSegment.Level.ToString(), segments);
                    }
                    segments.Add(circSegment);

                }
            }

            if (inputModels.TryGetValue("Vertical Circulation", out var verticalCirculationModel))
            {
                var verticalCirculationElements = verticalCirculationModel.AllElementsOfType<VerticalCirculationElement>();

                foreach (var lvl in levelVolumes)
                {
                    verticalCirculationByLevel.Add(lvl.Id.ToString(), new List<VerticalCirculationElement>());
                    foreach (var vce in verticalCirculationElements)
                    {
                        if (lvl.Profile.Contains(vce.Transform.Origin))
                        {
                            verticalCirculationByLevel[lvl.Id.ToString()].Add(vce);
                        }
                    }
                }
            }

            if (inputModels.TryGetValue("Core", out var coresModel))
            {
                var coreElements = coresModel.AllElementsOfType<ServiceCore>();

                foreach (var lvl in levelVolumes)
                {
                    coresByLevel.Add(lvl.Id.ToString(), new List<ServiceCore>());
                    foreach (var core in coreElements)
                    {
                        if (core.Profile.Perimeter.Vertices.Any(v => lvl.Profile.Contains(v)))
                        {
                            coresByLevel[lvl.Id.ToString()].Add(core);
                        }
                    }
                }

            }

            if (inputModels.TryGetValue("Walls", out var wallsModel))
            {
                var walls = wallsModel.AllElementsAssignableFromType<Wall>();

                foreach (var levelVolume in levelVolumes)
                {
                    wallsByLevel.Add(levelVolume.Id.ToString(), new List<Wall>());
                }
                wallsByLevel.Add("ungrouped", new List<Wall>());

                foreach (var wall in walls)
                {
                    // figure out which levels the wall belongs to. Sometimes
                    // walls will have levels attached, otherwise we might have
                    // to infer by geometry.
                    WallLevelInfo levelInfo = null;
                    if (wall.AdditionalProperties.TryGetValue("Levels", out var levels))
                    {
                        levelInfo = WallLevelInfo.FromJObject(levels as JObject);
                    }
                    var bottomLevel = levelVolumes.FirstOrDefault(lv => lv.Level == levelInfo?.BottomLevel.Id);
                    var topLevel = levelVolumes.FirstOrDefault(lv => lv.Level == levelInfo?.TopLevel.Id);
                    var bottomElevation = bottomLevel?.Transform.Origin.Z ?? 0;
                    var topElevation = topLevel?.Transform.Origin.Z ?? bottomElevation + wall.GetHeight();
                    if (topElevation == bottomElevation)
                    {
                        // in a levels from floors scenario w/ a single level, we'll find the same level for the top and bottom.
                        topElevation += 3;
                    }
                    var foundLevelMatch = false;
                    foreach (var lvl in levelVolumes)
                    {
                        if (lvl.Transform.Origin.Z >= bottomElevation && lvl.Transform.Origin.Z < topElevation - 0.01)
                        {
                            wallsByLevel[lvl.Id.ToString()].Add(wall);
                            foundLevelMatch = true;
                        }
                    }
                    if (!foundLevelMatch)
                    {
                        wallsByLevel["ungrouped"].Add(wall);
                    }
                }
            }

            return (circulationSegmentsByLevel, verticalCirculationByLevel, coresByLevel, wallsByLevel);
        }
    }
}