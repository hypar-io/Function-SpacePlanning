using Elements.Geometry;

namespace Elements
{
    public static class WallHelpers
    {
        public static double GetHeight(this Wall w)
        {
            return w switch
            {
                StandardWall sw => sw.Height,
                WallByProfile wp => wp.GetHeight(),
                _ => 0,
            };
        }

        public static Line GetCenterline(this Wall w)
        {
            return w switch
            {
                StandardWall sw => sw.CenterLine,
                WallByProfile wp => wp.Centerline,
                _ => null,
            };
        }
    }
}