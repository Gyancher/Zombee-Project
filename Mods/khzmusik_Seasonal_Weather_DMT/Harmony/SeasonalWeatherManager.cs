using DMT;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Introduces seasonal weather to the game. The seasonal year length and temperature variation
/// can be set in worldglobal.xml.
/// </summary>
/// <example>
/// <code>
/// &lt;property name="DaysPerMeteorologicalYear" value="84" />
/// &lt;property name="TemperatureVariation" value="40" />
/// </code>
/// </example>
public class SeasonalWeatherManager : IHarmony
{
    /// <summary>
    /// The name of the "person" who sends chat messages to the player about crop updates.
    /// This should be a key in Localization.txt.
    /// </summary>
    public const string ChatMainName = "weatherChatName";

    /// <summary>
    /// Default temperature variation (from WeatherManager.GenerateWeather).
    /// </summary>
    private const float DefaultTemperatureVariation = 31;

    /// <summary>
    /// Number of minutes per day.
    /// </summary>
    private const int MinutesPerDay = 60 * 24;

    /// <summary>
    /// Number of minutes per unit of world time. There are 24000 world time units in a game day,
    /// so one hour is 1000 units, with 60 minutes per hour.
    /// </summary>
    private const float MinutesPerWorldTime = 0.06f;

    /// <summary>
    /// Median temperature, midway between 70 and 101 degrees Fahrenheit (hard-coded by TFP).
    /// </summary>
    private const float MedianTemperature = 85.5f;

    /// <summary>
    /// Reference angle in radians at which summer starts.
    /// </summary>
    private static readonly float StartSummer = (float)Math.PI / 4.0f;

    /// <summary>
    /// Reference angle in radians at which fall starts.
    /// </summary>
    private static readonly float StartFall = (float)Math.PI * 3.0f / 4.0f;

    /// <summary>
    /// Reference angle in radians at which winter starts.
    /// </summary>
    private static readonly float StartWinter = (float)Math.PI * 5.0f / 4.0f;

    /// <summary>
    /// Reference angle in radians at which spring starts.
    /// </summary>
    private static readonly float StartSpring = (float)Math.PI * 7.0f / 4.0f;

    /// <summary>
    /// The minutes per meteorological year, set according to "DaysPerMeteorologicalYear" in
    /// worldglobal.xml.
    /// </summary>
    private int _minutesPerMYear = -1;

    /// <summary>
    /// Whether seasonal weather is enabled.
    /// </summary>
    private bool _enabled = true;

    /// <summary>
    /// The grace period. Initial value is the hard-coded TFP value for the weather grace period.
    /// </summary>
    private ulong _gracePeriod = 30000;

    /// <summary>
    /// The last time we checked whether the season has changed.
    /// </summary>
    private ulong _lastSeasonCheckTime = 0;

    /// <summary>
    /// The season we last logged to the console.
    /// </summary>
    private string _lastSeasonLogged = null;

    /// <summary>
    /// The phase shift, set according to "StartingSeason" in worldglobal.xml.
    /// </summary>
    private float _phaseShift = 0;

    /// <summary>
    /// Whether to alert players when the seasons start, set according to "SeasonStartAlerts" in
    /// worldglobal.xml.
    /// </summary>
    private bool _seasonStartAlerts = false;

    /// <summary>
    /// The temperature variation, set from "TemperatureVariation" in worldglobal.xml.
    /// </summary>
    private float _variation = -1;
    
    /// <summary>
    /// Whether the properties from worldglobal.xml are already loaded.
    /// </summary>
    private bool _worldEnvironmentPropertiesLoaded = false;
    
    // Note: This implements the IHarmony.Start method, but does not actually use Harmony.
    // It's being called by DMT at runtime. Though its stated purpose is to run the Harmony
    // patcher, it's being exploited to instead function like MonoBehaviour.Awake except without
    // the need to attach it to a scene.
    // Thanks to Alloc and SphereII for the suggestion!
    public void Start()
    {
        Debug.Log("Loading: " + GetType().ToString());

        ModEvents.GameUpdate.RegisterHandler(new Action(this.OnGameUpdate));

        Debug.Log("Seasonal weather manager registered");
    }

    public void OnGameUpdate()
    {
        if (!_enabled)
            return;

        if (WeatherManager.isTemperatureForceOn)
            return;
        
        // Unfortunately, there doesn't seem to be a way to ignore the grace period
        if (WeatherManager.inWeatherGracePeriod)
            return;
        
        if (!_worldEnvironmentPropertiesLoaded)
        {
            LoadWorldEnvironmentProperties();

            // Edge case: game can't load WorldEnvironment.Properties from worldglobal.xml
            if (!_worldEnvironmentPropertiesLoaded)
            {
                _enabled = false; 
                return;
            }

            if (_minutesPerMYear < 1)
            {
                _enabled = false; 
                return;
            }
        }

        // This should only be run on servers or single-player games
        if (!(GameManager.IsDedicatedServer ||
            SingletonMonoBehaviour<ConnectionManager>.Instance.IsServer))
        {
            _enabled = false;
            return;
        }

        SetGlobalTemperature();
    }

    private void LoadWorldEnvironmentProperties()
    {
        if (WorldEnvironment.Properties == null)
            return;
        
        _worldEnvironmentPropertiesLoaded = true;

        var daysPerMYear = WorldEnvironment.Properties.GetInt("DaysPerMeteorologicalYear");
        if (daysPerMYear < 1)  // Not enabled, don't bother with the other properties
            return;
        _minutesPerMYear = daysPerMYear * MinutesPerDay;

        var startingSeason = WorldEnvironment.Properties.GetString("StartingSeason");
        _phaseShift = GetPhaseShift(startingSeason);
    
        _variation = WorldEnvironment.Properties.GetFloat("TemperatureVariation");
        if (_variation <= 0)
            _variation = DefaultTemperatureVariation;
        
        _seasonStartAlerts = WorldEnvironment.Properties.GetBool("SeasonStartAlerts");
    }

    private void SetGlobalTemperature()
    {
        var worldTime = WeatherManager.worldTime;

        var angle = ToRadians(GetTotalMinutesSinceCycleStarted(worldTime));

        var offset = GetGlobalTemperatureOffset(angle);
        
        WeatherManager.SetGlobalTemperature(offset + MedianTemperature);

        LogSeasonStart(angle, worldTime);
    }

    private float GetTotalMinutesSinceCycleStarted(ulong worldTime)
    {
        // This is a check against TFP shortening the hard-coded grace period
        if (_gracePeriod > worldTime)
            _gracePeriod = worldTime;
        
        return (float)(worldTime - _gracePeriod) * MinutesPerWorldTime;
    }

    private float GetGlobalTemperatureOffset(float angle)
    {
        var ratio = Math.Sin(angle);

        return (float)(ratio * _variation / 2);
    }

    private float ToRadians(float totalMinutes)
    {
        // The arc length is the total number of minutes since the meteorological year started,
        // and the circumference is the minutes per meteorological year
        var radians = (float)(2 * Math.PI * totalMinutes) / _minutesPerMYear;

        return radians + _phaseShift;
    }

    private float GetPhaseShift(string startingSeason)
    {
        if (string.IsNullOrEmpty(startingSeason))
            return 0;
        if (startingSeason.ToLower() == "summer")
            return (float)Math.PI / 2.0F;
        if (startingSeason.ToLower() == "fall")
            return (float)Math.PI;
        if (startingSeason.ToLower() == "winter")
            return (float)Math.PI * 3.0F / 2.0F;
        // Default is "spring"
        return 0;
    }

    private void LogSeasonStart(float angle, ulong worldTime)
    {
        // Handle the user skipping back in time, e.g. for testing
        if (_lastSeasonCheckTime > worldTime)
            _lastSeasonCheckTime = worldTime - 1001;
        
        // Only check the season once per in-game hour
        if (worldTime - _lastSeasonCheckTime < 1000)
            return;
        
        var season = GetCurrentSeason(angle);
        if (season != _lastSeasonLogged)
        {
            // If we haven't checked before (game is starting or we're exiting grace period),
            // just notify the user of the season, don't say we're starting it
            var msg = (_lastSeasonCheckTime == 0) ?
                Localization.Get(ToLocalizationKey(season)) :
                string.Format(
                    Localization.Get("weatherSeasonStart"),
                    Localization.Get(ToLocalizationKey(season)));

            Log.Out(msg + ": global temperature = " + WeatherManager.globalTemperature);

            if (_seasonStartAlerts)
            {
                GameManager.Instance.ChatMessageServer(
                    null,
                    EChatType.Global,
                    -1,
                    msg,
                    ChatMainName,
                    true,
                    null);
            }

            _lastSeasonLogged = season;
        }

        _lastSeasonCheckTime = worldTime;
    }

    private static string ToLocalizationKey(string season)
    {
        if (season.ToLower() == "summer")
            return "weatherSummerSeason";
        if (season.ToLower() == "fall")
            return "weatherFallSeason";
        if (season.ToLower() == "winter")
            return "weatherWinterSeason";
        // Default is "spring"
        return "weatherSpringSeason";
    }

    private string GetCurrentSeason(float angle)
    {
        var referenceAngle = angle % (Math.PI * 2.0f);
        if (referenceAngle >= StartSummer && referenceAngle < StartFall)
            return "summer";
        if (referenceAngle >= StartFall && referenceAngle < StartWinter)
            return "fall";
        if (referenceAngle >= StartWinter && referenceAngle < StartSpring)
            return "winter";
        return "spring";
    }
}
