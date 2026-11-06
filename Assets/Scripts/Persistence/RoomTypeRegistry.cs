using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuildATower
{
    public sealed class RoomTypeRegistry
    {
        readonly Dictionary<string, RoomTypeSO> _types =
            new Dictionary<string, RoomTypeSO>(StringComparer.Ordinal);

        RoomTypeRegistry()
        {
        }

        public static RoomTypeRegistry Create(BuildController build, TowerGrid grid)
        {
            var registry = new RoomTypeRegistry();
            registry.RegisterRange(Resources.LoadAll<RoomTypeSO>("Rooms"));
            if (build != null)
                registry.RegisterRange(build.CatalogRoomTypes);
            registry.Register(build != null ? build.LobbyType : null);
            registry.Register(build != null ? build.SkyLobbyType : null);
            registry.Register(grid != null ? grid.ScaffoldingType : null);
            registry.RegisterRuntimeElevators();
            return registry;
        }

        public static RoomTypeRegistry CreateForTests(IEnumerable<RoomTypeSO> types)
        {
            if (types == null)
                throw new ArgumentNullException(nameof(types));

            var registry = new RoomTypeRegistry();
            registry.RegisterRange(types);
            return registry;
        }

        public bool TryResolve(string id, out RoomTypeSO type)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                type = null;
                return false;
            }

            return _types.TryGetValue(id, out type);
        }

        void RegisterRuntimeElevators()
        {
            if (!_types.ContainsKey("elevator_normal"))
                Register(RoomTypeSO.CreateRuntimeElevator(
                    "elevator_normal",
                    "Elevator",
                    ElevatorShaftKind.Normal,
                    requiredStars: 1,
                    buildCost: 145000));
            if (!_types.ContainsKey("elevator_express"))
                Register(RoomTypeSO.CreateRuntimeElevator(
                    "elevator_express",
                    "Express Elevator",
                    ElevatorShaftKind.Express,
                    requiredStars: 3,
                    buildCost: 12000));
            if (!_types.ContainsKey("elevator_service"))
                Register(RoomTypeSO.CreateRuntimeElevator(
                    "elevator_service",
                    "Service Elevator",
                    ElevatorShaftKind.Service,
                    requiredStars: 4,
                    buildCost: 9000));
        }

        void RegisterRange(IEnumerable<RoomTypeSO> types)
        {
            foreach (var type in types)
                Register(type);
        }

        void Register(RoomTypeSO type)
        {
            if (type == null)
                return;
            if (string.IsNullOrWhiteSpace(type.id))
                throw new InvalidOperationException("Room types in the stable-ID registry must have a non-blank ID.");

            if (!_types.TryGetValue(type.id, out var existing))
            {
                _types.Add(type.id, type);
                return;
            }

            if (ReferenceEquals(existing, type) || GameplayFieldsMatch(existing, type))
                return;

            throw new InvalidOperationException(
                $"Room type ID '{type.id}' has conflicting gameplay definitions.");
        }

        static bool GameplayFieldsMatch(RoomTypeSO left, RoomTypeSO right)
        {
            return left.category == right.category &&
                   left.size == right.size &&
                   left.buildCost == right.buildCost &&
                   left.incomeModel == right.incomeModel &&
                   left.baseIncome == right.baseIncome &&
                   left.noiseOutput.Equals(right.noiseOutput) &&
                   left.noiseSensitivity.Equals(right.noiseSensitivity) &&
                   left.requiresHousekeeping == right.requiresHousekeeping &&
                   left.hasActiveHours == right.hasActiveHours &&
                   left.activeHoursStart == right.activeHoursStart &&
                   left.activeHoursEnd == right.activeHoursEnd &&
                   left.allowAboveGround == right.allowAboveGround &&
                   left.allowBasement == right.allowBasement &&
                   left.isLobby == right.isLobby &&
                   left.isSkyLobby == right.isSkyLobby &&
                   left.isScaffolding == right.isScaffolding &&
                   left.isStairs == right.isStairs &&
                   left.isElevatorShaft == right.isElevatorShaft &&
                   left.isParkingRamp == right.isParkingRamp &&
                   left.luxuryBand == right.luxuryBand &&
                   left.cleanMinutes.Equals(right.cleanMinutes) &&
                   left.requiredStars == right.requiredStars &&
                   left.maxOccupants == right.maxOccupants &&
                   left.streetVisitWeight.Equals(right.streetVisitWeight) &&
                   left.eventCapacity == right.eventCapacity &&
                   left.ResolvedBuildFamily() == right.ResolvedBuildFamily() &&
                   left.ResolvedBuildSubgroup() == right.ResolvedBuildSubgroup();
        }
    }
}
