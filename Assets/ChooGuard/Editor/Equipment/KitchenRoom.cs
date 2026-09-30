using System.Collections.Generic;
using UnityEngine;

namespace ChooGuard.Editor
{
    /// <summary>
    /// A food shop of the twin in its own frame: u runs along the back wall (0 … <see cref="Width"/>), v from the back wall out
    /// toward the open shop front (0 … <see cref="Depth"/>), heights are above the floor. The twin models each tenant as three walls
    /// and a glass front; this is derived from those wall meshes by <see cref="KitchenRooms"/> (never from the shop's
    /// customer point, which lies outside two of the rooms).
    /// </summary>
    public sealed class KitchenRoom
    {
        /// <summary>Something already standing in the room (a sales counter, a table, a chair): a rectangle in (u, v).</summary>
        public readonly struct Obstacle
        {
            public readonly string Name;
            public readonly Rect Area;
            public Obstacle(string name, Rect area) { Name = name; Area = area; }
        }

        public string ShopId = "", Label = "";
        /// <summary>World position (x, z) of the back wall's end at u = 0, and the world directions of u and v.</summary>
        public Vector2 Origin, U, V;
        public float Width, Depth, FloorY, CeilingHeight;
        public readonly List<Obstacle> Obstacles = new List<Obstacle>();
        /// <summary>Where a customer stands to be served (u, v), when that point lies inside the room.</summary>
        public Vector2? Customer;

        /// <summary>World (x, z) of the point at (<paramref name="u"/>, <paramref name="v"/>).</summary>
        public Vector2 Ground(float u, float v) => Origin + U * u + V * v;

        /// <summary>World position of (<paramref name="u"/>, <paramref name="v"/>) at <paramref name="height"/> above the floor.</summary>
        public Vector3 World(float u, float v, float height)
        {
            var g = Ground(u, v);
            return new Vector3(g.x, FloorY + height, g.y);
        }
    }
}
