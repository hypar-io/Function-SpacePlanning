using Elements;
using Elements.Geometry;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
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
            // this is a really nasty hack to introduce new behavior for new
            // instances of the function, but keep old instances the same. We
            // created a new input as `boolean?`, and set the default to false,
            // so that new instances of the function would get "false" but old
            // instances of the function would be "null."
            var shouldAutoCreateSpaces = input.AutocreateSpaces == null;

            MessageManager.Initialize(output);
            inputModels.TryGetValue("Floors", out var floorsModel);
            inputModels.TryGetValue("Levels", out var levelsModel);
            inputModels.TryGetValue("Conceptual Mass", out var conceptualMassModel);
            var levelVolumes = new List<LevelVolume>();
            levelVolumes.AddRange(levelsModel?.AllElementsOfType<LevelVolume>().ToList() ?? new List<LevelVolume>());

            // The newer `Floors By Sketch` function produces LevelVolumes. Prefer these over the ones produced by conceptual mass.
            var levelsFromFloors = floorsModel?.AllElementsOfType<LevelVolume>().ToList();
            if (levelsFromFloors != null && levelsFromFloors.Any())
            {
                levelVolumes.AddRange(levelsFromFloors);
            }
            else
            {
                levelVolumes.AddRange(conceptualMassModel?.AllElementsOfType<LevelVolume>().ToList() ?? new List<LevelVolume>());
            }

            var levels = levelsModel?.AllElementsOfType<Level>();
            var levelGroups = levelsModel?.AllElementsOfType<LevelGroup>();

            if (levelVolumes.Count == 0)
            {
                if (levelGroups != null && levelGroups.Any())
                {
                    foreach (var levelGroup in levelGroups)
                    {
                        levelVolumes.AddRange(CreateLevelVolumes(levelGroup.Levels));
                    }
                }
                else if (levels != null && levels.Any())
                {
                    levelVolumes.AddRange(CreateLevelVolumes(levels));
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
            LevelVolume defaultLevelVolume = levelVolumes.Count > 0 ? levelVolumes[0] : null;

            // This code is for backwards compatibility with workflows that did not have a LevelVolume with an "AddId"
            if (defaultLevelVolume != null && defaultLevelVolume.AddId == null)
            {
                defaultLevelVolume.AddId = levelVolumes[0].Name;
            }

            // // Remove overrides that were drawn at levels that have now been removed
            RemoveOverridesAtRemovedLevels(input.Overrides.Spaces, input.Overrides.Additions.Spaces, levelVolumes);

            // These will be used to cut out the autogenerated spaces
            List<SpaceBoundary> manualSpaces = new List<SpaceBoundary>();

            // Removing this behavior for now until we figure out how to cut manually drawn spaces out of the generated spaces
            // GetTemporarySpaceBoundaries(input, levelVolumes, levelLayouts, defaultLevelVolume);

            foreach (var levelVolume in levelVolumes)
            {
                if (levelVolume.PrimaryUseCategory == "Residential")
                {
                    continue;
                }
                var proxy = levelVolume.Proxy("Unit Layout");
                var levelLayout = new LevelLayout(levelVolume, levelGroupedElements, manualSpaces, shouldAutoCreateSpaces);
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
                var levelLayout = new LevelLayout(dummyLevelVolume, levelGroupedElements, manualSpaces, shouldAutoCreateSpaces);
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

            AssignLevelLayoutToSpaceBoundaries(input, levelLayouts, defaultLevelVolume);

            var spaces = levelLayouts.SelectMany(lul => lul.CreateSpacesFromProfiles()).ToList();

            RemoveUnmatchedOverrides(input.Overrides.Spaces, input.Overrides.Additions.Spaces, levelLayouts);

            spaces = input.Overrides.Spaces.CreateElements(
                input.Overrides.Additions.Spaces,
                input.Overrides.Removals.Spaces,
                (add) => SpaceBoundary.Create(add, levelLayouts, levelVolumes),
                (sb, identity) => sb.Match(identity),
                (sb, edit) => sb.Update(edit, levelLayouts, levelVolumes),
                spaces);

            var levelElements = spaces.Select(s => s.LevelElements).Distinct().ToList();

            // This is just for the case where we delete a space that was removed after Circulation splits the generated space.
            RemoveAutoGeneratedSpacesContainedByRemovals(spaces, input.Overrides.Removals.Spaces);

            // Anthonie: I don't think we need this anymore, because we first resolve overrides and subtract them from
            // the generated space in the code in `GetTemporarySpaceBoundaries`
            // RemoveAutoGeneratedSpacesContainedByAdditions(spaces, input.Overrides.Additions.Spaces);

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
            output.Model.AddElements(levelElements);
            output.Model.AddElements(spaces);

            return output;
        }

        private static void AssignLevelLayoutToSpaceBoundaries(SpacePlanningInputs input, List<LevelLayout> levelLayouts, LevelVolume defaultLevelVolume)
        {
            var defaultLevelLayout = levelLayouts.SingleOrDefault(x => x.LevelVolume == defaultLevelVolume);

            foreach (var space in input.Overrides.Additions.Spaces)
            {
                if (space.Value.LevelLayout == null)
                {
                    if (defaultLevelLayout != null)
                    {
                        space.Value.LevelLayout = new SpacesOverrideAdditionValueLevelLayout(defaultLevelLayout.Name, defaultLevelVolume?.BuildingName, defaultLevelVolume.AddId);
                    }
                }
            }

            foreach (var space in input.Overrides.Spaces)
            {
                // if the space level ID is not in level layouts, then we assume it has been removed and assign the default
                if (space.Value.Level == null && space.Identity.LevelAddId == "dummy-level-volume")// || levelLayouts.SingleOrDefault(x => x.AddId == space.Value.Level.AddId) == null)
                {
                    if (defaultLevelVolume != null)
                    {
                        var autoLevel = new SpacesValueLevel(defaultLevelVolume.AddId, defaultLevelVolume.Name, defaultLevelVolume.BuildingName);
                        space.Value.Level = autoLevel;
                        space.Identity.LevelAddId = autoLevel.AddId;
                    }
                }
            }
        }

        private static List<SpaceBoundary> GetTemporarySpaceBoundaries(SpacePlanningInputs input, List<LevelVolume> levelVolumes, List<LevelLayout> levelLayouts, LevelVolume defaultLevelVolume)
        {
            foreach (var space in input.Overrides.Additions.Spaces)
            {
                if (space.Value.Level == null)
                {
                    if (defaultLevelVolume != null)
                    {
                        space.Value.Level = new SpacesOverrideAdditionValueLevel(defaultLevelVolume.AddId, defaultLevelVolume.Name, defaultLevelVolume.BuildingName);
                    }
                }
            }

            foreach (var space in input.Overrides.Spaces)
            {
                // if the space level ID is not in level layouts, then we assume it has been removed and assign the default
                if (space.Value.Level == null)// || levelLayouts.SingleOrDefault(x => x.AddId == space.Value.Level.AddId) == null)
                {
                    if (defaultLevelVolume != null && space.Identity.LevelAddId == "dummy-level-volume")
                    {
                        var autoLevel = new SpacesValueLevel(defaultLevelVolume.AddId, defaultLevelVolume.Name, defaultLevelVolume.BuildingName);
                        space.Value.Level = autoLevel;
                        space.Identity.LevelAddId = autoLevel.AddId;
                        space.Identity.TemporaryReferenceLevel = true;
                    }
                }
            }
            // This was causing all kinds of unpredictable behavior, with spaces coming back from the dead after having been removed, etc.
            // var tempSpaces = input.Overrides.Spaces.CreateElements(
            //     input.Overrides.Additions.Spaces,
            //     input.Overrides.Removals.Spaces,
            //     (add) => SpaceBoundary.Create(add, levelLayouts, levelVolumes),
            //     (sb, identity) => sb.Match(identity),
            //     (sb, edit) => sb.Update(edit, levelLayouts, levelVolumes),
            //     null);

            return new List<SpaceBoundary>();
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
                        if (largestSpace.Area() < 0.1)
                        {
                            spaces.Remove(space);
                            continue;
                        }
                        space.Boundary = largestSpace;
                    }
                }
            }
        }


        private static void RemoveAutoGeneratedSpacesContainedByAdditions(List<SpaceBoundary> spaces, IList<SpacesOverrideAddition> additions)
        {
            var addedAreasByLevel = additions.GroupBy(r => r.Value?.Level?.AddId ?? r.Value?.Level?.Name ?? "Level 1").ToDictionary(grp => grp.Key ?? "none", grp => grp.Select(r => new Profile(r.Value.Boundary.Perimeter, r.Value.Boundary.Voids)).ToList());

            var allUnmodifiedSpaces = spaces.Where(s => !s.AdditionalProperties.ContainsKey("associatedIdentities") && s.Boundary != null).ToList();
            for (int i = 0; i < allUnmodifiedSpaces.Count; i++)
            {
                var space = allUnmodifiedSpaces[i];
                var spaceLevel = space.LevelAddId;
                if (addedAreasByLevel.TryGetValue(spaceLevel, out var addedAreas) || addedAreasByLevel.TryGetValue(space.LevelVolume.Name, out addedAreas))
                {
                    var nonNullRemovedAreas = addedAreas.Where(a => a != null && a.Perimeter != null);
                    if (!nonNullRemovedAreas.Any())
                    {
                        continue;
                    }
                    try
                    {
                        var difference = Profile.Difference(new[] { space.Boundary }, nonNullRemovedAreas);
                        if (difference.Count == 0)
                        {
                            spaces.Remove(space);
                        }
                        else
                        {
                            var largestSpace = difference.OrderByDescending(p => Math.Abs(p.Area())).First();
                            if (largestSpace.Area() < 0.1)
                            {
                                spaces.Remove(space);
                                continue;
                            }
                            space.Boundary = largestSpace;
                        }
                    }
                    catch
                    {
                        spaces.Remove(space);
                    }
                }
            }
        }


        private static void RemoveUnmatchedOverrides(IList<SpacesOverride> edits, IList<SpacesOverrideAddition> additions, List<LevelLayout> levelLayouts)
        {
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

        private static void RemoveOverridesAtRemovedLevels(IList<SpacesOverride> edits, IList<SpacesOverrideAddition> additions, List<LevelVolume> levelVolumes)
        {
            // If we created a single dummy level layout, just assume all spaces belong to that and don't bother filtering.
            // If there is only one level volume, assume that any drawn spaces belong to it.
            if (levelVolumes.Count <= 1 || levelVolumes.Any(l => l.Name.Contains("dummy")))
            {
                return;
            }
            foreach (var edit in new List<SpacesOverride>(edits))
            {

                // Ignore additions that were never assigned a level because we will assign them to the default level later.
                if (edit.Value?.Level == null)
                {
                    continue;
                }


                var matchingLevelVolume =
                    levelVolumes.FirstOrDefault(ll => edit.Value?.Level?.AddId != null && ll.AddId == edit.Value?.Level?.AddId) ??
                    levelVolumes.FirstOrDefault(ll => ll.Name == edit.Value?.Level?.Name);

                if (matchingLevelVolume == null)
                {
                    edits.Remove(edit);
                }
            }

            foreach (var addition in new List<SpacesOverrideAddition>(additions))
            {
                // Ignore additions that were never assigned a level because we will assign them to the default level later.
                if (addition.Value?.Level == null)
                {
                    continue;
                }

                var matchingLevelVolume =
                    levelVolumes.FirstOrDefault(ll => addition.Value?.Level?.AddId != null && ll.AddId == addition.Value?.Level?.AddId) ??
                    levelVolumes.FirstOrDefault(ll => ll.Name == addition.Value?.Level?.Name);

                if (matchingLevelVolume == null)
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
                if (coreElements == null)
                {

                }

                foreach (var lvl in levelVolumes)
                {
                    if (lvl.Profile == null)
                    {
                        continue;
                    }
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

        private static IEnumerable<LevelVolume> CreateLevelVolumes(IEnumerable<Level> levels)
        {
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
                
                yield return levelVolume;
            }
        }
    }
}