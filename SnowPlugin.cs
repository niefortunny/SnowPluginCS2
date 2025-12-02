using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Utils;

public partial class SnowPlugin : BasePlugin, IPluginConfig<SnowConfig>
{
    public override string ModuleName => "Snow Plugin";
    public override string ModuleVersion => "1.0.2";
    public override string ModuleAuthor => "ALBAN1776 | fork: unfortunate";
    public override string ModuleDescription => "Creates snow particle";

    public SnowConfig Config { get; set; } = new();
    private readonly Dictionary<int, CParticleSystem> _activeParticles = [];

    public void OnConfigParsed(SnowConfig config)
    {
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        RegisterListener<Listeners.OnClientDisconnectPost>(OnClientDisconnect);

        RegisterListener<Listeners.OnServerPrecacheResources>(
            (manifest) => manifest.AddResource(Config.ParticleName)
        );
    }

    private void OnClientDisconnect(int slot)
    {
        RemoveSnow(slot);
    }

    [GameEventHandler]
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (
            player == null
            || !player.IsValid
            || player.IsBot
            || !PlayerCookies.TryGetValue(player, out var cookie)
            || !cookie.SnowEffect
        )
            return HookResult.Continue;

        RemoveSnow(player.Slot);
        AddTimer(0.3f, () => CreateSnow(player));

        return HookResult.Continue;
    }

    [ConsoleCommand("css_snow", "Toggle snow effect")]
    [CommandHelper(whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnSnowCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (
            player == null
            || !player.IsValid
            || ClientprefsApi == null
            || Cookies[CookieType.SnowEffect] == -1
        )
            return;

        string? newValue = PlayerCookies[player].SnowEffect ? "false" : "true";

        ClientprefsApi.SetPlayerCookie(player, Cookies[CookieType.SnowEffect], newValue);
        PlayerCookies[player].SnowEffect = bool.Parse(newValue);

        player.PrintToChat(
            $"{Localizer.ForPlayer(player, "Prefix")}  {Localizer.ForPlayer(player, $"Cookie.SetTo.{newValue}")}"
        );

        if (bool.Parse(newValue))
            CreateSnow(player);
        else
            RemoveSnow(player.Slot);
    }

    private void CreateSnow(CCSPlayerController player)
    {
        if (player is null || player.PlayerPawn.Value == null || !player.PlayerPawn.IsValid)
            return;

        var pawn = player.PlayerPawn.Value;
        if (pawn is null || !pawn.IsValid)
            return;

        RemoveSnow(player.Slot);

        var particle = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
        if (particle == null)
            return;

        particle.EffectName = Config.ParticleName;

        Vector pos = pawn.AbsOrigin!;
        QAngle ang = pawn.AbsRotation!;

        particle.Teleport(pos, ang, new Vector(0, 0, 0));
        particle.DispatchSpawn();

        Server.NextFrame(() =>
        {
            if (particle != null && particle.IsValid && pawn.IsValid)
            {
                particle.AcceptInput("SetParent", pawn, null, "!activator");
                particle.AcceptInput("Start");
            }
        });

        _activeParticles[player.Slot] = particle;
    }

    private void RemoveSnow(int slot)
    {
        if (!_activeParticles.TryGetValue(slot, out var particle))
            return;

        if (particle is null || particle.IsValid)
            return;

        particle.AcceptInput("Stop");
        particle.Remove();

        _activeParticles.Remove(slot);
    }
}
