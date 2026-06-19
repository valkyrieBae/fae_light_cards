namespace FaeLightCards
{
    public enum ConnectionMode
    {
        Undecided,
        LocalOnly,
        Connecting,
        Connected,
        Networked,
        ConnectionFailed
    }

    public class ApplicationState
    {
        public bool GameUiEnabled { get; set; } = false;
        public bool HasLaunchedGameUi { get; set; } = false;
        public ConnectionMode ActiveConnectionMode { get; set; } = ConnectionMode.Undecided;
        public GameMode ChosenGameMode { get; set; } = GameMode.Undecided;
        public string CurrentRoomId { get; set; } = string.Empty;
        public string ConnectionFailureMessage { get; set; } = string.Empty;
        public float ResetToModeSelectionTimer { get; set; } = 0f;
    }
}
