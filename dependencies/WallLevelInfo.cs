using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class WallLevelInfo
{
    [JsonProperty("Bottom Level")]
    public LevelInfo BottomLevel { get; set; }

    [JsonProperty("Top Level")]
    public LevelInfo TopLevel { get; set; }

    public static WallLevelInfo FromJObject(JObject jObject)
    {
        if (jObject == null)
        {
            return null;
        }
        var info = new WallLevelInfo();
        try
        {
            if (jObject.ContainsKey("Bottom Level"))
            {
                info.BottomLevel = jObject["Bottom Level"].ToObject<LevelInfo>();
            }

            if (jObject.ContainsKey("Top Level"))
            {
                info.TopLevel = jObject["Top Level"].ToObject<LevelInfo>();
            }
        }
        catch
        {
            return null;
        }

        return info;
    }
}

public class LevelInfo
{
    [JsonProperty("Id")]
    public Guid Id { get; set; }

    [JsonProperty("Elevation")]
    public double? Elevation { get; set; }
}