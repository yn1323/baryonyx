using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Baryonyx.Health
{
    // Preserve the native bridge's day object, including unknown and null fields.
    public sealed class HealthDaySnapshot
    {
        public string Day { get; }
        public string Zone { get; }
        public string ObservedAt { get; }
        public bool HasValue { get; }
        public long Steps { get; }
        public string StepsStatus { get; }
        public bool HasAdditionalData { get; }
        public bool HasReadFailures { get; }
        public string Json { get; }

        private HealthDaySnapshot(JObject value)
        {
            Day = RequiredString(value, "day");
            Zone = RequiredString(value, "zone");
            ObservedAt = RequiredString(value, "observedAt");
            var start = DateTimeOffset.Parse(
                RequiredString(value, "startAt"),
                CultureInfo.InvariantCulture
            );
            var end = DateTimeOffset.Parse(
                RequiredString(value, "endAt"),
                CultureInfo.InvariantCulture
            );
            if (
                !DateTime.TryParseExact(
                    Day,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _
                )
                || !DateTimeOffset.TryParse(
                    ObservedAt,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _
                )
                || end < start
                || value["steps"]?.Type != JTokenType.Integer
                || value["hasValue"]?.Type != JTokenType.Boolean
            )
                throw new FormatException("Invalid health day.");
            HasValue = value.Value<bool>("hasValue");
            Steps = value.Value<long>("steps");
            if (Steps < 0 || (!HasValue && Steps != 0))
                throw new FormatException("Invalid steps.");
            StepsStatus = value.Value<string>("stepsStatus") ?? (HasValue ? "success" : "empty");
            if (
                (
                    StepsStatus != "success"
                    && StepsStatus != "empty"
                    && StepsStatus != "permission_required"
                    && StepsStatus != "failed"
                )
                || (StepsStatus == "success") != HasValue
            )
                throw new FormatException("Invalid steps status.");
            HasReadFailures = StepsStatus == "failed";
            if (value["records"] is JObject records)
            {
                foreach (var entry in records.Properties())
                {
                    if (entry.Value is not JObject result || result["records"] is not JArray items)
                        throw new FormatException("Invalid health records.");
                    HasAdditionalData |= items.Count > 0;
                    HasReadFailures |= result.Value<string>("status") == "failed";
                }
            }
            Json = value.ToString(Formatting.Indented);
        }

        public static IReadOnlyList<HealthDaySnapshot> ParseWeek(string json)
        {
            using var input = new StringReader(
                json ?? throw new FormatException("Missing health JSON.")
            );
            using var reader = new JsonTextReader(input)
            {
                DateParseHandling = DateParseHandling.None,
            };
            var root = JObject.Load(
                reader,
                new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                }
            );
            if (reader.Read())
                throw new FormatException("Unexpected content after health JSON.");
            if (
                root.Value<string>("status") != "success"
                || root["days"] is not JArray days
                || days.Count != 7
            )
                throw new FormatException("Expected seven health days.");
            var snapshots = new List<HealthDaySnapshot>(7);
            foreach (var item in days)
            {
                if (item is not JObject day)
                    throw new FormatException("Expected a health day object.");
                snapshots.Add(new HealthDaySnapshot(day));
            }
            snapshots.Sort((left, right) => string.CompareOrdinal(right.Day, left.Day));
            for (int i = 1; i < snapshots.Count; i++)
            {
                if (
                    snapshots[i].Zone != snapshots[0].Zone
                    || DateTime
                        .ParseExact(snapshots[i].Day, "yyyy-MM-dd", CultureInfo.InvariantCulture)
                        .AddDays(1)
                        != DateTime.ParseExact(
                            snapshots[i - 1].Day,
                            "yyyy-MM-dd",
                            CultureInfo.InvariantCulture
                        )
                )
                    throw new FormatException("Expected consecutive health days in one time zone.");
            }
            return snapshots.AsReadOnly();
        }

        private static string RequiredString(JObject value, string key)
        {
            if (
                value[key]?.Type != JTokenType.String
                || string.IsNullOrWhiteSpace(value.Value<string>(key))
            )
                throw new FormatException("Missing health day field.");
            return value.Value<string>(key);
        }
    }
}
