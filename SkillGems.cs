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

        private CancellationTokenSource _hotkeyLevelingCts;
        private Task _hotkeyLevelingTask;

        private Vector2 _mousePosition;

        public override bool Initialise()
        {
            Input.RegisterKey(Settings.Run);
            return true;
        }

        public void Enable()
        {
            _autoGemLevelingCts = new CancellationTokenSource();
        }

        public void Disable()
        {
            _autoGemLevelingCts?.Cancel();
            _autoGemLevelingCts?.Dispose();

            _hotkeyLevelingCts?.Cancel();
            _hotkeyLevelingCts?.Dispose();
        }

        public override void OnUnload()
        {
            _autoGemLevelingCts?.Cancel();
            _autoGemLevelingCts?.Dispose();

            _hotkeyLevelingCts?.Cancel();
            _hotkeyLevelingCts?.Dispose();
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
            if (Settings.EnableAutoLevelUp.Value)
            {
                if (CanTick() && IsPlayerAlive() && AnythingToLevel() && PanelVisible() && _autoGemLevelingTask == null)
                {
                    _mousePosition = Input.MousePositionNum;
                    if (_autoGemLevelingCts == null || _autoGemLevelingCts.IsCancellationRequested)
                    {
                        _autoGemLevelingCts?.Dispose();
                        _autoGemLevelingCts = new CancellationTokenSource();
                    }

                    _autoGemLevelingTask = Task.Run(() => BeginGemLevel(_autoGemLevelingCts.Token), _autoGemLevelingCts.Token);
                    _autoGemLevelingTask.ContinueWith((task) =>
                    {
                        _autoGemLevelingTask = null;
                        _autoGemLevelingCts?.Dispose();
                        _autoGemLevelingCts = null;
                        if (Settings.ReturnMouseToStart.Value)
                        {
                            SetCursorPos(_mousePosition);
                        }
                    }, TaskContinuationOptions.None);
                }
                else if (!CanTick() || !IsPlayerAlive() || !PanelVisible() || !AnythingToLevel())
                {
                    _autoGemLevelingCts?.Cancel();
                }
            }
            else
            {
                _autoGemLevelingCts?.Cancel();
            }

            if (Input.IsKeyDown(Settings.Run.Value))
            {
                if (_hotkeyLevelingTask == null || _hotkeyLevelingTask.IsCompleted || _hotkeyLevelingTask.IsCanceled || _hotkeyLevelingTask.IsFaulted)
                {
                    _hotkeyLevelingCts?.Dispose();
                    _hotkeyLevelingCts = null;

                    _hotkeyLevelingCts = new CancellationTokenSource();
                    _hotkeyLevelingCts.CancelAfter(1000);

                    _mousePosition = Input.MousePositionNum;

                    _hotkeyLevelingTask = Task.Run(() => BeginGemLevel(_hotkeyLevelingCts.Token), _hotkeyLevelingCts.Token);
                    _hotkeyLevelingTask.ContinueWith((task) =>
                    {
                        _hotkeyLevelingTask = null;
                        _hotkeyLevelingCts?.Dispose();
                        _hotkeyLevelingCts = null;
                        if (Settings.ReturnMouseToStart.Value)
                        {
                            SetCursorPos(_mousePosition);
                        }
                    }, TaskContinuationOptions.None);

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
                try
                {
                    await Task.Delay(actionDelay, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (cancellationToken.IsCancellationRequested) return;

                Input.LeftDown();
                try
                {
                    await Task.Delay(actionDelay, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (cancellationToken.IsCancellationRequested) return;

                Input.LeftUp();
                try
                {
                    await Task.Delay(gemDelay, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    return;
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
