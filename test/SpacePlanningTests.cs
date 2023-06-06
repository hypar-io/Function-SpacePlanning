using Elements;
using Xunit;
using System;
using System.IO;
using System.Collections.Generic;
using Elements.Serialization.glTF;

namespace SpacePlanning.Tests
{
    public class SpacePlanningTests
    {
        private const string INPUT = "../../../_input/";
        private const string OUTPUT = "../../../_output/";

        [Fact]
        public void SpacePlanning2Floors()
        {
            // test with a two-story building with three rooms on each floor
            var testName = "2Floors";
            SpacePlanningTest(testName);
        }

        [Fact]
        public void SpacePlanning6Floors()
        {
            // test with a six-story building with three rooms on B1, Ground Level, Level 2, Level 4 floors
            // and with two rooms on Level 1, Level 3 floors
            var testName = "6Floors";
            SpacePlanningTest(testName);
        }

        private void SpacePlanningTest(string testName)
        {
            var circulationModel = Model.FromJson(System.IO.File.ReadAllText($"{INPUT}/{testName}/Circulation.json"));
            var levelsModel = Model.FromJson(System.IO.File.ReadAllText($"{INPUT}/{testName}/Levels.json"));
            var inputs = GetInput(testName);
            var outputs = SpacePlanning.Execute(
                new Dictionary<string, Model>
                {
                    {"Circulation", circulationModel},
                    {"Levels", levelsModel}
                }, inputs);

            System.IO.File.WriteAllText($"{OUTPUT}/{testName}/SpacePlanning.json", outputs.Model.ToJson());
            outputs.Model.AddElements(circulationModel.Elements.Values);
            outputs.Model.AddElements(levelsModel.Elements.Values);
            outputs.Model.ToGlTF($"{OUTPUT}/{testName}/SpacePlanning.glb");
        }

        private SpacePlanningInputs GetInput(string testName)
        {
            var json = File.ReadAllText($"{INPUT}/{testName}/inputs.json");
            return Newtonsoft.Json.JsonConvert.DeserializeObject<SpacePlanningInputs>(json);
        }
    }
}