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
                WallByProfile wp => wp.GetWallHeight(),
                _ => 0,
            };
        }

        public static double GetWallHeight(this WallByProfile w)
        {
            try
            {
                return w.GetHeight();
            }
            catch
            {
                w.UpdateRepresentations();
                var bbox = new BBox3(w);
                return bbox.Max.Z - bbox.Min.Z;
            }
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