using System;

namespace Mariasek.SharedClient
{
    public enum SortMode
    {
        Ascending,
        Descending,
        None
    }

    public class GameSettings
    {
        public SortMode SortMode { get; set; }
    }
}

