namespace FaeLightCards
{
    public static class UIConstants
    {
        // Card Dimensions
        public const float BaseCardWidth = 375f;
        public const float BaseCardHeight = 544f;
        public const float CardTextureScaleMultiplier = 3.75f;

        // Paddings and Spacing
        public const float DefaultPadX = 15f;
        public const float DefaultPadY = 15f;
        public const float DefaultSpacing = 10f;

        // Animations
        public const float ConveyorSlideDuration = 0.215f;
        public const float ConveyorHoldDuration = 2.31f;
        public const float ConveyorCenterCompletionDelay = 0.75f;
        public const float WinLoseAnimDuration = 2.4f;
        public const float AiThinkingBaseDuration = 1.5f;
        public const float AiThinkingVariance = 1.0f;
        public const float AiResultRevealDelay = 1.0f;
        public const float AiOutcomeHoldDuration = 1.6f;
        public const float PyramidDealerStepDelay = 1.8f;
        public const float SecondaryMessagePopDuration = 0.25f;
        public const float SecondaryMessageSettleDuration = 0.15f;
        public const float SecondaryMessageMinVisibleDuration = 2.2f;
        public const float SecondaryMessageFadeDuration = 0.45f;
        public const float SecondaryMessageDuration = SecondaryMessageMinVisibleDuration + SecondaryMessageFadeDuration;

        public const float ButtonClickDuration = 0.15f;
        public const float PromptShrinkDuration = 0.25f;
        public const float PromptGrowDuration = 0.25f;
    }
}
