using Elements;
using System.Collections.Generic;

namespace SpacePlanning
{
    internal interface ILevelsToObjectsMapper
    {
        public bool TryMapWallToLevels(Wall wall, Dictionary<string, List<Wall>> wallsByLevel);
    }
}
