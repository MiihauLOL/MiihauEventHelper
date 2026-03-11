using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Locations;
using StardewValley.Triggers;
using System;
using System.Collections.Generic;

namespace MiihauEventHelper
{
    /// <summary>
    ///     This mod provides additional utilities for Content Patcher events.
    /// </summary>
    public class EventHelperMod : Mod
    {
        private readonly Dictionary<string, FireflyEffect> activeEffects = new(StringComparer.OrdinalIgnoreCase);
        private Event? lastEvent;

        public override void Entry(IModHelper helper)
        {
            TriggerActionManager.RegisterAction($"{this.ModManifest.UniqueID}_SpawnFirefly", this.SpawnFireflyLight);
            TriggerActionManager.RegisterAction($"{this.ModManifest.UniqueID}_RemoveFireFly", this.RemoveFireflyLight);

            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        }

        /// <summary>
        ///     Usage:
        ///     action {{ModId}}_SpawnFirefly <tileX> <tileY> [durationMs] [baseRadius] [pulseAmplitude] [pulseSpeed] [color] [movementSpeed] [fireflyId]
        ///
        ///     Example:
        ///     action Miihau.EventHelper_SpawnFirefly 6 7 5000 0.9 0.1 0.3 #FF4938 15 introGlow
        /// 
        ///     Then later:
        ///     action Miihau.EventHelper_RemoveFireFly introGlow
        /// </summary>
        private bool SpawnFireflyLight(string[] args, TriggerActionContext context, out string error)
        {
            error = null;

            try
            {
                if (args.Length < 3)
                {
                    error = "Not enough arguments. Usage: <tileX> <tileY> [durationMs] [baseRadius] [pulseAmplitude] [pulseSpeed] [color] [movementSpeed] [fireflyId]";
                    return false;
                }

                if (!int.TryParse(args[1], out int tileX) || !int.TryParse(args[2], out int tileY))
                {
                    error = "Invalid tile coordinates.";
                    return false;
                }

                int durationMs = 5000;
                if (args.Length >= 4 && !string.IsNullOrWhiteSpace(args[3]))
                    int.TryParse(args[3], out durationMs);

                float baseRadius = 0.9f;
                if (args.Length >= 5 && !string.IsNullOrWhiteSpace(args[4]))
                    float.TryParse(args[4], out baseRadius);

                float pulseAmplitude = 0.1f;
                if (args.Length >= 6 && !string.IsNullOrWhiteSpace(args[5]))
                    float.TryParse(args[5], out pulseAmplitude);

                float pulseSpeed = 0.3f;
                if (args.Length >= 7 && !string.IsNullOrWhiteSpace(args[6]))
                    float.TryParse(args[6], out pulseSpeed);

                Color color = Color.BlueViolet;
                if (args.Length >= 8 && !string.IsNullOrWhiteSpace(args[7]))
                {
                    try
                    {
                        color = this.ParseColor(args[7]);
                    }
                    catch (Exception ex)
                    {
                        this.Monitor.Log($"Could not parse color '{args[7]}': {ex.Message}. Using default color.", LogLevel.Warn);
                    }
                }

                float movementSpeed = 15f;
                if (args.Length >= 9 && !string.IsNullOrWhiteSpace(args[8]))
                    float.TryParse(args[8], out movementSpeed);

                string? fireflyId = null;
                if (args.Length >= 10 && !string.IsNullOrWhiteSpace(args[9]))
                    fireflyId = args[9].Trim();

                float xPix = tileX * Game1.tileSize + Game1.tileSize / 2f;
                float yPix = tileY * Game1.tileSize + Game1.tileSize / 2f;

                var effect = new FireflyEffect(
                    modUniqueId: this.ModManifest.UniqueID,
                    xPix: xPix,
                    yPix: yPix,
                    durationMs: durationMs,
                    baseRadius: baseRadius,
                    amplitude: pulseAmplitude,
                    pulseSpeed: pulseSpeed,
                    color: color,
                    movementSpeed: movementSpeed,
                    customId: fireflyId
                );

                string effectId = effect.Id;

                // if the same ID already exists, replace it cleanly
                if (this.activeEffects.TryGetValue(effectId, out var existing))
                {
                    existing.Remove();
                    this.activeEffects.Remove(effectId);
                }

                this.activeEffects[effectId] = effect;

                GameLocation loc = Game1.currentLocation;
                effect.Initialize(loc);

                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                this.Monitor.Log($"Error in SpawnFireflyLight: {ex}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        ///     Usage:
        ///     action {{ModId}}_RemoveFireFly <id>
        /// </summary>
        private bool RemoveFireflyLight(string[] args, TriggerActionContext context, out string error)
        {
            error = null;

            try
            {
                if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
                {
                    error = "Missing firefly ID. Usage: <id>";
                    return false;
                }

                string id = args[1].Trim();

                if (!this.activeEffects.TryGetValue(id, out var effect))
                {
                    error = $"No active firefly found with ID '{id}'.";
                    return false;
                }

                effect.Remove();
                this.activeEffects.Remove(id);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                this.Monitor.Log($"Error in RemoveFireflyLight: {ex}", LogLevel.Error);
                return false;
            }
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (this.lastEvent != null && Game1.CurrentEvent == null)
                this.RemoveAllEffects();

            this.lastEvent = Game1.CurrentEvent;

            List<string>? expiredIds = null;

            foreach (var pair in this.activeEffects)
            {
                if (!pair.Value.Update(Game1.currentGameTime))
                {
                    expiredIds ??= new List<string>();
                    expiredIds.Add(pair.Key);
                }
            }

            if (expiredIds != null)
            {
                foreach (string id in expiredIds)
                {
                    if (this.activeEffects.TryGetValue(id, out var effect))
                    {
                        effect.Remove();
                        this.activeEffects.Remove(id);
                    }
                }
            }
        }

        private void RemoveAllEffects()
        {
            foreach (var effect in this.activeEffects.Values)
                effect.Remove();

            this.activeEffects.Clear();
        }

        private Color ParseColor(string value)
        {
            string val = value.Trim();
            if (val.StartsWith("#"))
                val = val.Substring(1);

            if (val.Length == 6 && int.TryParse(val, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
            {
                byte r = (byte)((rgb >> 16) & 0xFF);
                byte g = (byte)((rgb >> 8) & 0xFF);
                byte b = (byte)(rgb & 0xFF);
                return new Color(r, g, b);
            }

            var prop = typeof(Color).GetProperty(
                val,
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.IgnoreCase
            );

            if (prop != null)
                return (Color)prop.GetValue(null);

            throw new ArgumentException("Unknown color format.");
        }
    }


}