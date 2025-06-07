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
        private CancellationTokenSource _autoGemLevelingCts;
        private Task _autoGemLevelingTask;

        private CancellationTokenSource _hotkeyLevelingCts; // This will be created on hotkey press
        private Task _hotkeyLevelingTask; // This will hold the task for the hotkey press

        private Vector2 _mousePosition;

        public override bool Initialise()
        {
            Input.RegisterKey(Settings.Run);
            return true;
        }

        public void Enable()
        {
            // _autoGemLevelingCts should be initialized when the plugin is enabled
            // and disposed when disabled or unloaded.
            _autoGemLevelingCts = new CancellationTokenSource();
        }

        public void Disable()
        {
            // Cancel and dispose the continuous auto-leveling CTS
            _autoGemLevelingCts?.Cancel();
            _autoGemLevelingCts?.Dispose(); // Explicitly dispose here

            // Also cancel and dispose any active hotkey task
            _hotkeyLevelingCts?.Cancel();
            _hotkeyLevelingCts?.Dispose();
        }

        public override void OnUnload()
        {
            // Ensure all tasks are cancelled and disposed when the plugin unloads
            // This is crucial for clean shutdown.
            _autoGemLevelingCts?.Cancel();
            _autoGemLevelingCts?.Dispose();
            // Do NOT wait on _autoGemLevelingTask here. It might still be running.
            // Let the game controller manage its shutdown or handle in Disable.

            _hotkeyLevelingCts?.Cancel();
            _hotkeyLevelingCts?.Dispose();
            // Do NOT wait on _hotkeyLevelingTask here. It might still be running.
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
            // --- Continuous Auto-leveling Logic ---
            if (Settings.EnableAutoLevelUp.Value)
            {
                // Conditions to start/continue continuous auto-leveling
                if (CanTick() && IsPlayerAlive() && AnythingToLevel() && PanelVisible() && _autoGemLevelingTask == null)
                {
                    _mousePosition = Input.MousePositionNum; // Save mouse position
                    // Ensure a new CTS is created if we are starting a new continuous task
                    if (_autoGemLevelingCts == null || _autoGemLevelingCts.IsCancellationRequested)
                    {
                        _autoGemLevelingCts?.Dispose(); // Dispose if cancelled but not null
                        _autoGemLevelingCts = new CancellationTokenSource();
                    }

                    _autoGemLevelingTask = Task.Run(() => BeginGemLevel(_autoGemLevelingCts.Token), _autoGemLevelingCts.Token);
                    _autoGemLevelingTask.ContinueWith((task) =>
                    {
                        // Clean up the task reference and CTS after it completes or is cancelled
                        _autoGemLevelingTask = null;
                        _autoGemLevelingCts?.Dispose(); // Dispose after task is done
                        _autoGemLevelingCts = null; // Clear reference
                        if (Settings.ReturnMouseToStart.Value)
                        {
                            SetCursorPos(_mousePosition);
                        }
                    }, TaskContinuationOptions.None); // Use TaskContinuationOptions.None to always run, then check task.IsCanceled
                }
                else if (!CanTick() || !IsPlayerAlive() || !PanelVisible() || !AnythingToLevel())
                {
                    // If conditions are no longer met, cancel the continuous task
                    _autoGemLevelingCts?.Cancel();
                }
            }
            else
            {
                // If auto-leveling is disabled via settings, cancel any active task
                _autoGemLevelingCts?.Cancel();
            }


            // --- Hotkey-triggered 1-second Auto-leveling Logic ---
            if (Input.IsKeyDown(Settings.Run.Value))
            {
                // If a hotkey task is already running OR has completed/been cancelled,
                // and it's not null, ensure it's properly handled before starting a new one.
                // This prevents trying to operate on a disposed CTS.
                if (_hotkeyLevelingTask == null || _hotkeyLevelingTask.IsCompleted || _hotkeyLevelingTask.IsCanceled || _hotkeyLevelingTask.IsFaulted)
                {
                    // Dispose the previous CTS if it exists and is no longer needed
                    _hotkeyLevelingCts?.Dispose();
                    _hotkeyLevelingCts = null; // Clear the reference

                    _hotkeyLevelingCts = new CancellationTokenSource();
                    _hotkeyLevelingCts.CancelAfter(1000); // 1000 milliseconds = 1 second duration

                    _mousePosition = Input.MousePositionNum; // Save mouse position before starting

                    _hotkeyLevelingTask = Task.Run(() => BeginGemLevel(_hotkeyLevelingCts.Token), _hotkeyLevelingCts.Token);
                    _hotkeyLevelingTask.ContinueWith((task) =>
                    {
                        // Clean up the task reference and CTS after it completes or is cancelled
                        _hotkeyLevelingTask = null;
                        _hotkeyLevelingCts?.Dispose(); // Dispose after task is done
                        _hotkeyLevelingCts = null; // Clear reference
                        if (Settings.ReturnMouseToStart.Value)
                        {
                            SetCursorPos(_mousePosition);
                        }
                    }, TaskContinuationOptions.None); // Use TaskContinuationOptions.None to always run, then check task.IsCanceled

                    // Keep this if you want a single trigger per key press (release and re-press needed)
                    // Remove it if you want it to trigger every Tick while held down.
                    Input.KeyUp(Settings.Run.Value);
                }
            }

            return null;
        }

        private async Task BeginGemLevel(CancellationToken cancellationToken)
        {
            var gemsToLvlUpElements = GetLevelableGems();

            if (!gemsToLvlUpElements.Any()) return;

            foreach (var gemElement in gemsToLvlUpElements)
            {
                // Always check for cancellation before and after delays/actions.
                if (cancellationToken.IsCancellationRequested) return;

                var elementToClick = gemElement.GetChildAtIndex(1);

                if (elementToClick == null || !elementToClick.IsVisible) continue;

                var actionDelay = Settings.DelayBetweenEachMouseEvent.Value;
                var gemDelay = Settings.DelayBetweenEachGemClick.Value;

                if (Settings.AddPingIntoDelay.Value)
                {
                    actionDelay += GameController.IngameState.ServerData.Latency;
                    gemDelay += GameController.IngameState.ServerData.Latency;
                }

                SetCursorPos(elementToClick);
                // Use `await Task.Delay` with the provided cancellation token.
                try
                {
                    await Task.Delay(actionDelay, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    return; // Exit if cancelled during delay
                }

                if (cancellationToken.IsCancellationRequested) return;

                Input.LeftDown();
                try
                {
                    await Task.Delay(actionDelay, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    return; // Exit if cancelled during delay
                }

                if (cancellationToken.IsCancellationRequested) return;

                Input.LeftUp();
                try
                {
                    await Task.Delay(gemDelay, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    return; // Exit if cancelled during delay
                }
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
                    bool foundClickText = false;
                    if (possibleGemsToLvlUpElement.Text?.Contains("Click to level") == true)
                    {
                        foundClickText = true;
                    }
                    else
                    {
                        foreach (var elem in possibleGemsToLvlUpElement.Children)
                        {
                            if (elem.Text?.Contains("Click to level") == true)
                            {
                                foundClickText = true;
                                break;
                            }
                        }
                    }

                    if (foundClickText)
                    {
                        gemsToLevelUp.Add(possibleGemsToLvlUpElement);
                    }
                }
            }
            return gemsToLevelUp;
        }
    }
}
