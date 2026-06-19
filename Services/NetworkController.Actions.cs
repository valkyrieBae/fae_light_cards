using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FaeLightCards
{
    public partial class NetworkController
    {
        public void HandlePlayerGuess(int pickedOptionIndex)
        {
            QueueAction("PlayerGuess", new { choice = pickedOptionIndex });
        }
        public void HandleDealerDeal()
        {
            QueueAction("DealerDeal");
        }
        public void HandleDealerAdvanceNextPlayer()
        {
            QueueAction("DealerAdvancePlayer");
        }
        public void HandleFlipPyramidCard()
        {
            QueueAction("FlipPyramidCard");
        }
        public void HandleAdvancePyramidRow()
        {
            QueueAction("AdvancePyramidRow");
        }
        public void PlayLocalMatch(string targetPlayerName)
        {
            int slotIdx = plugin.GameState.PendingLocalMatchSlotIndex;
            if (slotIdx == -1 || string.IsNullOrWhiteSpace(pendingMatchId)) return;

            QueueAction("PlayLocalMatch", new
            {
                matchId = pendingMatchId,
                targetPlayer = targetPlayerName,
            });
        }
        public void GivePendingDrinkToPlayer(string targetPlayerName)
        {
            var localPlayer = plugin.GameState.Players.FirstOrDefault(p => p.IsLocal);
            if (!plugin.GameState.HasPendingDrinkTarget || localPlayer == null || localPlayer.Name != plugin.GameState.PendingDrinkGiverName)
            {
                return;
            }

            string? drinkId = pendingDrinkId ?? plugin.GameState.PendingDrinkId;
            if (string.IsNullOrWhiteSpace(drinkId))
            {
                return;
            }

            QueueAction("GivePendingDrinkToPlayer", new
            {
                drinkId = drinkId,
                targetPlayer = targetPlayerName
            });
        }
        public void HandleDealerDealBusCard()
        {
            QueueAction("DealerDealBusCard");
        }
        public void EndGame()
        {
            QueueAction("ResetGame");
        }
        public void JoinRoom(string roomId)
        {
            string normalizedRoomId = roomId.ToUpperInvariant().Trim();
            plugin.AppState.CurrentRoomId = normalizedRoomId;

            QueueAction("JoinRoom", new
            {
                roomId = normalizedRoomId,
                playerName = this.localPlayerName,
                resumeToken = resumeToken
            });
        }
        public void StartGame(bool includeNpcs)
        {
            QueueAction("StartGame", new
            {
                includeNpcs = includeNpcs
            });
        }
        public void DebugSkipToPyramid()
        {
#if FAELIGHTCARDS_DEV_TOOLS
            if (serverDebugToolsEnabled)
            {
                QueueAction("DebugSkipToPyramid");
            }
#endif
        }
        public void DebugSkipToLastCard()
        {
#if FAELIGHTCARDS_DEV_TOOLS
            if (serverDebugToolsEnabled)
            {
                QueueAction("DebugSkipToLastCard");
            }
#endif
        }
        public void ChooseBusRider(string playerName)
        {
            QueueAction("ChooseBusRider", new
            {
                playerName = playerName
            });
        }
        public void AddNpcPlayer()
        {
            QueueAction("AddNpcPlayer");
        }
    }
}
