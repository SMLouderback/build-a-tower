using UnityEngine;

namespace BuildATower
{
    public static class HealthHudFormat
    {
        const string ClinicId = "service_clinic";

        public static string FormatBar(float averageIllness, float sickShare, bool quarantined)
        {
            var illness = Mathf.RoundToInt(averageIllness);
            var sickPct = Mathf.RoundToInt(sickShare * 100f);
            var text = $"Illness {illness} · Sick {sickPct}%";
            if (quarantined)
                text += " · Quarantine";
            return text;
        }

        public static string StaffLabel(string roomTypeId, int staffed) =>
            roomTypeId == ClinicId
                ? $"Doctors ({staffed}/4)"
                : $"Staff ({staffed}/4)";

        public const string WaitingOnDoctor = "Waiting on a doctor.";
    }
}
