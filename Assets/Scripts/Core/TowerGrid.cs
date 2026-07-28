using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class TowerGrid
    {
        readonly Dictionary<Vector2Int, RoomInstance> _cells = new();
        readonly List<RoomInstance> _rooms = new();
        readonly RoomTypeSO _scaffoldingType;
        int _nextId = 1;

        public bool HasLobby { get; private set; }
        public int MinX { get; private set; }
        public int MaxX { get; private set; }
        public IReadOnlyList<RoomInstance> Rooms => _rooms;

        public TowerGrid()
        {
            _scaffoldingType = CreateScaffoldingType();
        }

        public bool TryGetRoomAt(Vector2Int cell, out RoomInstance room) =>
            _cells.TryGetValue(cell, out room);

        public bool CanPlaceLobby(int minX, int maxX, int floor)
        {
            if (HasLobby) return false;
            if (floor != 1) return false;
            if (maxX < minX) return false;
            for (var x = minX; x <= maxX; x++)
            {
                var cell = new Vector2Int(x, floor);
                if (_cells.ContainsKey(cell)) return false;
            }
            return true;
        }

        public bool TryPlaceLobby(RoomTypeSO lobbyType, int minX, int maxX, int floor, out RoomInstance room)
        {
            room = null;
            if (lobbyType == null || !lobbyType.isLobby) return false;
            if (!CanPlaceLobby(minX, maxX, floor)) return false;

            var width = maxX - minX + 1;
            room = new RoomInstance(_nextId++, lobbyType, new Vector2Int(minX, floor), new Vector2Int(width, 1));
            Register(room);
            HasLobby = true;
            MinX = minX;
            MaxX = maxX;
            return true;
        }

        /// <summary>
        /// Expand an existing floor-1 lobby left and/or right. newMin/newMax must fully
        /// contain the current lobby. Returns how many new cells were added.
        /// </summary>
        public bool CanExtendLobby(int newMinX, int newMaxX)
        {
            if (!HasLobby) return false;
            if (newMaxX < newMinX) return false;
            if (newMinX > MinX || newMaxX < MaxX) return false;
            if (newMinX == MinX && newMaxX == MaxX) return false;

            for (var x = newMinX; x <= newMaxX; x++)
            {
                var cell = new Vector2Int(x, 1);
                if (!_cells.TryGetValue(cell, out var occupant)) continue;
                if (occupant.Type == null || !occupant.Type.isLobby) return false;
            }

            return true;
        }

        public bool TryExtendLobby(
            RoomTypeSO lobbyType,
            int newMinX,
            int newMaxX,
            out RoomInstance lobby,
            out int addedCells)
        {
            lobby = null;
            addedCells = 0;
            if (lobbyType == null || !lobbyType.isLobby) return false;
            if (!CanExtendLobby(newMinX, newMaxX)) return false;

            RoomInstance oldLobby = null;
            foreach (var room in _rooms)
            {
                if (room.Type != null && room.Type.isLobby)
                {
                    oldLobby = room;
                    break;
                }
            }

            if (oldLobby == null) return false;

            addedCells = (newMaxX - newMinX + 1) - oldLobby.Size.x;
            if (addedCells <= 0) return false;

            foreach (var c in oldLobby.OccupiedCells())
                _cells.Remove(c);
            _rooms.Remove(oldLobby);

            lobby = new RoomInstance(
                oldLobby.InstanceId,
                lobbyType,
                new Vector2Int(newMinX, 1),
                new Vector2Int(newMaxX - newMinX + 1, 1));
            Register(lobby);
            MinX = newMinX;
            MaxX = newMaxX;
            return true;
        }

        public bool CanPlace(RoomTypeSO type, Vector2Int origin)
        {
            if (type == null || type.isLobby || type.isScaffolding) return false;
            if (!HasLobby) return false;
            if (type.size.x <= 0 || type.size.y <= 0) return false;

            // Collect footprint first so multi-floor rooms can support themselves.
            var footprint = new HashSet<Vector2Int>();
            for (var dy = 0; dy < type.size.y; dy++)
            for (var dx = 0; dx < type.size.x; dx++)
                footprint.Add(new Vector2Int(origin.x + dx, origin.y + dy));

            foreach (var cell in footprint)
            {
                if (cell.y == 0) return false;
                if (!IsFloorAllowed(type, cell.y)) return false;
                if (cell.x < MinX || cell.x > MaxX) return false;
                if (_cells.TryGetValue(cell, out var occupant) && !IsScaffolding(occupant))
                    return false;
                if (!HasSupportFromAdjacentLevel(cell, footprint)) return false;
            }

            return true;
        }

        public bool TryPlace(RoomTypeSO type, Vector2Int origin, out RoomInstance room) =>
            TryPlace(type, origin, out room, out _);

        public bool TryPlace(
            RoomTypeSO type,
            Vector2Int origin,
            out RoomInstance room,
            out List<RoomInstance> clearedScaffolding)
        {
            room = null;
            clearedScaffolding = new List<RoomInstance>();
            if (!CanPlace(type, origin)) return false;

            // Rebuilding over studs/scaffolding consumes those cells.
            var seen = new HashSet<RoomInstance>();
            for (var dy = 0; dy < type.size.y; dy++)
            for (var dx = 0; dx < type.size.x; dx++)
            {
                var cell = new Vector2Int(origin.x + dx, origin.y + dy);
                if (!_cells.TryGetValue(cell, out var occupant) || !IsScaffolding(occupant))
                    continue;
                if (!seen.Add(occupant)) continue;
                RemoveRoom(occupant);
                clearedScaffolding.Add(occupant);
            }

            room = new RoomInstance(_nextId++, type, origin, type.size);
            Register(room);
            return true;
        }

        public bool TryDemolishAt(Vector2Int cell, out RoomInstance removed) =>
            TryDemolishAt(cell, out removed, out _);

        /// <summary>
        /// Removes a non-lobby room. Cells that still support floors above (or basement
        /// below) are filled with scaffolding/studs so the tower does not float.
        /// Load-bearing scaffolding cannot be cleared until nothing depends on it.
        /// </summary>
        public bool TryDemolishAt(
            Vector2Int cell,
            out RoomInstance removed,
            out List<RoomInstance> scaffoldsPlaced)
        {
            removed = null;
            scaffoldsPlaced = new List<RoomInstance>();
            if (!_cells.TryGetValue(cell, out var room)) return false;
            if (room.Type != null && room.Type.isLobby) return false;

            if (IsScaffolding(room) && IsLoadBearingCell(cell))
                return false;

            var vacated = new List<Vector2Int>(room.OccupiedCells());
            RemoveRoom(room);
            removed = room;

            if (IsScaffolding(room))
                return true;

            foreach (var vacatedCell in vacated)
            {
                if (!NeedsStructuralFill(vacatedCell)) continue;
                var scaffold = new RoomInstance(
                    _nextId++,
                    _scaffoldingType,
                    vacatedCell,
                    Vector2Int.one);
                Register(scaffold);
                scaffoldsPlaced.Add(scaffold);
            }

            return true;
        }

        /// <summary>
        /// A cell may only be built where the level toward the ground already exists:
        /// above-ground floors need the cell directly below; basements need the cell
        /// toward the surface (skipping unused floor 0, so B1 is supported by lobby).
        /// </summary>
        bool HasSupportFromAdjacentLevel(Vector2Int cell, HashSet<Vector2Int> footprint)
        {
            if (cell.y > 1)
            {
                var below = new Vector2Int(cell.x, cell.y - 1);
                return footprint.Contains(below) || _cells.ContainsKey(below);
            }

            if (cell.y == 1)
                return false; // tenant rooms never place on floor 1; lobby APIs only

            if (cell.y < 0)
            {
                var supportY = cell.y + 1;
                if (supportY == 0) supportY = 1;
                var support = new Vector2Int(cell.x, supportY);
                return footprint.Contains(support) || _cells.ContainsKey(support);
            }

            return false;
        }

        /// <summary>
        /// True when something still depends on this cell as structural support.
        /// </summary>
        bool NeedsStructuralFill(Vector2Int cell) => IsLoadBearingCell(cell);

        bool IsLoadBearingCell(Vector2Int cell)
        {
            if (cell.y >= 1)
            {
                // Floors above rest on the cell directly below.
                var aboveY = cell.y + 1;
                return _cells.ContainsKey(new Vector2Int(cell.x, aboveY));
            }

            if (cell.y < 0)
            {
                // Deeper basement rooms rest on the cell toward the surface.
                var below = new Vector2Int(cell.x, cell.y - 1);
                return _cells.ContainsKey(below);
            }

            return false;
        }

        static bool IsScaffolding(RoomInstance room) =>
            room?.Type != null && room.Type.isScaffolding;

        static bool IsFloorAllowed(RoomTypeSO type, int floor)
        {
            if (floor > 0) return type.allowAboveGround;
            if (floor < 0) return type.allowBasement;
            return false;
        }

        void Register(RoomInstance room)
        {
            foreach (var c in room.OccupiedCells())
                _cells[c] = room;
            _rooms.Add(room);
        }

        void RemoveRoom(RoomInstance room)
        {
            foreach (var c in room.OccupiedCells())
                _cells.Remove(c);
            _rooms.Remove(room);
        }

        static RoomTypeSO CreateScaffoldingType()
        {
            var so = ScriptableObject.CreateInstance<RoomTypeSO>();
            so.id = "scaffolding";
            so.displayName = "Scaffolding";
            so.category = RoomCategory.Structure;
            so.size = Vector2Int.one;
            so.buildCost = 0;
            so.isScaffolding = true;
            so.allowAboveGround = true;
            so.allowBasement = true;
            // Wood-stud / framing placeholder — readable against sky and room colors.
            so.placeholderColor = new Color(0.76f, 0.62f, 0.40f, 1f);
            return so;
        }
    }
}
