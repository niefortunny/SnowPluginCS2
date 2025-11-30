using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

public class SnowConfig : BasePluginConfig
{
    [JsonPropertyName("particle_name")]
    public string ParticleName { get; set; } = "particles/snow.vpcf";
}
