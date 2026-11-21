using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;

namespace MiihauEventHelper
{
    internal enum MapCheckType
    {
        Warp,
        Action,
        TouchAction,
        TileProperty
    }

    internal sealed class MapCheckToken
    {
        private readonly MapCheckType type;
        private readonly Dictionary<string, bool> results = new(StringComparer.OrdinalIgnoreCase);
        private bool ready;

        public MapCheckToken(MapCheckType type)
        {
            this.type = type;
        }

        public bool AllowsInput() => true;
        public bool RequiresInput() => true;
        public bool CanHaveMultipleValues(string? input = null) => false;
        public bool IsReady() => this.ready;

        public bool UpdateContext()
        {
            bool wasReady = this.ready;
            this.ready = Context.IsWorldReady;

            if (!this.ready)
            {
                this.results.Clear();
                return wasReady;
            }

            bool changed = !wasReady;
            foreach (string input in new List<string>(this.results.Keys))
            {
                if (!this.TryParse(input, out MapCheckQuery query, out _))
                    continue;

                bool current = this.Check(query);
                if (current != this.results[input])
                {
                    this.results[input] = current;
                    changed = true;
                }
            }

            return changed;
        }

        public bool TryValidateInput(string? input, out string? error)
        {
            return this.TryParse(input, out _, out error);
        }

        public IEnumerable<string> GetValues(string? input)
        {
            if (!this.ready || !this.TryParse(input, out MapCheckQuery query, out _))
                return Array.Empty<string>();

            if (!this.results.TryGetValue(input!, out bool found))
            {
                found = this.Check(query);
                this.results[input!] = found;
            }

            return new[] { found ? "true" : "false" };
        }

        private bool Check(MapCheckQuery query)
        {
            GameLocation? location = Game1.getLocationFromName(query.Location);
            if (location == null)
                return false;

            if (this.type == MapCheckType.Warp)
            {
                foreach (Warp warp in location.warps)
                {
                    if (query.Name != null && !string.Equals(warp.TargetName, query.Name, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (query.HasCoordinates && (Math.Abs(warp.X - query.X) > query.Radius || Math.Abs(warp.Y - query.Y) > query.Radius))
                        continue;

                    return true;
                }

                return false;
            }

            if (location.Map == null)
                return false;

            string propertyName = this.type switch
            {
                MapCheckType.Action => "Action",
                MapCheckType.TouchAction => "TouchAction",
                _ => query.Name!
            };

            foreach (var layer in location.Map.Layers)
            {
                if (query.Layer != null && !string.Equals(layer.Id, query.Layer, StringComparison.OrdinalIgnoreCase))
                    continue;

                int startX = query.HasCoordinates ? Math.Max(0, query.X - query.Radius) : 0;
                int startY = query.HasCoordinates ? Math.Max(0, query.Y - query.Radius) : 0;
                int endX = query.HasCoordinates ? Math.Min(layer.LayerWidth - 1, query.X + query.Radius) : layer.LayerWidth - 1;
                int endY = query.HasCoordinates ? Math.Min(layer.LayerHeight - 1, query.Y + query.Radius) : layer.LayerHeight - 1;

                for (int x = startX; x <= endX; x++)
                {
                    for (int y = startY; y <= endY; y++)
                    {
                        if (layer.Tiles[x, y] == null)
                            continue;

                        string? value = location.doesTileHaveProperty(x, y, propertyName, layer.Id);
                        if (value == null)
                            continue;

                        if (query.Value != null && !string.Equals(value.Trim(), query.Value, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (this.type == MapCheckType.TileProperty || query.Name == null)
                            return true;

                        string[] actionParts = value.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                        if (actionParts.Length > 0 && string.Equals(actionParts[0], query.Name, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }

            return false;
        }

        private bool TryParse(string? input, out MapCheckQuery query, out string? error)
        {
            query = new MapCheckQuery();
            error = null;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "A location name is required.";
                return false;
            }

            string[] options = input.Split('|');
            string[] parts = options[0].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length == 0 || Array.Exists(parts, string.IsNullOrWhiteSpace))
            {
                error = "Arguments cannot be blank.";
                return false;
            }

            query.Location = parts[0];

            if (this.type == MapCheckType.TileProperty)
            {
                if (parts.Length < 3)
                {
                    error = "Usage: Location, Layer, [X, Y,] PropertyName [, PropertyValue].";
                    return false;
                }

                query.Layer = parts[1];
                int index = 2;
                if (int.TryParse(parts[index], out _))
                {
                    if (!TryReadCoordinates(parts, index, query, out error))
                        return false;
                    index += 2;
                }

                if (index >= parts.Length || parts.Length - index > 2)
                {
                    error = "Expected a property name and optional value.";
                    return false;
                }

                query.Name = parts[index++];
                if (index < parts.Length)
                    query.Value = parts[index];
            }
            else
            {
                int index = 1;
                if (index < parts.Length && int.TryParse(parts[index], out _))
                {
                    if (!TryReadCoordinates(parts, index, query, out error))
                        return false;
                    index += 2;

                    if (index < parts.Length && !int.TryParse(parts[index], out _))
                        query.Name = parts[index++];
                }
                else
                {
                    if (index < parts.Length)
                        query.Name = parts[index++];

                    if (index < parts.Length)
                    {
                        if (!TryReadCoordinates(parts, index, query, out error))
                            return false;
                        index += 2;
                    }
                }

                if (index < parts.Length)
                {
                    if (!int.TryParse(parts[index++], out query.Radius) || query.Radius < 0)
                    {
                        error = "Radius must be a non-negative integer.";
                        return false;
                    }
                }

                if (index != parts.Length)
                {
                    error = "Too many arguments.";
                    return false;
                }
            }

            foreach (string option in options[1..])
            {
                int separator = option.IndexOf('=');
                if (separator < 1)
                {
                    error = "Options must use name=value.";
                    return false;
                }

                string key = option[..separator].Trim();
                string value = option[(separator + 1)..].Trim();
                if (value.Length == 0)
                {
                    error = $"Missing value for '{key}'.";
                    return false;
                }

                if (string.Equals(key, "radius", StringComparison.OrdinalIgnoreCase))
                {
                    if (!int.TryParse(value, out query.Radius) || query.Radius < 0)
                    {
                        error = "Radius must be a non-negative integer.";
                        return false;
                    }
                }
                else if (string.Equals(key, "value", StringComparison.OrdinalIgnoreCase) && this.type != MapCheckType.Warp)
                {
                    query.Value = value;
                }
                else if (string.Equals(key, "layer", StringComparison.OrdinalIgnoreCase)
                    && (this.type == MapCheckType.Action || this.type == MapCheckType.TouchAction))
                {
                    query.Layer = value;
                }
                else
                {
                    error = $"Unknown option '{key}'.";
                    return false;
                }
            }

            if (query.Radius > 0 && !query.HasCoordinates)
            {
                error = "Radius can only be used with tile coordinates.";
                return false;
            }

            return true;
        }

        private static bool TryReadCoordinates(string[] parts, int index, MapCheckQuery query, out string? error)
        {
            error = null;
            if (index + 1 >= parts.Length
                || !int.TryParse(parts[index], out query.X)
                || !int.TryParse(parts[index + 1], out query.Y))
            {
                error = "Expected X and Y tile coordinates.";
                return false;
            }

            query.HasCoordinates = true;
            return true;
        }

        private sealed class MapCheckQuery
        {
            public string Location = "";
            public string? Layer;
            public string? Name;
            public string? Value;
            public bool HasCoordinates;
            public int X;
            public int Y;
            public int Radius;
        }
    }
}
