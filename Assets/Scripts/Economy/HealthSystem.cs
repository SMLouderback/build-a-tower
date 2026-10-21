using System;
using System.Collections.Generic;

namespace BuildATower
{
    public sealed class HealthSystem
    {
        public const float MaxIllness = 100f;
        public const float GuestRaisePerMinute = 0.04f;
        public const float SickRoomRaisePerMinute = 0.15f;
        public const float NaturalDecayPerMinute = 0.05f;
        public const float ClinicBaselinePerMinute = 0.04f;
        public const float ClinicProximityPerMinute = 0.20f;
        public const int ClinicProximityFloors = 8;

        public const float QuarantineOn = 0.25f;
        public const float QuarantineOff = 0.22f;
        public const float HotelFillThreshold = 0.10f;
        public const float OfficeFillThreshold = 0.15f;
        public const float CondoFillThreshold = 0.20f;

        public const float IllnessMarkThreshold = 40f;
        public const float IllnessMarkMinutes = 360f;
        public const float DoctorIllnessDrop = 18f;
        public const int DoctorMinMinutes = 24 * 60;
        public const int DoctorMaxMinutes = 48 * 60;
        public const float GuestStressAtMax = 12f;
        public const float OfficeRestroomStressRelief = 2f;

        readonly Dictionary<int, float> _illness = new();
        readonly Dictionary<int, float> _sickMarkMinutes = new();
        int _towerFloorCount;

        public CleanMode CleanMode { get; set; } = CleanMode.Normal;
        public bool Quarantined { get; set; }

        public float GetIllness(int floor) =>
            _illness.TryGetValue(floor, out var value) ? value : 0f;

        public void SetIllness(int floor, float value)
        {
            var clamped = Clamp(value);
            if (Approximately(clamped, 0f))
                _illness.Remove(floor);
            else
                _illness[floor] = clamped;
            AverageIllness = ComputeAverage(_towerFloorCount);
        }

        public float AverageIllness { get; private set; }

        public void Tick(
            float deltaGameMinutes,
            IReadOnlyDictionary<int, int> hotelGuestsByFloor,
            IReadOnlyDictionary<int, bool> trashCoveredByFloor,
            IReadOnlyDictionary<int, int> sickRoomsByFloor,
            IReadOnlyList<int> workingClinicFloors,
            int towerFloorCount)
        {
            if (deltaGameMinutes <= 0f)
                return;

            var decayingFloors = new List<int>(_illness.Keys);

            if (hotelGuestsByFloor != null)
            {
                foreach (var kv in hotelGuestsByFloor)
                {
                    if (kv.Value <= 0)
                        continue;
                    if (IsTrashCovered(trashCoveredByFloor, kv.Key))
                        continue;
                    Add(kv.Key, GuestRaisePerMinute * kv.Value * deltaGameMinutes);
                }
            }

            if (sickRoomsByFloor != null)
            {
                foreach (var kv in sickRoomsByFloor)
                {
                    if (kv.Value <= 0)
                        continue;
                    Add(kv.Key, SickRoomRaisePerMinute * kv.Value * deltaGameMinutes);
                }
            }

            foreach (var floor in decayingFloors)
                Add(floor, -DecayPerFloor(floor, deltaGameMinutes, workingClinicFloors));

            _towerFloorCount = towerFloorCount;
            AverageIllness = ComputeAverage(towerFloorCount);
        }

        static bool IsTrashCovered(IReadOnlyDictionary<int, bool> trashCoveredByFloor, int floor)
        {
            if (trashCoveredByFloor == null)
                return false;
            return trashCoveredByFloor.TryGetValue(floor, out var covered) && covered;
        }

        static float DecayPerFloor(int floor, float deltaGameMinutes, IReadOnlyList<int> workingClinicFloors)
        {
            var decay = NaturalDecayPerMinute * deltaGameMinutes;
            if (workingClinicFloors == null)
                return decay;

            for (var i = 0; i < workingClinicFloors.Count; i++)
            {
                decay += ClinicBaselinePerMinute * deltaGameMinutes;
                var distance = Math.Abs(floor - workingClinicFloors[i]);
                if (distance > ClinicProximityFloors)
                    continue;
                decay += ClinicProximityPerMinute * (1f - distance / (float)ClinicProximityFloors) * deltaGameMinutes;
            }

            return decay;
        }

        float ComputeAverage(int towerFloorCount)
        {
            if (towerFloorCount <= 0)
                return 0f;

            var sum = 0f;
            foreach (var kv in _illness)
                sum += kv.Value;
            return sum / towerFloorCount;
        }

        void Add(int floor, float delta)
        {
            if (Approximately(delta, 0f))
                return;

            var next = Clamp(GetIllness(floor) + delta);
            if (Approximately(next, 0f))
                _illness.Remove(floor);
            else
                _illness[floor] = next;
        }

        static float Clamp(float value)
        {
            if (value < 0f)
                return 0f;
            if (value > MaxIllness)
                return MaxIllness;
            return value;
        }

        static bool Approximately(float a, float b) =>
            Math.Abs(b - a) < Math.Max(0.000001f * Math.Max(Math.Abs(a), Math.Abs(b)), 1.1920928955078125e-7f * 8f);

        /// <summary>
        /// Adds time only while the floor stays at or above <see cref="IllnessMarkThreshold"/>.
        /// Below it the floor's timer resets. Returns true (and resets) once
        /// <see cref="IllnessMarkMinutes"/> have accumulated.
        /// </summary>
        public bool AdvanceSickMarkTimer(int floor, float deltaGameMinutes)
        {
            if (GetIllness(floor) < IllnessMarkThreshold)
            {
                _sickMarkMinutes.Remove(floor);
                return false;
            }

            if (deltaGameMinutes < 0f)
                deltaGameMinutes = 0f;

            _sickMarkMinutes.TryGetValue(floor, out var minutes);
            minutes += deltaGameMinutes;
            if (minutes >= IllnessMarkMinutes)
            {
                _sickMarkMinutes.Remove(floor);
                return true;
            }

            _sickMarkMinutes[floor] = minutes;
            return false;
        }

        /// <summary>Puts the full mark window back when <see cref="AdvanceSickMarkTimer"/> fired but no room could be marked.</summary>
        public void RestoreSickMarkTimer(int floor) =>
            _sickMarkMinutes[floor] = IllnessMarkMinutes;

        /// <summary>
        /// Runs the floor's mark timer and, when it fires, marks one room sick. Returns true
        /// when a room was marked.
        /// </summary>
        public bool TryMarkSickRoom(int floor, float deltaGameMinutes, IReadOnlyList<RoomInstance> rooms)
        {
            if (!AdvanceSickMarkTimer(floor, deltaGameMinutes))
                return false;

            var room = ChooseRoomToSicken(rooms, floor);
            if (room == null)
            {
                RestoreSickMarkTimer(floor);
                return false;
            }

            room.SetSick(true);
            return true;
        }

        /// <summary>Hotel first, then office/condo; never sick or broken rooms. Ties go to the lowest instance id.</summary>
        public RoomInstance ChooseRoomToSicken(IReadOnlyList<RoomInstance> rooms, int floor)
        {
            if (rooms == null)
                return null;

            RoomInstance best = null;
            var bestRank = int.MaxValue;
            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (!IsTenantRoom(room) || room.Origin.y != floor || room.Sick || room.IsBroken)
                    continue;

                var rank = room.Type.category == RoomCategory.Hotel ? 0 : 1;
                if (rank > bestRank)
                    continue;
                if (rank == bestRank && room.InstanceId >= best.InstanceId)
                    continue;

                best = room;
                bestRank = rank;
            }

            return best;
        }

        public void ApplyDoctorCure(int floor) =>
            SetIllness(floor, GetIllness(floor) - DoctorIllnessDrop);

        public static float SickShare(int sick, int tenants) =>
            tenants <= 0 ? 0f : sick / (float)tenants;

        /// <summary>Counts placed hotels, offices, and condos; <paramref name="sick"/> is the sick subset.</summary>
        public static int CountTenants(IReadOnlyList<RoomInstance> rooms, out int sick)
        {
            sick = 0;
            if (rooms == null)
                return 0;

            var tenants = 0;
            for (var i = 0; i < rooms.Count; i++)
            {
                var room = rooms[i];
                if (!IsTenantRoom(room))
                    continue;
                tenants++;
                if (room.Sick)
                    sick++;
            }

            return tenants;
        }

        public static float GuestStressGain(float illness) =>
            illness <= 0f ? 0f : GuestStressAtMax * (illness / MaxIllness);

        public static bool IsTenantRoom(RoomInstance room)
        {
            if (room == null || ReferenceEquals(room.Type, null))
                return false;
            return room.Type.category is RoomCategory.Hotel or RoomCategory.Office or RoomCategory.Condo;
        }

        public static float FillMultiplier(float sickShare, float threshold)
        {
            if (sickShare < threshold)
                return 1f;

            var span = 0.25f - threshold;
            var t = (sickShare - threshold) / span;
            if (t < 0f)
                t = 0f;
            else if (t > 1f)
                t = 1f;
            return 0.85f * (1f - t);
        }

        public static bool EvaluateQuarantine(bool currentlyOn, float sickShare)
        {
            if (currentlyOn)
            {
                if (sickShare <= QuarantineOff)
                    return false;
                return true;
            }

            return sickShare >= QuarantineOn;
        }

        public HealthSnapshotV1 CaptureSnapshot()
        {
            var count = _illness.Count;
            var floors = new float[count];
            var values = new float[count];
            var index = 0;
            foreach (var kv in _illness)
            {
                floors[index] = kv.Key;
                values[index] = kv.Value;
                index++;
            }

            return new HealthSnapshotV1
            {
                floorIndices = floors,
                illness = values,
                cleanMode = CleanMode.ToString(),
                quarantined = Quarantined
            };
        }

        /// <summary>
        /// Restore from a save. Null or an absent legacy block clears illness and resets clean mode and quarantine.
        /// Throws <see cref="ArgumentException"/> when the snapshot is present but invalid.
        /// </summary>
        public void RestoreSnapshot(HealthSnapshotV1 snapshot)
        {
            _illness.Clear();
            CleanMode = CleanMode.Normal;
            Quarantined = false;
            AverageIllness = 0f;

            if (IsAbsent(snapshot))
                return;

            if (!TryValidateSnapshot(snapshot, out var error))
                throw new ArgumentException(error, nameof(snapshot));

            Enum.TryParse(snapshot.cleanMode, false, out CleanMode mode);
            CleanMode = mode;
            Quarantined = snapshot.quarantined;

            for (var i = 0; i < snapshot.floorIndices.Length; i++)
                SetIllness((int)snapshot.floorIndices[i], snapshot.illness[i]);
        }

        /// <summary>
        /// True for older saves with no health block. <c>JsonUtility</c> materializes a missing
        /// serializable class as an all-default object, so blank clean mode and empty arrays count as absent too.
        /// </summary>
        public static bool IsAbsent(HealthSnapshotV1 snapshot) =>
            snapshot == null
            || (string.IsNullOrEmpty(snapshot.cleanMode)
                && IsNullOrEmpty(snapshot.floorIndices)
                && IsNullOrEmpty(snapshot.illness)
                && !snapshot.quarantined);

        public static bool TryValidateSnapshot(HealthSnapshotV1 snapshot, out string error)
        {
            if (snapshot == null)
            {
                error = "Health snapshot is missing.";
                return false;
            }

            if (!TryParseCleanMode(snapshot.cleanMode, out _))
            {
                error = "Health clean mode is unknown.";
                return false;
            }

            if (snapshot.floorIndices == null || snapshot.illness == null)
            {
                error = "Health floor illness arrays are missing.";
                return false;
            }

            if (snapshot.floorIndices.Length != snapshot.illness.Length)
            {
                error = "Health floor and illness arrays differ in length.";
                return false;
            }

            for (var i = 0; i < snapshot.floorIndices.Length; i++)
            {
                var floor = snapshot.floorIndices[i];
                if (!IsWholeNumber(floor))
                {
                    error = "A health floor index is not a whole number.";
                    return false;
                }

                var illness = snapshot.illness[i];
                if (illness < 0f || illness > MaxIllness)
                {
                    error = "A health illness value is outside 0–100.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        static bool TryParseCleanMode(string value, out CleanMode mode)
        {
            if (!Enum.TryParse(value, false, out mode))
                return false;

            return mode is CleanMode.Quick or CleanMode.Normal or CleanMode.Deep;
        }

        static bool IsNullOrEmpty(float[] values) => values == null || values.Length == 0;

        static bool IsWholeNumber(float value) =>
            Math.Abs(value - MathF.Round(value)) < 0.0001f;
    }
}
