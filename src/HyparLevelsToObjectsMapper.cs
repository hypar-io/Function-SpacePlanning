using Elements;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpacePlanning
{
    internal class HyparLevelsToObjectsMapper : ILevelsToObjectsMapper
    {
        private readonly IEnumerable<LevelVolume> _levelVolumes;

        public HyparLevelsToObjectsMapper(IEnumerable<LevelVolume> levelVolumes)
        {
            _levelVolumes = levelVolumes;
        }

        public bool TryMapWallToLevels(Wall wall, Dictionary<string, List<Wall>> wallsByLevel)
        {
            // figure out which levels the wall belongs to. Sometimes
            // walls will have levels attached, otherwise we might have
            // to infer by geometry.
            WallLevelInfo levelInfo = null;
            if (wall.AdditionalProperties.TryGetValue("Levels", out var levels))
            {
                levelInfo = WallLevelInfo.FromJObject(levels as JObject);
            }

            var bottomLevel = _levelVolumes.FirstOrDefault(lv => lv.Level == levelInfo?.BottomLevel.Id);
            var topLevel = _levelVolumes.FirstOrDefault(lv => lv.Level == levelInfo?.TopLevel.Id);
            var bottomElevation = bottomLevel?.Transform.Origin.Z ?? 0;
            var topElevation = topLevel?.Transform.Origin.Z ?? bottomElevation + wall.GetHeight();
            if (topElevation == bottomElevation)
            {
                // in a levels from floors scenario w/ a single level, we'll find the same level for the top and bottom.
                topElevation += 3;
            }

            var foundLevelMatch = false;
            foreach (var lvl in _levelVolumes)
            {
                if (lvl.Transform.Origin.Z >= bottomElevation && lvl.Transform.Origin.Z < topElevation - 0.01)
                {
                    wallsByLevel[lvl.Id.ToString()].Add(wall);
                    foundLevelMatch = true;
                }
            }

            return foundLevelMatch;
        }
    }
}
