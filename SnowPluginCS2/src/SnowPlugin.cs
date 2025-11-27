using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnowPlugin;

public class SnowConfig : BasePluginConfig
{
    [JsonPropertyName("particle_name")]
    public string ParticleName { get; set; } = "particles/snow.vpcf"; //specify the path to the file specified when publishing the add-on in the workshop

    [JsonPropertyName("offset_z")]
    public float OffsetZ { get; set; } = 200.0f;
}

public class SnowData
{
    public Dictionary<ulong, bool> PlayerPreferences { get; set; } = new();
}

public class SnowPlugin : BasePlugin, IPluginConfig<SnowConfig>
{
    public override string ModuleName => "Snow Plugin";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "ALBAN1776";
    public override string ModuleDescription => "Creates snow particle";

    public SnowConfig Config { get; set; } = new();
    private SnowData _data = new();
    private string _dataFilePath = "";

    private readonly Dictionary<int, uint> _activeParticles = new();

    public void OnConfigParsed(SnowConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        _dataFilePath = Path.Combine(ModuleDirectory, "snow_data.json");
        LoadData();

        RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
        RegisterListener<Listeners.OnClientDisconnectPost>(OnClientDisconnect);
    }

    private void LoadData()
    {
        if (File.Exists(_dataFilePath))
        {
            try
            {
                var json = File.ReadAllText(_dataFilePath);
                _data = JsonSerializer.Deserialize<SnowData>(json) ?? new SnowData();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Snow] Error loading data: {ex.Message}");
            }
        }
    }

    private void SaveData()
    {
        try
        {
            var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_dataFilePath, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Snow] Error saving data: {ex.Message}");
        }
    }

    private void OnClientConnected(int slot)
    {
        // When the player enters nothing needs to be done
    }

    private void OnClientDisconnect(int slot)
    {
        RemoveSnow(slot);
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.IsBot)
            return HookResult.Continue;

        RemoveSnow(player.Slot);

        if (GetPlayerSnowState(player.SteamID))
        {
            AddTimer(0.2f, () => CreateSnow(player));
        }

        return HookResult.Continue;
    }

    [ConsoleCommand("css_snow", "Toggle snow effect")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnSnowCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null) return;

        bool currentState = GetPlayerSnowState(player.SteamID);
        bool newState = !currentState;

        _data.PlayerPreferences[player.SteamID] = newState;
        SaveData();

        if (newState)
        {
            CreateSnow(player);
            player.PrintToChat($" {ChatColors.Green}[Snow] {ChatColors.White}Effect {ChatColors.Green}Enabled");
        }
        else
        {
            RemoveSnow(player.Slot);
            player.PrintToChat($" {ChatColors.Red}[Snow] {ChatColors.White}Effect {ChatColors.Red}Disabled");
        }
    }

    private bool GetPlayerSnowState(ulong steamId)
    {
        if (_data.PlayerPreferences.TryGetValue(steamId, out var enabled))
        {
            return enabled;
        }
        return true;
    }

    private void CreateSnow(CCSPlayerController player)
    {
        if (player.PlayerPawn == null || !player.PlayerPawn.IsValid) return;
        var pawn = player.PlayerPawn.Value;

        var particle = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
        if (particle == null) return;

        Vector pos = pawn.AbsOrigin!;
        pos.Z += Config.OffsetZ;

        QAngle ang = pawn.AbsRotation!;

        particle.EffectName = Config.ParticleName;
        particle.StartActive = true;

        particle.Teleport(pos, ang, null);
        particle.DispatchSpawn();
        particle.AcceptInput("SetParent", pawn, null, "!activator");

        _activeParticles[player.Slot] = particle.Index;
    }

    private void RemoveSnow(int slot)
    {
        if (_activeParticles.TryGetValue(slot, out var entIndex))
        {
            var entity = Utilities.GetEntityFromIndex<CParticleSystem>((int)entIndex);
            if (entity != null && entity.IsValid)
            {
                entity.AcceptInput("Stop", null, null, null);
                entity.Remove();
            }
            _activeParticles.Remove(slot);
        }
    }
}