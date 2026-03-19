using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
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
        private readonly List<CustomEmoteBubble> activeCustomEmotes = new();
        private Event? lastEvent;
        private Texture2D? bubbleTexture;

        public override void Entry(IModHelper helper)
        {
            TriggerActionManager.RegisterAction($"{this.ModManifest.UniqueID}_SpawnFirefly", this.SpawnFireflyLight);
            TriggerActionManager.RegisterAction($"{this.ModManifest.UniqueID}_RemoveFireFly", this.RemoveFireflyLight);
            TriggerActionManager.RegisterAction($"{this.ModManifest.UniqueID}_CustomEmote", this.ShowCustomEmote);

            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            helper.Events.Display.RenderedWorld += this.OnRenderedWorld;
        }

        /// <summary>
        ///     Usage:
        ///     action {{ModId}}_SpawnFirefly <tileX> <tileY> [durationMs] [baseRadius] [pulseAmplitude] [pulseSpeed] [color] [movementSpeed] [fadeInDurationMs] [fireflyId]
        ///
        ///     Example:
        ///     action MiihauEventHelper_SpawnFirefly 6 7 5000 0.9 0.1 0.3 #FF4938 15 3000 introGlow
        /// 
        ///     Then later:
        ///     action MiihauEventHelper_RemoveFireFly introGlow
        /// </summary>
        private bool SpawnFireflyLight(string[] args, TriggerActionContext context, out string error)
        {
            error = null;

            try
            {
                if (args.Length < 3)
                {
                    error = "Not enough arguments. Usage: <tileX> <tileY> [durationMs] [baseRadius] [pulseAmplitude] [pulseSpeed] [color] [movementSpeed] [fadeInDurationMs] [fireflyId]";
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

                int fadeInDurationMs = 0;
                string? fireflyId = null;

                if (args.Length >= 10 && !string.IsNullOrWhiteSpace(args[9]))
                {
                    // old format: arg 9 was fireflyId
                    // new format: arg 9 can be fadeInDurationMs
                    if (!int.TryParse(args[9], out fadeInDurationMs))
                        fireflyId = args[9].Trim();
                }

                if (args.Length >= 11 && !string.IsNullOrWhiteSpace(args[10]))
                    fireflyId = args[10].Trim();

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
                    fadeInDurationMs: fadeInDurationMs,
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
        ///     action {{ModId}}_CustomEmote <actor> <PathToSpriteFile> <XposInSprite> <YposInSprite> <width> <height>
        ///
        ///     Example:
        ///     action MiihauEventHelper_CustomEmote Abigail LooseSprites\Cursors 0 0 10 9
        /// </summary>
        private bool ShowCustomEmote(string[] args, TriggerActionContext context, out string error)
        {
            error = null;

            try
            {
                if (args.Length < 7)
                {
                    error = "Not enough arguments. Usage: <actor> <PathToSpriteFile> <XposInSprite> <YposInSprite> <width> <height>";
                    return false;
                }

                string actorName = args[1].Trim();
                string assetName = this.NormalizeAssetName(args[2]);

                if (!int.TryParse(args[3], out int sourceX)
                    || !int.TryParse(args[4], out int sourceY)
                    || !int.TryParse(args[5], out int sourceWidth)
                    || !int.TryParse(args[6], out int sourceHeight))
                {
                    error = "Invalid source rectangle. X, Y, width, and height must all be integers.";
                    return false;
                }

                if (sourceWidth <= 0 || sourceHeight <= 0)
                {
                    error = "Width and height must be greater than zero.";
                    return false;
                }

                Character? actor = this.ResolveActor(actorName);
                if (actor == null)
                {
                    error = $"Couldn't find an actor named '{actorName}'.";
                    return false;
                }

                Texture2D iconTexture = Game1.content.Load<Texture2D>(assetName);
                Rectangle sourceRect = new Rectangle(sourceX, sourceY, sourceWidth, sourceHeight);

                if (sourceRect.Right > iconTexture.Width || sourceRect.Bottom > iconTexture.Height)
                {
                    error = $"The source rectangle {sourceRect} is outside the texture bounds {iconTexture.Width}x{iconTexture.Height}.";
                    return false;
                }

                int emoteYOffset = 0;
                if (args.Length >= 8 && !string.IsNullOrWhiteSpace(args[7]))
                    int.TryParse(args[7], out emoteYOffset);

                this.activeCustomEmotes.Add(new CustomEmoteBubble(actor, this.GetBubbleTexture(), iconTexture, sourceRect, emoteYOffset));
                return true;
            }
            catch (Exception ex)
            {
                error = ex.ToString();
                this.Monitor.Log($"Error in ShowCustomEmote: {ex}", LogLevel.Error);
                return false;
            }
        }

        /// <summary>
        ///     Usage:
        ///     action {{ModId}}_RemoveFireFly <id> [fadeOutDurationMs]
        /// </summary>
        private bool RemoveFireflyLight(string[] args, TriggerActionContext context, out string error)
        {
            error = null;

            try
            {
                if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
                {
                    error = "Missing firefly ID. Usage: <id> [fadeOutDurationMs]";
                    return false;
                }

                string id = args[1].Trim();

                if (!this.activeEffects.TryGetValue(id, out var effect))
                {
                    error = $"No active firefly found with ID '{id}'.";
                    return false;
                }

                int fadeOutDurationMs = 0;
                if (args.Length >= 3 && !string.IsNullOrWhiteSpace(args[2]))
                    int.TryParse(args[2], out fadeOutDurationMs);

                if (fadeOutDurationMs > 0)
                {
                    effect.Remove(fadeOutDurationMs);
                }
                else
                {
                    effect.Remove();
                    this.activeEffects.Remove(id);
                }

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
            {
                this.RemoveAllEffects();
                this.activeCustomEmotes.Clear();
            }

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

            if (this.activeCustomEmotes.Count > 0)
            {
                for (int i = this.activeCustomEmotes.Count - 1; i >= 0; i--)
                {
                    if (!this.activeCustomEmotes[i].Update(Game1.currentGameTime))
                        this.activeCustomEmotes.RemoveAt(i);
                }
            }
        }

        private void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (!Context.IsWorldReady || this.activeCustomEmotes.Count == 0)
                return;

            foreach (CustomEmoteBubble bubble in this.activeCustomEmotes)
                bubble.Draw(Game1.spriteBatch);
        }

        private void RemoveAllEffects()
        {
            foreach (var effect in this.activeEffects.Values)
                effect.Remove();

            this.activeEffects.Clear();
        }

        private Texture2D GetBubbleTexture()
        {
            this.bubbleTexture ??= this.Helper.ModContent.Load<Texture2D>("assets/Bubbles.png");
            return this.bubbleTexture;
        }

        private Character? ResolveActor(string actorName)
        {
            if (string.IsNullOrWhiteSpace(actorName))
                return null;

            if (string.Equals(actorName, "farmer", StringComparison.OrdinalIgnoreCase)
                || string.Equals(actorName, Game1.player.Name, StringComparison.OrdinalIgnoreCase)
                || string.Equals(actorName, Game1.player.displayName, StringComparison.OrdinalIgnoreCase))
            {
                return Game1.player;
            }

            if (Game1.CurrentEvent?.actors != null)
            {
                foreach (NPC npc in Game1.CurrentEvent.actors)
                {
                    if (this.IsMatchingActorName(npc, actorName))
                        return npc;
                }
            }

            if (Game1.currentLocation?.characters != null)
            {
                foreach (NPC npc in Game1.currentLocation.characters)
                {
                    if (this.IsMatchingActorName(npc, actorName))
                        return npc;
                }
            }

            NPC? globalNpc = Game1.getCharacterFromName(actorName, false);
            if (globalNpc != null)
                return globalNpc;

            return null;
        }

        private bool IsMatchingActorName(NPC npc, string actorName)
        {
            return string.Equals(npc.Name, actorName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(npc.displayName, actorName, StringComparison.OrdinalIgnoreCase);
        }

        private string NormalizeAssetName(string assetName)
        {
            string normalized = assetName.Trim();

            if (normalized.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(0, normalized.Length - 4);

            return normalized.Replace('/', '\\');
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
