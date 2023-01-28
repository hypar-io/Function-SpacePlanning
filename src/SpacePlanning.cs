using Elements;
using Elements.Geometry;
using Newtonsoft.Json.Linq;
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
            inputModels.TryGetValue("Conceptual Mass", out var conceptualMassModel);
            var levelVolumes = conceptualMassModel?.AllElementsOfType<LevelVolume>().ToList() ?? new List<LevelVolume>();
            inputModels.TryGetValue("Levels", out var levelsModel);
            levelVolumes.AddRange(levelsModel?.AllElementsOfType<LevelVolume>().ToList() ?? new List<LevelVolume>());
            if (levelVolumes.Count == 0)
            {
                var levels = levelsModel?.AllElementsOfType<Level>();
                if (levels != null && levels.Count() > 0)
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
            if (programReqs != null && programReqs.Count() > 0)
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

            if (levelVolumes.Count() == 0)
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
                    var levelLayout = levelLayouts.FirstOrDefault(l => l.LevelVolumes.First().Id == group.Key);
                    levelLayout?.AddExistingSpaces(group.ToList());
                }
            }
            else if (input.OldSpaceBoundaries?.LocalFilePath != null && File.Exists(input.OldSpaceBoundaries.LocalFilePath))
            {
                var oldSpaceBoundaryModel = Model.FromJson(File.ReadAllText(input.OldSpaceBoundaries.LocalFilePath));
                var oldSpaces = oldSpaceBoundaryModel.AllElementsOfType<SpaceBoundary>();
                var oldSpacesByLevel = oldSpaces.GroupBy(s => s.Level);
                foreach (var group in oldSpacesByLevel)
                {
                    var levelLayout = levelLayouts.FirstOrDefault(l => l.LevelVolumes.First().Id == group.Key);
                    levelLayout?.AddExistingSpaces(group.ToList());
                }
            }

            var levelUnitLayoutsOverridden = input.Overrides.LevelLayout.Apply(
              levelLayouts,
              (lul, identity) => lul.Match(identity),
              (lul, edit) => lul.Update(edit, levelGroupedElements));


            var spaces = levelUnitLayoutsOverridden.SelectMany(lul => lul.CreateSpaces()).ToList();
            spaces = input.Overrides.ProgramAssignment.Apply(
                spaces,
                (sb, identity) => sb.Match(identity),
                (sb, edit) => sb.Update(edit));
            var levelElements = spaces.Select(s => s.LevelElements).Distinct().ToList();
            foreach (var space in spaces)
            {
                var spaceLevelElements = space.LevelElements;
                spaceLevelElements.Elements.Add(space);
                // weird legacy stuff, TODO remove when layout functions don't depend on it
                space.LevelElements = null;
                space.LevelVolume = null;
                output.Model.AddElement(space);
            }
            output.Model.AddElements(levelUnitLayoutsOverridden);
            output.Model.AddElements(levelUnitLayoutsOverridden.SelectMany(lul => lul.CreateModelLines()));
            output.Model.AddElements(levelElements);
            output.Model.AddElements(spaces);

            return output;
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