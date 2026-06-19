using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Bindings.ImGui;

namespace FaeLightCards
{
    public partial class HandWindow
    {
        public void Dispose()
        {
            plugin.EventBus.BusRideCardDealt -= TriggerBusRideDeal;
            plugin.EventBus.CardDealt -= TriggerDealAnimation;
            plugin.EventBus.BusRideSlideDown -= TriggerBusRideSlideDown;
            plugin.EventBus.LocalCardMatched -= HandleLocalCardMatched;
            plugin.EventBus.ScionCardMatched -= HandleScionCardMatched;
            FlushPendingHandScaleSave();
        }
        private void HandleLocalCardMatched(Player source, Card card, int handIndex, int pyramidIndex, string targetPlayerName)
        {
            Vector2 endPos = plugin.PyramidWindow.GetPyramidCardScreenPos(pyramidIndex);
            float rowScale = plugin.PyramidWindow.GetPyramidCardScale(pyramidIndex);
            float w = plugin.PyramidWindow.GetPyramidCardWidth(pyramidIndex);
            float h = plugin.PyramidWindow.GetPyramidCardHeight(pyramidIndex);

            TriggerDiscardAnimation(card, handIndex, pyramidIndex, endPos, rowScale, w, h, GameConstants.LocalPlayerName, targetPlayerName);
        }
        private void HandleScionCardMatched(Player source, Card card, int pyramidIndex, string targetPlayerName)
        {
            Vector2 startPos = plugin.PyramidWindow.GetPlayerRowScreenPos(source.Name);
            Vector2 endPos = plugin.PyramidWindow.GetPyramidCardScreenPos(pyramidIndex);
            float rowScale = plugin.PyramidWindow.GetPyramidCardScale(pyramidIndex);
            float w = plugin.PyramidWindow.GetPyramidCardWidth(pyramidIndex);
            float h = plugin.PyramidWindow.GetPyramidCardHeight(pyramidIndex);

            TriggerScionDiscardAnimation(card, startPos, pyramidIndex, endPos, rowScale, w, h, source.Name, targetPlayerName);
        }
    }
}
