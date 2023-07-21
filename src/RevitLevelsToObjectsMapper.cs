using Elements;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpacePlanning
{
    internal class RevitLevelsToObjectsMapper : ILevelsToObjectsMapper
    {
        private readonly IEnumerable<LevelVolume> _levels;

        public RevitLevelsToObjectsMapper(IEnumerable<LevelVolume> levels)
        {
            _levels = levels;
        }

        public bool TryMapWallToLevels(Wall wall, Dictionary<string, List<Wall>> wallsByLevel)
        {
            if (!wall.AdditionalProperties.TryGetValue("Base Constraint", out var baseConstraint))
            {
                return false;
            }

            if (baseConstraint == null) 
            { 
                return false; 
            }

            if (!wall.AdditionalProperties.TryGetValue("Top Constraint", out var topConstraint))
            {
                return false;
            }

            string baseLevelName = baseConstraint.ToString();
            string topLevelName = topConstraint?.ToString();

            bool isBaseFound = false;
            foreach (var level in _levels)
            {
                if (level.Name == baseLevelName)
                {
                    isBaseFound = true;
                }

                if (level.Name == topLevelName)
                {
                    break;
                }

                if (isBaseFound)
                {
                    wallsByLevel[level.Id.ToString()].Add(wall);
                }
            }

            return isBaseFound;
        }
    }
}
