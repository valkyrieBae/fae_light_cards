using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ManagedFontAtlas;

namespace FaeLightCards
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Fae Light Cards";
        private const string CommandName = "/faecards";

        [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
        [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
        [PluginService] internal static IPluginLog Log { get; private set; } = null!;
        [PluginService] internal static IClientState ClientState { get; private set; } = null!;
        [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;

        public Configuration Configuration { get; init; }
        public GameState GameState { get; init; }
        public RulesEngine RulesEngine { get; init; }
        public UIState UiState { get; private set; }
        public AudioManager AudioManager { get; init; }
        public EventBus EventBus { get; init; }
        public AnimationManager AnimationManager { get; init; }
        public ApplicationState AppState { get; init; }
        public TurnManager TurnManager { get; init; }
        public GameCoordinatorService GameCoordinator { get; init; }
        public CardDeckService CardDeckService { get; init; }
        public WindowSystem WindowSystem = new("FaeLightCards");
        public IGameController GameController { get; internal set; }

        public OverlayWindow OverlayWindow { get; init; }

        public DeckWindow DeckWindow { get; init; }
        public HandWindow HandWindow { get; init; }
        public SettingsWindow SettingsWindow { get; init; }
        public PromptWindow PromptWindow { get; init; }
        public PyramidWindow PyramidWindow { get; init; }
        public PlayersWindow PlayersWindow { get; init; }
        public PyramidControlsWindow PyramidControlsWindow { get; init; }

        public IFontHandle LargeFont { get; init; }
        public IFontHandle MediumFont { get; init; }

        public bool HasTriggeredEndGame { get; set; } = false;






        public bool IsAnimationPlaying => HandWindow.HasActiveAnimations
                                        || UiState.PendingWinLoseMessage != null
                                        || UiState.PromptState == UIState.PromptAnimState.ButtonClick
                                        || UiState.PromptState == UIState.PromptAnimState.Shrinking
                                        || UiState.PromptState == UIState.PromptAnimState.Growing;

        internal bool IsLocalDealer => GameState.ActiveMode == GameMode.Dealer
                                       && (GameState.Players.FirstOrDefault(p => p.IsLocal)?.IsDealer == true
                                           || (AppState.ChosenGameMode == GameMode.Dealer
                                               && AppState.ActiveConnectionMode == ConnectionMode.LocalOnly));

        private bool IsGameRunning => GameState.ActiveMode != GameMode.Undecided;
        private bool ShouldShowDeckVisual => IsGameRunning &&
                                             (GameState.ActivePhase == GamePhase.Accumulation ||
                                              GameState.ActivePhase == GamePhase.BusRide);

        public Plugin()
        {
            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);

            AudioManager = new AudioManager(PluginInterface.AssemblyLocation.DirectoryName!, Log, CommandManager);
            GameState = new GameState();
            RulesEngine = new RulesEngine(this, GameState);
            UiState = new UIState();
            EventBus = new EventBus();
            AnimationManager = new AnimationManager();
            AppState = new ApplicationState();
            TurnManager = new TurnManager();
            CardDeckService = new CardDeckService(Configuration, PluginInterface.AssemblyLocation.DirectoryName!, Log);
            GameCoordinator = new GameCoordinatorService(this);
            GameController = new OfflineController(this);

            DeckWindow = new DeckWindow(this);
            HandWindow = new HandWindow(this);
            SettingsWindow = new SettingsWindow(this);
            PromptWindow = new PromptWindow(this);
            PyramidWindow = new PyramidWindow(this);
            PlayersWindow = new PlayersWindow(this);
            PyramidControlsWindow = new PyramidControlsWindow(this);
            OverlayWindow = new OverlayWindow(this);

            LargeFont = PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(60f)));
            MediumFont = PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(e => e.OnPreBuild(tk => tk.AddDalamudDefaultFont(24f)));

            WindowSystem.AddWindow(DeckWindow);
            WindowSystem.AddWindow(HandWindow);
            WindowSystem.AddWindow(SettingsWindow);
            WindowSystem.AddWindow(PromptWindow);
            WindowSystem.AddWindow(PyramidWindow);
            WindowSystem.AddWindow(PlayersWindow);
            WindowSystem.AddWindow(PyramidControlsWindow);
            WindowSystem.AddWindow(OverlayWindow);

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Opens the plugin UI. Use /faecards settings or /faecards config to open settings."
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigWindow;
            PluginInterface.UiBuilder.OpenMainUi += DrawMainWindow;


            EventBus.PromptStateChangeRequested += HandlePromptStateChange;
            EventBus.PlaySoundRequested += PlaySound;

        }

        private void InitializeStartupMode()
        {
            AppState.ChosenGameMode = GameMode.Undecided;
            AppState.ActiveConnectionMode = ConnectionMode.Undecided;
            AppState.CurrentRoomId = string.Empty;
            AppState.ConnectionFailureMessage = string.Empty;
        }

        public void StartOrShowGameUi()
        {
            if (AppState.GameUiEnabled)
            {
                return;
            }

            AppState.GameUiEnabled = true;
            AppState.HasLaunchedGameUi = true;
            InitializeStartupMode();
        }

        public void HideGameUi()
        {
            ResetGame();
            AppState.GameUiEnabled = false;
            AppState.HasLaunchedGameUi = false;
            CloseGameplayWindows();
        }

        private void HandlePromptStateChange(UIState.PromptAnimState state, float scale)
        {
            if (state == UIState.PromptAnimState.Growing)
            {
                GameCoordinator.GrowPromptIfHidden();
                return;
            }

            UiState.PromptState = state;
            UiState.PromptScale = scale;
            UiState.PromptAnimTimer = 0f;
        }


        public void Dispose()
        {
            WindowSystem.RemoveAllWindows();

            PluginInterface.UiBuilder.Draw -= DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi -= DrawConfigWindow;
            PluginInterface.UiBuilder.OpenMainUi -= DrawMainWindow;
            EventBus.PromptStateChangeRequested -= HandlePromptStateChange;
            EventBus.PlaySoundRequested -= PlaySound;

            CommandManager.RemoveHandler(CommandName);

            GameCoordinator.Dispose();
            HandWindow.Dispose();
            LargeFont.Dispose();
            MediumFont.Dispose();
            AudioManager.Dispose();
            GameController?.Dispose();
            GC.SuppressFinalize(this);
        }

        private void OnCommand(string command, string args)
        {
            switch ((args ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "":
                    if (AppState.GameUiEnabled)
                    {
                        HideGameUi();
                    }
                    else
                    {
                        StartOrShowGameUi();
                    }
                    break;
                case "config":
                case "settings":
                    SettingsWindow.IsOpen = true;
                    break;
                default:
                    ChatGui.Print("Usage: /faecards [settings|config]");
                    break;
            }
        }

        public void PlaySound(string soundFile)
        {
            AudioManager.PlaySound(soundFile);
        }

        public void TriggerWinLoseAnimation(string message, bool showFireworks = false)
        {
            GameCoordinator.TriggerWinLoseAnimation(message, showFireworks);
        }

        private void DrawUI()
        {
            float dt = ImGui.GetIO().DeltaTime;

            if (!AppState.GameUiEnabled)
            {
                CloseGameplayWindows();
                OverlayWindow.IsOpen = HasPendingOverlayMessages();
                WindowSystem.Draw();
                return;
            }

            GameController.Update(dt);
            GameCoordinator.Update(dt);

            PromptWindow.UpdatePromptTransition(dt);

            bool isLocalDealer = IsLocalDealer;
            bool isLocalPlayerAutoDealer = GameState.ActiveMode == GameMode.Player && AppState.ActiveConnectionMode == ConnectionMode.LocalOnly;
            bool isLocalDealerTieChoice = GameState.ActivePhase == GamePhase.TieChoice && isLocalDealer;
            bool hasLocalDealerPhaseChangePrompt = AppState.ActiveConnectionMode == ConnectionMode.LocalOnly
                                                   && isLocalDealer
                                                   && UiState.DealerPhaseChangePrompt != UIState.DealerPhaseChangePromptState.None;
            bool shouldShowPlayers = GameState.ActiveMode != GameMode.Undecided &&
                                     (GameState.ActivePhase == GamePhase.Accumulation || GameState.ActivePhase == GamePhase.Pyramid || GameState.ActivePhase == GamePhase.TieChoice || GameState.ActivePhase == GamePhase.BusRide);

            PromptWindow.IsOpen = (GameState.ActiveMode == GameMode.Undecided ||
                                   (GameState.ActiveMode == GameMode.Dealer && isLocalDealer && GameState.ActivePhase != GamePhase.Pyramid && !isLocalDealerTieChoice && !GameState.HasPendingDrinkTarget) ||
                                   hasLocalDealerPhaseChangePrompt ||
                                   RulesEngine.GetCurrentStage() != null ||
                                   UiState.PromptState != UIState.PromptAnimState.Normal) &&
                                  !isLocalDealerTieChoice &&
                                  MediumFont.Available;
            PyramidWindow.IsOpen = GameState.ActivePhase == GamePhase.Pyramid || GameState.ActivePhase == GamePhase.TieChoice;
            PlayersWindow.IsOpen = shouldShowPlayers;
            PyramidControlsWindow.IsOpen = GameState.ActivePhase == GamePhase.Pyramid && ((GameState.ActiveMode == GameMode.Dealer && isLocalDealer) || isLocalPlayerAutoDealer);
            HandWindow.IsOpen = true;
            DeckWindow.IsOpen = ShouldShowDeckVisual;
            OverlayWindow.IsOpen = true;

            WindowSystem.Draw();
            CheckEndGame();


        }

        private void CloseGameplayWindows()
        {
            PromptWindow.IsOpen = false;
            HandWindow.IsOpen = false;
            DeckWindow.IsOpen = false;
            PyramidWindow.IsOpen = false;
            PyramidControlsWindow.IsOpen = false;
            PlayersWindow.IsOpen = false;
            OverlayWindow.IsOpen = false;
        }

        private bool HasPendingOverlayMessages()
        {
            return UiState.PendingWinLoseMessage != null
                   || UiState.ActiveOverlayMessages.Count > 0
                   || UiState.OverlayMessageQueue.Count > 0
                   || UiState.SecondaryMessage != null
                   || UiState.SecondaryMessageQueue.Count > 0;
        }


        private void DrawConfigWindow()
        {
            SettingsWindow.IsOpen = true;
        }

        private void DrawMainWindow()
        {
            StartOrShowGameUi();
        }



        public void ResetGame() => GameCoordinator.ResetGame();
        public void SetGameMode(GameMode mode) => GameCoordinator.SetGameMode(mode);
        public void DebugSkipToPyramid()
        {
#if FAELIGHTCARDS_DEV_TOOLS
            GameCoordinator.DebugSkipToPyramid();
#endif
        }

        public void DebugSkipToLastCard()
        {
#if FAELIGHTCARDS_DEV_TOOLS
            GameCoordinator.DebugSkipToLastCard();
#endif
        }
        public void ChooseBusRider(string playerName) => GameCoordinator.ChooseBusRider(playerName);
        public void CheckEndGame() => GameCoordinator.CheckEndGame();
    }
}
