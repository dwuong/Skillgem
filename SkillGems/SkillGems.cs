using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.Shared.Helpers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Vector2 = System.Numerics.Vector2;

namespace SkillGems
{
    public class SkillGems : BaseSettingsPlugin<SkillGemsSettings>
    {
        private CancellationTokenSource _gemLevelingCts;
        private Task _gemLevelingTask;
        private Vector2 _mousePosition;

        public override bool Initialise()
        {
            Input.RegisterKey(Settings.Run); // Keep this if you want the manual hotkey
            return true;
        }

        public void Enable()
        {
            _gemLevelingCts = new CancellationTokenSource();
        }

        public void Disable()
        {
            _gemLevelingCts.Cancel();
        }

        private void SetCursorPos(Vector2 v)
        {
            Input.SetCursorPos(GameController.Window.GetWindowRectangleTimeCache.TopLeft.ToVector2Num() + v);
        }

        private void SetCursorPos(Element e)
        {
            SetCursorPos(e.GetClientRectCache.Center.ToVector2Num());
        }

        public override Job Tick()
        {
            // --- NEW: Check if auto-leveling is enabled via settings ---
            // If the auto-leveling toggle is OFF, cancel any running task and stop here.
            if (!Settings.EnableAutoLevelUp.Value)
            {
                _gemLevelingCts?.Cancel();
                return null;
            }
            // --- END NEW ---

            // Old logic for manual key press. If you want *only* auto, you can remove this.
            // If you want both manual and auto, this checks if the manual key is active.
            if (Input.IsKeyDown(Settings.Run.Value))
            {
                // If the manual key is pressed, prioritize it.
                // You might want to add a separate task/logic for manual vs auto.
                // For simplicity, we'll let the auto-leveling logic below handle it if conditions are met.
                // If you want manual to override, you'd put manual logic here and `return null;` after.
            }
            // --- Auto-leveling logic starts here ---
            // Cancel current task if a UI panel is visible (like inventory)
            if (!PanelVisible())
            {
                _gemLevelingCts?.Cancel();
            }
            // If all conditions are met and no leveling task is running, start one.
            else if (CanTick() && IsPlayerAlive() && AnythingToLevel() && PanelVisible() && _gemLevelingTask == null)
            {
                _mousePosition = Input.MousePositionNum;
                _gemLevelingCts = new CancellationTokenSource();
                _gemLevelingTask = Task.FromResult(BeginGemLevel(_gemLevelingCts.Token)).Unwrap();
                _gemLevelingTask.ContinueWith((task) =>
                {
                    _gemLevelingTask = null;
                    if (Settings.ReturnMouseToStart.Value) // Use your existing setting for mouse return
                    {
                        SetCursorPos(_mousePosition);
                    }
                });
            }

            return null;
        }

        private async Task BeginGemLevel(CancellationToken cancellationToken)
        {
            var gemsToLvlUpElements = GetLevelableGems();

            // Check if auto-leveling is still enabled before proceeding
            if (!Settings.EnableAutoLevelUp.Value) return;

            if (!gemsToLvlUpElements.Any()) return;

            foreach (var gemElement in gemsToLvlUpElements)
            {
                // Check for cancellation or if auto-leveling was disabled mid-loop
                if (cancellationToken.IsCancellationRequested || !Settings.EnableAutoLevelUp.Value) return;

                var elementToClick = gemElement.GetChildAtIndex(1); // Assuming the clickable element is always at index 1

                if (elementToClick == null || !elementToClick.IsVisible) continue;

                var ActionDelay = Settings.DelayBetweenEachMouseEvent.Value;
                var GemDelay = Settings.DelayBetweenEachGemClick.Value;

                if (Settings.AddPingIntoDelay.Value)
                {
                    ActionDelay += GameController.IngameState.ServerData.Latency;
                    GemDelay += GameController.IngameState.ServerData.Latency;
                }

                SetCursorPos(elementToClick);
                await Task.Delay(ActionDelay, cancellationToken);
                if (cancellationToken.IsCancellationRequested || !Settings.EnableAutoLevelUp.Value) return;

                Input.LeftDown();
                await Task.Delay(ActionDelay, cancellationToken);
                if (cancellationToken.IsCancellationRequested || !Settings.EnableAutoLevelUp.Value) return;

                Input.LeftUp();
                await Task.Delay(GemDelay, cancellationToken);
            }
        }

        private bool PanelVisible()
        {
            return !(GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible
                     || GameController.Game.IngameState.IngameUi.Atlas.IsVisible
                     || GameController.Game.IngameState.IngameUi.TreePanel.IsVisible
                     || GameController.Game.IngameState.IngameUi.SyndicatePanel.IsVisible
                     || GameController.Game.IngameState.IngameUi.OpenRightPanel.IsVisible
                     || GameController.Game.IngameState.IngameUi.ChatTitlePanel.IsVisible
                     || GameController.Game.IngameState.IngameUi.DelveWindow.IsVisible);
        }

        private bool CanTick()
        {
            return !GameController.IsLoading
                   && GameController.Game.IngameState.ServerData.IsInGame
                   && GameController.Player != null
                   && GameController.Player.Address != 0
                   && GameController.Player.IsValid
                   && GameController.Window.IsForeground();
        }

        private bool IsPlayerAlive()
        {
            return GameController.Game.IngameState.Data.LocalPlayer.GetComponent<Life>().CurHP > 0;
        }

        private bool AnythingToLevel()
        {
            return GetLevelableGems().Any();
        }

        private List<Element> GetLevelableGems()
        {
            var gemsToLevelUp = new List<Element>();
            var possibleGemsToLvlUpElements = GameController.IngameState.IngameUi?.GemLvlUpPanel?.GemsToLvlUp;

            if (possibleGemsToLvlUpElements != null && possibleGemsToLvlUpElements.Any())
            {
                foreach (var possibleGemsToLvlUpElement in possibleGemsToLvlUpElements)
                {
                    foreach (var elem in possibleGemsToLvlUpElement.Children)
                    {
                        if (elem.Text?.Contains("Click to level") == true)
                        {
                            gemsToLevelUp.Add(possibleGemsToLvlUpElement);
                            break;
                        }
                    }
                }
            }
            return gemsToLevelUp;
        }
    }
}