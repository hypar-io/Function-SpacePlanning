using Elements;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Newtonsoft.Json;
using SpacePlanning;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Elements
{
    public partial class Level : Element
    {
        [JsonProperty("Add Id")]
        public string AddId { get; set; }
    }
}