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
        public bool SoundEnabled { get; set; }
        public SortMode SortMode { get; set; }

        public GameSettings()
        {
            SoundEnabled = true;
            SortMode = SortMode.Ascending;
        }
    }
}

