using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class TowerGrid
    {
        /// <summary>
        /// Ground / lobby floor. G and "1st floor" are the same level (no separate unused G).
        /// Floors above are 1, 2, …; basements are -1, -2, ….
        /// </summary>
        public const int LobbyFloor = 0;
        public const int MaxElevatorSpan = 30;

        readonly Dictionary<Vector2Int, RoomInstance> _cells = new();
        readonly List<RoomInstance> _rooms = new();
        readonly Dictionary<Vector2Int, RoomInstance> _underStairs = new();
        readonly Dictionary<Vector2Int, RoomInstance> _underElevator = new();
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
            if (floor != LobbyFloor) return false;
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

        public bool CanExtendLobby(int newMinX, int newMaxX)
        {
            if (!HasLobby) return false;
            if (newMaxX < newMinX) return false;
            if (newMinX > MinX || newMaxX < MaxX) return false;
            if (newMinX == MinX && newMaxX == MaxX) return false;

            for (var x = newMinX; x <= newMaxX; x++)
            {
                var cell = new Vector2Int(x, LobbyFloor);
                if (!_cells.TryGetValue(cell, out var occupant)) continue;
                if (IsStairs(occupant) || IsElevator(occupant)) continue;
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

            // Preserve transit rooms that punch through the lobby.
            var transitOnLobby = new List<RoomInstance>();
            foreach (var room in _rooms)
            {
                if (IsStairs(room) || IsElevator(room))
                    transitOnLobby.Add(room);
            }

            foreach (var c in oldLobby.OccupiedCells())
            {
                if (_cells.TryGetValue(c, out var occ) &&
                    (IsStairs(occ) || IsElevator(occ))) continue;
                _cells.Remove(c);
            }

            _rooms.Remove(oldLobby);

            lobby = new RoomInstance(
                oldLobby.InstanceId,
                lobbyType,
                new Vector2Int(newMinX, LobbyFloor),
                new Vector2Int(newMaxX - newMinX + 1, 1));
            Register(lobby);

            // Re-assert transit rooms on top of the new lobby span where they already existed.
            foreach (var transit in transitOnLobby)
            {
                foreach (var c in transit.OccupiedCells())
                {
                    if (c.y != LobbyFloor) continue;
                    if (_cells.TryGetValue(c, out var under) &&
                        !IsStairs(under) &&
                        !IsElevator(under))
                    {
                        if (IsStairs(transit))
                            _underStairs[c] = under;
                        else
                            _underElevator[c] = under;
                    }
                    _cells[c] = transit;
                }
            }

            MinX = newMinX;
            MaxX = newMaxX;
            return true;
        }

        public bool CanPlace(RoomTypeSO type, Vector2Int origin)
        {
            if (type == null || type.isLobby || type.isScaffolding) return false;
            if (!HasLobby) return false;
            if (type.size.x <= 0 || type.size.y <= 0) return false;

            var footprint = BuildFootprint(origin, type.size);
            if (type.isStairs)
                return CanPlaceStairs(type, origin, footprint);
            if (type.isElevatorShaft)
                return CanPlaceElevator(type, footprint, null);

            foreach (var cell in footprint)
            {
                if (cell.y == LobbyFloor) return false;
                if (!IsFloorAllowed(type, cell.y)) return false;
                if (cell.x < MinX || cell.x > MaxX) return false;

                if (_cells.TryGetValue(cell, out var occupant))
                {
                    if (IsScaffolding(occupant))
                    {
                        // Rebuild over studs.
                    }
                    else if (IsStairs(occupant))
                    {
                        // Rooms may sit behind stairs (stairs stay the visible/path owner).
                        if (_underStairs.TryGetValue(cell, out var under) &&
                            under != null &&
                            !IsScaffolding(under))
                            return false;
                    }
                    else if (IsElevator(occupant))
                    {
                        // Rooms may sit behind elevators (elevators stay the visible owner).
                        if (_underElevator.TryGetValue(cell, out var under) &&
                            under != null &&
                            !IsScaffolding(under))
                            return false;
                    }
                    else
                    {
                        return false;
                    }
                }

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

            var footprint = BuildFootprint(origin, type.size);

            if (type.isStairs)
            {
                room = PlaceStairs(type, origin, footprint, clearedScaffolding);
                return room != null;
            }
            if (type.isElevatorShaft)
            {
                room = PlaceElevator(type, origin, footprint, clearedScaffolding, _nextId++);
                return room != null;
            }

            var seen = new HashSet<RoomInstance>();
            foreach (var cell in footprint)
            {
                if (!_cells.TryGetValue(cell, out var occupant) || !IsScaffolding(occupant))
                    continue;
                if (!seen.Add(occupant)) continue;
                RemoveRoom(occupant);
                clearedScaffolding.Add(occupant);
            }

            room = new RoomInstance(_nextId++, type, origin, type.size);
            RegisterBehindTransit(room, footprint);
            return true;
        }

        /// <summary>
        /// Claims empty cells for the room; where transit already owns a cell, bookmark the room
        /// as underlay so demolishing transit restores it and transit stays on top.
        /// </summary>
        void RegisterBehindTransit(RoomInstance room, HashSet<Vector2Int> footprint)
        {
            foreach (var cell in footprint)
            {
                if (_cells.TryGetValue(cell, out var occupant) && IsStairs(occupant))
                {
                    _underStairs[cell] = room;
                    continue;
                }
                if (_cells.TryGetValue(cell, out occupant) && IsElevator(occupant))
                {
                    _underElevator[cell] = room;
                    continue;
                }

                _cells[cell] = room;
            }

            if (!_rooms.Contains(room))
                _rooms.Add(room);
        }

        public bool TryDemolishAt(Vector2Int cell, out RoomInstance removed) =>
            TryDemolishAt(cell, out removed, out _, out _);

        public bool TryDemolishAt(
            Vector2Int cell,
            out RoomInstance removed,
            out List<RoomInstance> scaffoldsPlaced) =>
            TryDemolishAt(cell, out removed, out scaffoldsPlaced, out _);

        /// <summary>
        /// Removes a non-lobby room. Stairs restore whatever they covered (lobby/rooms).
        /// Other demolitions may leave scaffolding under floors that still need support.
        /// </summary>
        public bool TryDemolishAt(
            Vector2Int cell,
            out RoomInstance removed,
            out List<RoomInstance> scaffoldsPlaced,
            out List<RoomInstance> restoredUnderStairs)
        {
            removed = null;
            scaffoldsPlaced = new List<RoomInstance>();
            restoredUnderStairs = new List<RoomInstance>();
            if (!_cells.TryGetValue(cell, out var room)) return false;
            if (room.Type != null && room.Type.isLobby) return false;

            if (IsScaffolding(room) && IsLoadBearingCell(cell))
                return false;

            var vacated = new List<Vector2Int>(room.OccupiedCells());

            if (IsStairs(room) || IsElevator(room))
            {
                var underTransit = IsStairs(room) ? _underStairs : _underElevator;
                RemoveRoom(room);
                removed = room;
                var restoredSeen = new HashSet<RoomInstance>();
                foreach (var vacatedCell in vacated)
                {
                    // Stacked stairs may still cover this landing — keep the chain.
                    if (IsStairs(room))
                    {
                        var otherStairs = FindStairsCovering(vacatedCell);
                        if (otherStairs != null)
                        {
                            _cells[vacatedCell] = otherStairs;
                            continue;
                        }
                    }

                    if (underTransit.TryGetValue(vacatedCell, out var under))
                    {
                        underTransit.Remove(vacatedCell);
                        _cells[vacatedCell] = under;
                        if (restoredSeen.Add(under))
                            restoredUnderStairs.Add(under);
                        continue;
                    }

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

            // Clear under-stairs bookmarks that pointed at this room.
            var underKeys = new List<Vector2Int>();
            foreach (var pair in _underStairs)
            {
                if (ReferenceEquals(pair.Value, room))
                    underKeys.Add(pair.Key);
            }

            foreach (var key in underKeys)
                _underStairs.Remove(key);

            underKeys.Clear();
            foreach (var pair in _underElevator)
            {
                if (ReferenceEquals(pair.Value, room))
                    underKeys.Add(pair.Key);
            }

            foreach (var key in underKeys)
                _underElevator.Remove(key);

            RemoveRoom(room);
            removed = room;

            if (IsScaffolding(room))
                return true;

            foreach (var vacatedCell in vacated)
            {
                // Stairs may still own this cell (punch-through); leave it alone.
                if (_cells.ContainsKey(vacatedCell)) continue;
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

        bool CanPlaceStairs(RoomTypeSO type, Vector2Int origin, HashSet<Vector2Int> footprint)
        {
            foreach (var cell in footprint)
            {
                if (!IsFloorAllowed(type, cell.y)) return false;
                if (cell.x < MinX || cell.x > MaxX) return false;

                if (_cells.TryGetValue(cell, out var occupant))
                {
                    if (IsElevator(occupant)) return false;
                    if (IsStairs(occupant))
                    {
                        var existingRole = StairsCornerRole(occupant.Origin, occupant.Size, cell);
                        var newRole = StairsCornerRole(origin, type.size, cell);
                        if (StairsRolesConflict(existingRole, newRole))
                            return false;
                        continue;
                    }

                    // Lobby, rooms, and scaffolding may be overlapped.
                    continue;
                }

                if (FindElevatorCovering(cell) != null) return false;
                if (!HasSupportForStairs(cell, footprint)) return false;
            }

            // Check every stairs room (stacked segments may not be the _cells owner).
            foreach (var existing in _rooms)
            {
                if (IsElevator(existing))
                {
                    foreach (var cell in footprint)
                    {
                        if (RoomContains(existing, cell)) return false;
                    }
                    continue;
                }
                if (!IsStairs(existing)) continue;
                foreach (var cell in footprint)
                {
                    if (!StairsContains(existing, cell)) continue;
                    var existingRole = StairsCornerRole(existing.Origin, existing.Size, cell);
                    var newRole = StairsCornerRole(origin, type.size, cell);
                    if (StairsRolesConflict(existingRole, newRole))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Corner roles inside a stairs footprint:
        /// <code>
        /// [3,4] upper floor
        /// [1,2] lower floor
        /// </code>
        /// Stair run is 1 → 4 (bottom-left to top-right). Role 0 = non-corner.
        /// </summary>
        static int StairsCornerRole(Vector2Int origin, Vector2Int size, Vector2Int cell)
        {
            var dx = cell.x - origin.x;
            var dy = cell.y - origin.y;
            if (dx < 0 || dy < 0 || dx >= size.x || dy >= size.y) return 0;

            var bottom = dy == 0;
            var top = dy == size.y - 1;
            var left = dx == 0;
            var right = dx == size.x - 1;

            if (bottom && left) return 1;
            if (bottom && right) return 2;
            if (top && left) return 3;
            if (top && right) return 4;
            return 0;
        }

        static bool StairsRolesConflict(int a, int b)
        {
            if (a == 0 || b == 0) return false;
            return (a == 1 && b == 4) || (a == 4 && b == 1);
        }

        static bool StairsContains(RoomInstance stairs, Vector2Int cell)
        {
            return RoomContains(stairs, cell);
        }

        static bool RoomContains(RoomInstance room, Vector2Int cell)
        {
            foreach (var c in room.OccupiedCells())
            {
                if (c == cell) return true;
            }

            return false;
        }

        RoomInstance PlaceStairs(
            RoomTypeSO type,
            Vector2Int origin,
            HashSet<Vector2Int> footprint,
            List<RoomInstance> clearedScaffolding)
        {
            var seenScaffold = new HashSet<RoomInstance>();
            foreach (var cell in footprint)
            {
                if (!_cells.TryGetValue(cell, out var occupant)) continue;
                if (IsScaffolding(occupant))
                {
                    if (!seenScaffold.Add(occupant)) continue;
                    RemoveRoom(occupant);
                    clearedScaffolding.Add(occupant);
                    continue;
                }

                // Keep the original underlay when stacking over another stairs segment.
                if (IsStairs(occupant))
                    continue;

                _underStairs[cell] = occupant;
            }

            var stairs = new RoomInstance(_nextId++, type, origin, type.size);
            Register(stairs);
            return stairs;
        }

        RoomInstance FindStairsCovering(Vector2Int cell)
        {
            foreach (var room in _rooms)
            {
                if (!IsStairs(room)) continue;
                if (StairsContains(room, cell)) return room;
            }

            return null;
        }

        RoomInstance FindElevatorCovering(Vector2Int cell)
        {
            foreach (var room in _rooms)
            {
                if (!IsElevator(room)) continue;
                if (RoomContains(room, cell)) return room;
            }

            return null;
        }

        bool CanPlaceElevator(
            RoomTypeSO type,
            HashSet<Vector2Int> footprint,
            RoomInstance extendingShaft)
        {
            if (type.size.x != 1) return false;
            if (footprint.Count < 2 || footprint.Count > MaxElevatorSpan) return false;
            if (extendingShaft == null &&
                (type.size.y != 2 || footprint.Count != 2))
                return false;

            foreach (var cell in footprint)
            {
                if (!IsFloorAllowed(type, cell.y)) return false;
                if (cell.x < MinX || cell.x > MaxX) return false;
                if (FindStairsCovering(cell) != null) return false;

                var coveringElevator = FindElevatorCovering(cell);
                if (coveringElevator != null &&
                    !ReferenceEquals(coveringElevator, extendingShaft))
                    return false;

                if (_cells.TryGetValue(cell, out var occupant))
                {
                    if (IsStairs(occupant)) return false;
                    if (IsElevator(occupant) &&
                        !ReferenceEquals(occupant, extendingShaft))
                        return false;

                    // Lobby, rooms, scaffolding, and the extending shaft may be overlapped.
                    continue;
                }

                if (!HasSupportForStairs(cell, footprint)) return false;
            }

            return true;
        }

        RoomInstance PlaceElevator(
            RoomTypeSO type,
            Vector2Int origin,
            HashSet<Vector2Int> footprint,
            List<RoomInstance> clearedScaffolding,
            int instanceId)
        {
            var seenScaffold = new HashSet<RoomInstance>();
            foreach (var cell in footprint)
            {
                if (!_cells.TryGetValue(cell, out var occupant)) continue;
                if (IsScaffolding(occupant))
                {
                    if (!seenScaffold.Add(occupant)) continue;
                    RemoveRoom(occupant);
                    clearedScaffolding.Add(occupant);
                    continue;
                }

                if (IsElevator(occupant)) continue;
                _underElevator[cell] = occupant;
            }

            var elevator = new RoomInstance(
                instanceId,
                type,
                origin,
                new Vector2Int(type.size.x, footprint.Count / type.size.x));
            Register(elevator);
            return elevator;
        }

        public bool CanExtendElevator(RoomInstance shaft, int newMinY, int newMaxY)
        {
            if (!IsElevator(shaft)) return false;
            if (!_rooms.Contains(shaft)) return false;
            var span = newMaxY - newMinY + 1;
            if (span < 2 || span > MaxElevatorSpan) return false;
            if (newMinY > shaft.Origin.y ||
                newMaxY < shaft.Origin.y + shaft.Size.y - 1) return false;
            if (newMinY == shaft.Origin.y &&
                newMaxY == shaft.Origin.y + shaft.Size.y - 1) return false;

            var footprint = BuildFootprint(
                new Vector2Int(shaft.Origin.x, newMinY),
                new Vector2Int(1, span));
            return CanPlaceElevator(shaft.Type, footprint, shaft);
        }

        public bool TryExtendElevator(
            RoomInstance shaft,
            int newMinY,
            int newMaxY,
            out int addedCells)
        {
            addedCells = 0;
            if (!CanExtendElevator(shaft, newMinY, newMaxY)) return false;

            var oldCells = new List<Vector2Int>(shaft.OccupiedCells());
            RemoveRoom(shaft);
            foreach (var cell in oldCells)
            {
                if (!_underElevator.TryGetValue(cell, out var under)) continue;
                _underElevator.Remove(cell);
                _cells[cell] = under;
            }

            var span = newMaxY - newMinY + 1;
            var origin = new Vector2Int(shaft.Origin.x, newMinY);
            var footprint = BuildFootprint(origin, new Vector2Int(1, span));
            PlaceElevator(shaft.Type, origin, footprint, new List<RoomInstance>(), shaft.InstanceId);
            addedCells = span - shaft.Size.y;
            return true;
        }

        bool HasSupportForStairs(Vector2Int cell, HashSet<Vector2Int> footprint)
        {
            if (cell.y == LobbyFloor)
                return HasLobby;

            if (cell.y > LobbyFloor)
            {
                var below = new Vector2Int(cell.x, cell.y - 1);
                return footprint.Contains(below) || _cells.ContainsKey(below);
            }

            // Basement: support from the level toward the surface (y + 1), including lobby at 0.
            var support = new Vector2Int(cell.x, cell.y + 1);
            return footprint.Contains(support) || _cells.ContainsKey(support);
        }

        bool HasSupportFromAdjacentLevel(Vector2Int cell, HashSet<Vector2Int> footprint)
        {
            if (cell.y > LobbyFloor)
            {
                var below = new Vector2Int(cell.x, cell.y - 1);
                return footprint.Contains(below) || _cells.ContainsKey(below);
            }

            if (cell.y == LobbyFloor)
                return false;

            var support = new Vector2Int(cell.x, cell.y + 1);
            return footprint.Contains(support) || _cells.ContainsKey(support);
        }

        bool NeedsStructuralFill(Vector2Int cell) => IsLoadBearingCell(cell);

        bool IsLoadBearingCell(Vector2Int cell)
        {
            if (cell.y > LobbyFloor)
                return _cells.ContainsKey(new Vector2Int(cell.x, cell.y + 1));

            if (cell.y < LobbyFloor)
                return _cells.ContainsKey(new Vector2Int(cell.x, cell.y - 1));

            return false;
        }

        static HashSet<Vector2Int> BuildFootprint(Vector2Int origin, Vector2Int size)
        {
            var footprint = new HashSet<Vector2Int>();
            for (var dy = 0; dy < size.y; dy++)
            for (var dx = 0; dx < size.x; dx++)
                footprint.Add(new Vector2Int(origin.x + dx, origin.y + dy));
            return footprint;
        }

        static bool IsScaffolding(RoomInstance room) =>
            room?.Type != null && room.Type.isScaffolding;

        static bool IsStairs(RoomInstance room) =>
            room?.Type != null && room.Type.isStairs;

        static bool IsElevator(RoomInstance room) =>
            room?.Type != null && room.Type.isElevatorShaft;

        static bool IsFloorAllowed(RoomTypeSO type, int floor)
        {
            if (floor == LobbyFloor)
                return (type.isStairs || type.isElevatorShaft) && type.allowAboveGround;
            if (floor > LobbyFloor) return type.allowAboveGround;
            if (floor < LobbyFloor) return type.allowBasement;
            return false;
        }

        void Register(RoomInstance room)
        {
            foreach (var c in room.OccupiedCells())
                _cells[c] = room;
            if (!_rooms.Contains(room))
                _rooms.Add(room);
        }

        void RemoveRoom(RoomInstance room)
        {
            foreach (var c in room.OccupiedCells())
            {
                if (_cells.TryGetValue(c, out var occ) && ReferenceEquals(occ, room))
                    _cells.Remove(c);
            }

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
            so.placeholderColor = new Color(0.76f, 0.62f, 0.40f, 1f);
            return so;
        }
    }
}
