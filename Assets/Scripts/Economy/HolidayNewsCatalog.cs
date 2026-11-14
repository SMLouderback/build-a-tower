using System;
using System.Collections.Generic;

namespace BuildATower
{
    /// <summary>
    /// Calendar holidays for tower-news quirks. Dates follow the Gregorian game calendar
    /// (epoch Saturday 1 Jan 2000). Includes major US federal days plus popular observances.
    /// </summary>
    public static class HolidayNewsCatalog
    {
        public readonly struct HolidayMatch
        {
            public readonly string Id;
            public readonly string Name;
            public readonly string[] Lines;

            public HolidayMatch(string id, string name, string[] lines)
            {
                Id = id;
                Name = name;
                Lines = lines;
            }
        }

        /// <summary>All holiday matches for <paramref name="date"/> (usually 0–1; rare overlaps possible).</summary>
        public static List<HolidayMatch> MatchesFor(DateTime date)
        {
            var list = new List<HolidayMatch>(2);
            var y = date.Year;
            var m = date.Month;
            var d = date.Day;

            // Fixed-date observances.
            TryFixed(list, m, d, 1, 1, "new_year", "New Year's Day", NewYearLines);
            TryFixed(list, m, d, 2, 2, "groundhog", "Groundhog Day", GroundhogLines);
            TryFixed(list, m, d, 2, 14, "valentine", "Valentine's Day", ValentineLines);
            TryFixed(list, m, d, 3, 14, "pi_day", "Pi Day", PiDayLines);
            TryFixed(list, m, d, 3, 17, "st_patrick", "St. Patrick's Day", StPatrickLines);
            TryFixed(list, m, d, 4, 1, "april_fools", "April Fools' Day", AprilFoolsLines);
            TryFixed(list, m, d, 4, 22, "earth_day", "Earth Day", EarthDayLines);
            TryFixed(list, m, d, 5, 1, "may_day", "May Day", MayDayLines);
            TryFixed(list, m, d, 5, 5, "cinco", "Cinco de Mayo", CincoLines);
            TryFixed(list, m, d, 6, 14, "flag_day", "Flag Day", FlagDayLines);
            TryFixed(list, m, d, 6, 19, "juneteenth", "Juneteenth", JuneteenthLines);
            TryFixed(list, m, d, 7, 4, "july4", "Independence Day", July4Lines);
            TryFixed(list, m, d, 10, 31, "halloween", "Halloween", HalloweenLines);
            TryFixed(list, m, d, 11, 11, "veterans", "Veterans Day", VeteransLines);
            TryFixed(list, m, d, 12, 24, "xmas_eve", "Christmas Eve", ChristmasEveLines);
            TryFixed(list, m, d, 12, 25, "christmas", "Christmas", ChristmasLines);
            TryFixed(list, m, d, 12, 26, "boxing", "Boxing Day", BoxingDayLines);
            TryFixed(list, m, d, 12, 31, "nye", "New Year's Eve", NewYearsEveLines);

            // Floating observances.
            TryDate(list, date, NthWeekday(y, 1, DayOfWeek.Monday, 3), "mlk", "Martin Luther King Jr. Day", MlkLines);
            TryDate(list, date, NthWeekday(y, 2, DayOfWeek.Monday, 3), "presidents", "Presidents' Day", PresidentsLines);
            TryDate(list, date, EasterSunday(y), "easter", "Easter", EasterLines);
            TryDate(list, date, NthWeekday(y, 5, DayOfWeek.Sunday, 2), "mothers", "Mother's Day", MothersDayLines);
            TryDate(list, date, LastWeekday(y, 5, DayOfWeek.Monday), "memorial", "Memorial Day", MemorialDayLines);
            TryDate(list, date, NthWeekday(y, 6, DayOfWeek.Sunday, 3), "fathers", "Father's Day", FathersDayLines);
            TryDate(list, date, NthWeekday(y, 9, DayOfWeek.Monday, 1), "labor", "Labor Day", LaborDayLines);
            TryDate(list, date, NthWeekday(y, 10, DayOfWeek.Monday, 2), "columbus", "Columbus Day", ColumbusLines);
            TryDate(list, date, NthWeekday(y, 11, DayOfWeek.Thursday, 4), "thanksgiving", "Thanksgiving", ThanksgivingLines);

            return list;
        }

        public static bool IsHoliday(DateTime date) => MatchesFor(date).Count > 0;

        public static DateTime NthWeekday(int year, int month, DayOfWeek dayOfWeek, int n)
        {
            var first = new DateTime(year, month, 1);
            var offset = ((int)dayOfWeek - (int)first.DayOfWeek + 7) % 7;
            return first.AddDays(offset + 7 * (n - 1));
        }

        public static DateTime LastWeekday(int year, int month, DayOfWeek dayOfWeek)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var last = new DateTime(year, month, daysInMonth);
            var offset = ((int)last.DayOfWeek - (int)dayOfWeek + 7) % 7;
            return last.AddDays(-offset);
        }

        /// <summary>Western Easter Sunday (Anonymous Gregorian algorithm).</summary>
        public static DateTime EasterSunday(int year)
        {
            var a = year % 19;
            var b = year / 100;
            var c = year % 100;
            var d = b / 4;
            var e = b % 4;
            var f = (b + 8) / 25;
            var g = (b - f + 1) / 3;
            var h = (19 * a + b - d - g + 15) % 30;
            var i = c / 4;
            var k = c % 4;
            var l = (32 + 2 * e + 2 * i - h - k) % 7;
            var m = (a + 11 * h + 22 * l) / 451;
            var month = (h + l - 7 * m + 114) / 31;
            var day = ((h + l - 7 * m + 114) % 31) + 1;
            return new DateTime(year, month, day);
        }

        static void TryFixed(
            List<HolidayMatch> list,
            int month,
            int day,
            int wantMonth,
            int wantDay,
            string id,
            string name,
            string[] lines)
        {
            if (month == wantMonth && day == wantDay)
                list.Add(new HolidayMatch(id, name, lines));
        }

        static void TryDate(
            List<HolidayMatch> list,
            DateTime date,
            DateTime holiday,
            string id,
            string name,
            string[] lines)
        {
            if (date.Date == holiday.Date)
                list.Add(new HolidayMatch(id, name, lines));
        }

        // --- Funny ticker lines (tower-flavored) ---

        static readonly string[] NewYearLines =
        {
            "New Year's Day: the lobby countdown clock is still on last year. Facilities shrugs.",
            "Resolution desk opened in the lobby: 'take stairs once' is already crossed out.",
            "Confetti found in Elevator B. Nobody claims it. Security is taking names anyway."
        };

        static readonly string[] GroundhogLines =
        {
            "Groundhog Day: a guest asked if six more weeks of winter apply to elevator waits.",
            "Lobby debate: if the groundhog sees its shadow, do we still open the gift shop?",
            "Maintenance swears the boiler predicted the weather first. The groundhog is a backup."
        };

        static readonly string[] ValentineLines =
        {
            "Valentine's Day: someone left a rose in the express elevator. It has a meeting invite attached.",
            "Office candy hearts say 'ASAP' and 'CC ME'. Romance is thriving.",
            "Front desk is redirecting anonymous crush notes to Lost & Found. Again."
        };

        static readonly string[] PiDayLines =
        {
            "Pi Day: the cafeteria sold pie until 3:14. Then they sold regret.",
            "An engineer tried to bill 3.14159 hours. Payroll rounded to 3.14 and moved on.",
            "Whiteboard on 5 now just says 3.14159265… and 'do not erase'."
        };

        static readonly string[] StPatrickLines =
        {
            "St. Patrick's Day: the lobby plant is wearing a tiny green hat. Nobody confesses.",
            "Security is hunting a rogue bagpipe ringtone on floor 4.",
            "Someone dyed the water cooler green. Housekeeping has opinions."
        };

        static readonly string[] AprilFoolsLines =
        {
            "April Fools': Facilities announced free roof parking. There is no roof parking.",
            "A memo claimed Elevator A now goes to floor π. Three people tried anyway.",
            "Prank of the day: a sticky note on the boss chair that just says 'synergy?'"
        };

        static readonly string[] EarthDayLines =
        {
            "Earth Day: tenants are arguing about who unplugged the coffee machine 'for the planet'.",
            "Recycling bin overflowed with good intentions and one shoe.",
            "The lobby plant got a TED-talk length pep talk. It looks the same."
        };

        static readonly string[] MayDayLines =
        {
            "May Day: somebody braided flowers onto a stair rail. Risk assessment is pending.",
            "A maypole was proposed for the atrium. Legal said 'define pole'.",
            "Spring cleaning found three jackets and a mystery thermos labeled 'DO NOT'."
        };

        static readonly string[] CincoLines =
        {
            "Cinco de Mayo: the break room salsa has been upgraded from 'mild' to 'career-limiting'.",
            "Mariachi ringtone wars on floor 2. Security is losing.",
            "Guest asked if the stairs count as a parade route. Staff said only downhill."
        };

        static readonly string[] FlagDayLines =
        {
            "Flag Day: someone hung a tiny flag on the mail cart. It has seniority now.",
            "Lobby pledge of allegiance lasted until the elevator dinged. Priorities.",
            "A patriotic sticky note appeared on the coffee machine: 'I too serve.'"
        };

        static readonly string[] JuneteenthLines =
        {
            "Juneteenth: the tower library cart is out of history books and full of waitlist slips.",
            "Community table in the lobby: free lemonade and aggressively helpful pamphlets.",
            "A guest thanked security for the welcome banner. Security pretended they planned it."
        };

        static readonly string[] July4Lines =
        {
            "Fourth of July: sparklers are banned. Glitter is somehow worse.",
            "Rooftop fireworks plan rejected. Elevator 'light show' plan also rejected.",
            "Someone hummed the national anthem in the stairwell. Echoes for days."
        };

        static readonly string[] HalloweenLines =
        {
            "Halloween: a guest checked in as a ghost. Housekeeping wants hazard pay.",
            "Candy bowl emptied by 10am. Security suspects the interns.",
            "Costume contest: Elevator B dressed as Elevator A. Judges are split."
        };

        static readonly string[] VeteransLines =
        {
            "Veterans Day: complimentary coffee for service members — until the machine betrayed everyone.",
            "Lobby thank-you cards filled a whole bulletin board. One just says 'stairs rule'.",
            "A quiet moment at noon. Then someone microwaved fish. Balance restored."
        };

        static readonly string[] ChristmasEveLines =
        {
            "Christmas Eve: the gift shop is out of everything except batteries and hope.",
            "Someone wrapped the lobby plant. It looks offended and festive.",
            "Overnight staff report 'mysterious cookie plate'. Cookies gone. Plate remains."
        };

        static readonly string[] ChristmasLines =
        {
            "Christmas: elevators are full of shopping bags and one very patient tree.",
            "A guest asked if Santa uses the freight elevator. Staff said 'union rules'.",
            "Ugly sweater day claimed another victim on floor 6. The sweater won."
        };

        static readonly string[] BoxingDayLines =
        {
            "Boxing Day: returns line wraps past the stairs. Nobody is boxing. Everyone is sighing.",
            "Gift shop restock: boxes of boxes. Meta is thriving.",
            "A guest tried to return a hotel stay. Front desk admired the ambition."
        };

        static readonly string[] NewYearsEveLines =
        {
            "New Year's Eve: confetti budget already overspent. It's only morning.",
            "Party on 12 booked solid. Noise complaint from 11 pre-filed.",
            "Countdown rehearsal in the lobby startled three tourists and one security raccoon rumor."
        };

        static readonly string[] MlkLines =
        {
            "MLK Day: community reading hour in the lobby — standing room only by the stairs.",
            "A quote board filled up before lunch. Someone added 'hold the elevator for each other'.",
            "Quiet programming upstairs; microwave fish still found a way."
        };

        static readonly string[] PresidentsLines =
        {
            "Presidents' Day: mattress sale ads somehow reached the elevator screens.",
            "Cherry-tree joke told three times before noon. Facilities has an axe. Metaphorically.",
            "Office pool: who can name the most presidents while waiting for Elevator A."
        };

        static readonly string[] EasterLines =
        {
            "Easter: a plastic egg blocked the stair door. Investigation ongoing.",
            "Egg hunt rules: no elevators, no sabotage, no 'borrowing' from Lost & Found.",
            "Someone hid chocolate on floor 8. Temperatures rose. Literally."
        };

        static readonly string[] MothersDayLines =
        {
            "Mother's Day: floral delivery bottleneck in the lobby. Bees theoretically concerned.",
            "Brunch reservations spilled into a stair landing. Ambition.",
            "A kid asked if the tower has a mom floor. Staff said every floor on Sundays."
        };

        static readonly string[] MemorialDayLines =
        {
            "Memorial Day: unofficial start of summer — the AC is not convinced.",
            "Grill smoke reported from a balcony. Rules clarified. Scent lingered.",
            "Long weekend energy: half the offices empty, all the elevators somehow full."
        };

        static readonly string[] FathersDayLines =
        {
            "Father's Day: novelty ties sold out. Dad jokes did not.",
            "A guest asked for the 'best floor for grilling advice'. Staff suggested Outside.",
            "Tool demo in Maintenance drew a crowd. Nobody fixed anything. Morale up."
        };

        static readonly string[] LaborDayLines =
        {
            "Labor Day: the coffee machine deserves hazard pay and a parade.",
            "Unofficial end of summer: flip-flops spotted in a boardroom. Minutes sealed.",
            "Staff picnic proposed for the roof. Gravity and Legal voted no."
        };

        static readonly string[] ColumbusLines =
        {
            "Columbus Day: map in the lobby still says 'here be snacks'. Accurate.",
            "Debate club booked Conference for 'history, politely'. Snacks less polite.",
            "Someone claimed they discovered a new shortcut. It was the other stairs."
        };

        static readonly string[] ThanksgivingLines =
        {
            "Thanksgiving: turkey perfume in Elevator A. There is no turkey perfume product. Worry.",
            "Leftover negotiations in the break room reached binding arbitration.",
            "Guest gratitude list includes 'fast elevators' and 'no microwave fish today'."
        };
    }
}
