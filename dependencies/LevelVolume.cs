using Elements;
using System;
using System.Linq;
using System.Collections.Generic;
using Elements.Geometry;
using Newtonsoft.Json;

namespace Elements
{
    public partial class LevelVolume
    {
        [JsonProperty("Primary Use Category")]
        public string PrimaryUseCategory { get; set; }

        [JsonProperty("Add Id")]
        public string AddId { get; set; }

        [JsonIgnore]
        public Transform LocalCoordinateSystem { get; set; }

        public Guid? Envelope { get; set; }
    }
}