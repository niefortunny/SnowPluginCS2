using System.Collections.Concurrent;
using Clientprefs.API;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Capabilities;
using Microsoft.Extensions.Logging;

public partial class SnowPlugin
{
    private readonly PluginCapability<IClientprefsApi> g_PluginCapability = new("Clientprefs");
    private IClientprefsApi? ClientprefsApi;
    private readonly ConcurrentDictionary<CCSPlayerController, SnowEffectCookie> PlayerCookies =
        new();
    private readonly Dictionary<CookieType, int> Cookies = [];

    private enum CookieType
    {
        SnowEffect,
    }

    private class SnowEffectCookie
    {
        public bool SnowEffect { get; set; } = true;
    }

    public override void Unload(bool hotReload)
    {
        base.Unload(hotReload);

        if (ClientprefsApi == null)
            return;

        ClientprefsApi.OnDatabaseLoaded -= OnClientprefDatabaseReady;
        ClientprefsApi.OnPlayerCookiesCached -= OnPlayerCookiesCached;
    }

    public override void OnAllPluginsLoaded(bool hotReload)
    {
        ClientprefsApi = g_PluginCapability.Get();
        if (ClientprefsApi == null)
        {
            Logger.LogError("Failed to get ClientprefsApi");
            return;
        }

        ClientprefsApi.OnDatabaseLoaded += OnClientprefDatabaseReady;
        ClientprefsApi.OnPlayerCookiesCached += OnPlayerCookiesCached;

        if (hotReload)
        {
            foreach (var player in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
            {
                if (!ClientprefsApi.ArePlayerCookiesCached(player))
                    continue;

                LoadPlayerSettings(player);
            }
        }
    }

    private void LoadPlayerSettings(CCSPlayerController player)
    {
        if (!player.IsValid || player.IsBot || ClientprefsApi == null)
            return;

        if (!PlayerCookies.TryAdd(player, new SnowEffectCookie()))
        {
            PlayerCookies[player] = new SnowEffectCookie();
        }

        var settings = PlayerCookies[player];

        foreach (var cookie in Cookies.Where(c => c.Value != -1))
        {
            string value = ClientprefsApi.GetPlayerCookie(player, cookie.Value);
            if (string.IsNullOrEmpty(value))
            {
                value = "true";
                ClientprefsApi.SetPlayerCookie(player, cookie.Value, value);
            }

            if (!bool.TryParse(value, out bool result))
            {
                Logger.LogWarning($"Invalid cookie value for {cookie.Key}: {value}");
                result = true;
            }

            switch (cookie.Key)
            {
                case CookieType.SnowEffect:
                    settings.SnowEffect = result;
                    break;
            }
        }
    }

    public void OnClientprefDatabaseReady()
    {
        if (ClientprefsApi == null)
        {
            Logger.LogError("ClientprefsApi is null in OnClientprefDatabaseReady");
            return;
        }

        var cookieDefs = new (CookieType type, string name, string desc)[]
        {
            (
                CookieType.SnowEffect,
                "vip_killboosteffect",
                "Whether kill boost effects are enabled"
            ),
        };

        foreach (var (type, name, desc) in cookieDefs)
        {
            int cookie = ClientprefsApi.RegPlayerCookie(
                name,
                desc,
                CookieAccess.CookieAccess_Public
            );
            if (cookie == -1)
            {
                Logger.LogError($"Failed to register cookie: {name}");
                continue;
            }

            Cookies[type] = cookie;
            Logger.LogInformation($"Registered/Loaded cookie {type} ({name}) with ID: {cookie}");
        }
    }

    public void OnPlayerCookiesCached(CCSPlayerController player)
    {
        LoadPlayerSettings(player);
    }
}
