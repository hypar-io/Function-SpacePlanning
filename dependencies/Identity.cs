using Elements.Geometry;

namespace SpacePlanning
{
    public interface ISpaceBoundaryIdentity
    {
        [Newtonsoft.Json.JsonProperty("Level Add Id", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public string LevelAddId { get; set; }

        [Newtonsoft.Json.JsonProperty("Relative Position", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public Vector3 RelativePosition { get; set; }
    }

    public partial class SpacesIdentity : ISpaceBoundaryIdentity
    {
    }
}