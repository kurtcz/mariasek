using System.Windows.Input;

namespace Mariasek.TesterGUI
{
    public class Commands
    {
        static Commands()
        {
            NewGame = new RoutedUICommand("_New Game", "NewGame", typeof(Commands), new InputGestureCollection(){ new KeyGesture(Key.N, ModifierKeys.Control, "Ctrl-N")});
            LoadGame = new RoutedUICommand("_Load Game", "LoadGame", typeof(Commands), new InputGestureCollection() { new KeyGesture(Key.L, ModifierKeys.Control, "Ctrl-L") });
            SaveGame = new RoutedUICommand("_Save Game", "SaveGame", typeof(Commands), new InputGestureCollection() { new KeyGesture(Key.L, ModifierKeys.Control, "Ctrl-S") });
            EndGame = new RoutedUICommand("_End", "EndGame", typeof(Commands), new InputGestureCollection() { new KeyGesture(Key.F4, ModifierKeys.Alt, "Alt-F4") });
            DisplaySettings = new RoutedUICommand("_Settings", "DisplaySettings", typeof(Commands));
            Rewind = new RoutedUICommand("_Rewind", "Rewind", typeof(Commands), new InputGestureCollection() { new KeyGesture(Key.Z, ModifierKeys.Control, "Ctrl-Z") });
            ShowHands = new RoutedUICommand("Show _Hands", "ShowHands", typeof(Commands), new InputGestureCollection() { new KeyGesture(Key.H, ModifierKeys.Control, "Ctrl-H") });
            Editor = new RoutedUICommand("_Editor", "Editor", typeof(Commands), new InputGestureCollection() { new KeyGesture(Key.E, ModifierKeys.Control, "Ctrl-E") });
            Log = new RoutedUICommand("Lo_g", "Log", typeof(Commands), new InputGestureCollection() { new KeyGesture(Key.Tab, ModifierKeys.None, "Tab") });
            Probabilities = new RoutedUICommand("_Probabilities", "Probabilities", typeof(Commands), new InputGestureCollection() { new KeyGesture(Key.P, ModifierKeys.Control, "Ctrl-P") });
        }

        public static RoutedUICommand NewGame { get; private set; }
        public static RoutedUICommand LoadGame { get; private set; }
        public static RoutedUICommand SaveGame { get; private set; }
        public static RoutedUICommand EndGame { get; private set; }
        public static RoutedUICommand DisplaySettings { get; private set; }
        public static RoutedUICommand Rewind { get; private set; }
        public static RoutedUICommand ShowHands { get; private set; }
        public static RoutedUICommand Editor { get; private set; }
        public static RoutedUICommand Log { get; private set; }
        public static RoutedUICommand Probabilities { get; private set; }
    }
}
