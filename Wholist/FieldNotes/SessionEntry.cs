using System;

namespace Wholist.FieldNotes
{
    internal sealed class SessionEntry
    {
        public string Name { get; init; } = string.Empty;
        public uint HomeWorldId { get; init; }
        public string HomeWorldName { get; init; } = string.Empty;
        public string? CurrentWorldName { get; set; }
        public DateTime FirstSeenUtc { get; init; }
        public DateTime LastSeenUtc { get; set; }
        public int SeenCount { get; set; }
        public bool IsVisible { get; set; }
    }
}
