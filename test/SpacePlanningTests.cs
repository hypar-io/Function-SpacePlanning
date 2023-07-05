using Elements;
using Xunit;
using System.IO;
using System.Collections.Generic;
using Elements.Serialization.glTF;
using System.Linq;

namespace SpacePlanning.Tests
{
    public class SpacePlanningTests
    {
        private const string INPUT = "../../../_input/";
        private const string OUTPUT = "../../../_output/";

        [Fact]
        public void SpacePlanning2Floors()
        {
            // test with a two-story building with four rooms on each floor
            var testName = "2Floors";
            var modelNames = new string[] { "Circulation", "Levels" };
            var output = SpacePlanningTest(testName, modelNames);
            var boundariesCountByLevels = new Dictionary<string, Dictionary<string, (int count, double area)>>()
            {
                ["B1"] = new Dictionary<string, (int count, double area)>()
                {
                    ["unspecified"] = (3, 3855.7),
                    ["Lounge"] = (1, 2246),
                },
                ["Ground Level"] = new Dictionary<string, (int count, double area)>()
                {
                    ["unspecified"] = (3, 3818.9),
                    ["Reception"] = (1, 1079.3),
                },
            };

            CheckSpaces(output, boundariesCountByLevels);
        }

        [Fact]
        public void SpacePlanning6Floors()
        {
            // test with a six-story building with three rooms on B1, Ground Level, Level 2, Level 4 floors
            // and with two rooms on Level 1, Level 3 floors
            var testName = "6Floors";
            var modelNames = new string[] { "Circulation", "Levels" };
            var output = SpacePlanningTest(testName, modelNames);
            var boundariesCountByLevels = new Dictionary<string, Dictionary<string, (int count, double area)>>()
            {
                ["B1"] = new Dictionary<string, (int count, double area)>()
                {
                    ["unspecified"] = (3, 3855.7),
                    ["Lounge"] = (1, 2246),
                },
                ["Ground Level"] = new Dictionary<string, (int count, double area)>()
                {
                    ["unspecified"] = (3, 3818.9),
                    ["Reception"] = (1, 1079.3),
                },
                ["Level 1"] = new Dictionary<string, (int count, double area)>()
                {
                    ["unspecified"] = (3, 6924.1),
                },
                ["Level 2"] = new Dictionary<string, (int count, double area)>()
                {
                    ["unspecified"] = (4, 6924.1),
                },
                ["Level 3"] = new Dictionary<string, (int count, double area)>()
                {
                    ["unspecified"] = (3, 6924.1),
                },
                ["Level 4"] = new Dictionary<string, (int count, double area)>()
                {
                    ["unspecified"] = (4, 6924.1),
                },
            };

            CheckSpaces(output, boundariesCountByLevels);
        }

        [Fact]
        private void SampleProjectTest()
        {
            var testName = "SampleProject";
            var modelNames = new string[] { "Circulation", "Levels", "Program Requirements", "Core", "Floors" };
            var output = SpacePlanningTest(testName, modelNames);
            var boundariesCountByLevels = new Dictionary<string, Dictionary<string, (int count, double area)>>()
            {
                ["Level 1"] = new Dictionary<string, (int count, double area)>()
                {
                    ["Meeting Room"] = (12, 272.1),
                    ["Open Collaboration"] = (2, 39.9),
                    ["Resource / Copy"] = (3, 57.3),
                    ["Private Office"] = (12, 221.8),
                    ["Open Office"] = (11, 1134.3),
                    ["Phone Booth"] = (2, 15.2),
                    ["Circulation"] = (1, 10.2),
                    ["Lounge"] = (1, 37.2),
                    ["Reception"] = (1, 42.2),
                    ["Pantry"] = (1, 89.3),
                    ["Display Board"] = (1, 9.7),
                    ["Storage"] = (9, 50.7),
                    ["Electrical Room"] = (1, 19.7),
                    ["Servers / IT"] = (1, 12.9),
                    ["Coffee Point"] = (4, 17.2),
                    ["Coat Closet"] = (2, 8.3),
                    ["Coffee"] = (1, 18.5),
                    ["Mother's Lounge"] = (1, 15.1),
                },
            };

            CheckSpaces(output, boundariesCountByLevels);
        }

        private SpacePlanningOutputs SpacePlanningTest(string testName, string[] modelNames)
        {
            var models = modelNames.ToDictionary(name => name, name => Model.FromJson(System.IO.File.ReadAllText($"{INPUT}/{testName}/{name}.json")));
            var input = GetInput(testName);
            var output = SpacePlanning.Execute(models, input);

            System.IO.File.WriteAllText($"{OUTPUT}/{testName}/SpacePlanning.json", output.Model.ToJson());
            foreach (var model in models)
            {
                output.Model.AddElements(model.Value.Elements.Values);
            }
            output.Model.ToGlTF($"{OUTPUT}/{testName}/SpacePlanning.glb");
            output.Model.ToGlTF($"{OUTPUT}/{testName}/SpacePlanning.gltf", false);

            return output;
        }

        private void CheckSpaces(SpacePlanningOutputs output, Dictionary<string, Dictionary<string, (int count, double area)>> boundariesCountByLevels)
        {
            var boundaries = output.Model.AllElementsOfType<SpaceBoundary>();
            var boundariesByLevels = boundaries.GroupBy(b => b.LevelAddId).Select(g => (g.Key, g.GroupBy(b => b.ProgramType))).ToList();
            Assert.Equal(boundariesCountByLevels.Count(), boundariesByLevels.Count());

            foreach (var boundariesByLevel in boundariesByLevels)
            {
                foreach (var boundariesByType in boundariesByLevel.Item2)
                {
                    Assert.Equal(boundariesCountByLevels[boundariesByLevel.Key][boundariesByType.Key].count, boundariesByType.Count());
                    Assert.True(boundariesCountByLevels[boundariesByLevel.Key][boundariesByType.Key].area.ApproximatelyEquals(boundariesByType.Sum(b => b.Area), 0.1));
                }
            }
        }

        private SpacePlanningInputs GetInput(string testName)
        {
            var json = File.ReadAllText($"{INPUT}/{testName}/inputs.json");
            return Newtonsoft.Json.JsonConvert.DeserializeObject<SpacePlanningInputs>(json);
        }
    }
}