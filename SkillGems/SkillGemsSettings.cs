using ExileCore.Shared.Attributes; // Add this using directive for [Menu]
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Windows.Forms;

namespace SkillGems
{
    public class SkillGemsSettings : ISettings
    {
        // Mandatory setting to allow enabling/disabling your plugin
        public ToggleNode Enable { get; set; } = new ToggleNode(false);

        // New setting to enable/disable auto-leveling
        [Menu("Enable Auto Gem Leveling", 1)] // This will show up in the settings menu
        public ToggleNode EnableAutoLevelUp { get; set; } = new ToggleNode(true); // Default to true (enabled)

        public ToggleNode ReturnMouseToStart { get; set; } = new ToggleNode(true);
        public ToggleNode AddPingIntoDelay { get; set; } = new ToggleNode(false);

        // This 'Run' hotkey is for manual activation. You can keep it if you want both auto and manual.
        public HotkeyNode Run { get; set; } = new HotkeyNode(Keys.A);

        public RangeNode<int> DelayBetweenEachGemClick { get; set; } = new RangeNode<int>(20, 0, 1000);
        public RangeNode<int> DelayBetweenEachMouseEvent { get; set; } = new RangeNode<int>(20, 0, 1000);

        // Put all your settings here if you can.
        // There's a bunch of ready-made setting nodes,
        // nested menu support and even custom callbacks are supported.
        // If you want to override DrawSettings instead, you better have a very good reason.
    }
}