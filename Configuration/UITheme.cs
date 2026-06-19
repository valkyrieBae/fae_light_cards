using System.Numerics;

namespace FaeLightCards
{
    public static class UITheme
    {
        public static readonly Vector4 WhiteText = new(1f, 1f, 1f, 1.0f);
        public static readonly Vector4 MutedText = new(0.6f, 0.6f, 0.6f, 0.4f);

        public static readonly Vector4 PrimaryFill = new(0.2f, 0.5f, 0.3f, 1.0f);
        public static readonly Vector4 PrimaryOutline = new(0.1f, 0.3f, 0.15f, 1.0f);
        public static readonly Vector4 SecondaryFill = new(0.2f, 0.4f, 0.6f, 1.0f);
        public static readonly Vector4 SecondaryOutline = new(0.1f, 0.2f, 0.3f, 1.0f);
        public static readonly Vector4 DangerFill = new(0.85f, 0.15f, 0.2f, 1.0f);
        public static readonly Vector4 DangerOutline = new(0.4f, 0.05f, 0.08f, 1.0f);
        public static readonly Vector4 DisabledFill = new(0.2f, 0.2f, 0.2f, 0.4f);
        public static readonly Vector4 DisabledOutline = new(0.4f, 0.4f, 0.4f, 0.4f);
        public static readonly Vector4 NeutralFill = new(0.25f, 0.25f, 0.3f, 1.0f);
        public static readonly Vector4 NeutralOutline = new(0.15f, 0.15f, 0.2f, 1.0f);
        public static readonly Vector4 SuitRedFill = new(0.80f, 0.15f, 0.2f, 1.0f);
        public static readonly Vector4 SuitBlackFill = new(0.12f, 0.12f, 0.14f, 1.0f);
        public static readonly Vector4 SuitBlackOutline = new(0.4f, 0.4f, 0.4f, 1.0f);
        public static readonly Vector4 SuitBlackText = new(0.85f, 0.85f, 0.85f, 1.0f);
        public static readonly Vector4 SuitBlackTextOutline = new(0.2f, 0.2f, 0.2f, 1.0f);
        public static readonly Vector4 PauseFill = new(0.45f, 0.32f, 0.12f, 1.0f);
        public static readonly Vector4 PauseOutline = new(0.3f, 0.2f, 0.08f, 1.0f);
        public static readonly Vector4 PromptBackground = new(0.05f, 0.05f, 0.08f, 0.85f);
        public static readonly Vector4 PromptBorder = new(0.2f, 0.2f, 0.3f, 0.9f);
        public static readonly Vector4 InputBackground = new(0.12f, 0.12f, 0.18f, 0.95f);
        public static readonly Vector4 InputBorder = new(0.25f, 0.25f, 0.38f, 0.90f);
        public static readonly Vector4 SuccessText = new(0.2f, 0.8f, 0.2f, 1.0f);
        public static readonly Vector4 WarningText = new(1.0f, 0.6f, 0.0f, 1.0f);
        public static readonly Vector4 GoldText = new(0.98f, 0.75f, 0.14f, 1f);

        public static readonly ButtonTheme Primary = new(PrimaryFill, PrimaryOutline, WhiteText, PrimaryOutline);
        public static readonly ButtonTheme Secondary = new(SecondaryFill, SecondaryOutline, WhiteText, SecondaryOutline);
        public static readonly ButtonTheme Danger = new(DangerFill, DangerOutline, WhiteText, DangerOutline);
        public static readonly ButtonTheme Disabled = new(DisabledFill, DisabledOutline, MutedText, DisabledOutline);
        public static readonly ButtonTheme Neutral = new(NeutralFill, NeutralOutline, WhiteText, NeutralOutline);
        public static readonly ButtonTheme Pause = new(PauseFill, PauseOutline, WhiteText, PauseOutline);
        public static readonly ButtonTheme Red = new(DangerFill, new Vector4(0f, 0f, 0f, 1.0f), WhiteText, new Vector4(0f, 0f, 0f, 1.0f));
        public static readonly ButtonTheme Black = new(new Vector4(0.08f, 0.08f, 0.1f, 1.0f), new Vector4(0.6f, 0.1f, 0.15f, 1.0f), WhiteText, new Vector4(0.6f, 0.1f, 0.15f, 1.0f));
        public static readonly ButtonTheme SuitRed = new(SuitRedFill, DangerOutline, WhiteText, DangerOutline);
        public static readonly ButtonTheme SuitBlack = new(SuitBlackFill, SuitBlackOutline, SuitBlackText, SuitBlackTextOutline);
        public static readonly ButtonTheme Inside = new(new Vector4(0.2f, 0.5f, 0.4f, 1.0f), new Vector4(0.1f, 0.3f, 0.2f, 1.0f), WhiteText, new Vector4(0.1f, 0.3f, 0.2f, 1.0f));
        public static readonly ButtonTheme Outside = new(new Vector4(0.4f, 0.3f, 0.5f, 1.0f), new Vector4(0.2f, 0.15f, 0.3f, 1.0f), WhiteText, new Vector4(0.2f, 0.15f, 0.3f, 1.0f));

        public static Vector4 WithOpacity(Vector4 color, float opacity)
        {
            return new Vector4(color.X, color.Y, color.Z, color.W * opacity);
        }

        public static ButtonTheme ForEnabled(bool enabled, ButtonTheme enabledTheme)
        {
            return enabled ? enabledTheme : Disabled;
        }
    }
}
