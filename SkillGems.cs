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

        private CancellationTokenSource _hotkeyGemLevelingCts;
        private Task _hotkeyGemLevelingTask;

        private Vector2 _mousePosition;

        public override bool Initialise()
        {
            Input.RegisterKey(Settings.Run);
            return true;
        }

        public override void OnLoad()
        {
        }

        public override void OnUnload()
        {
            _autoGemLevelingCts?.Cancel();
            _autoGemLevelingCts?.Dispose();
            _autoGemLevelingTask?.Wait();

            _hotkeyGemLevelingCts?.Cancel();
            _hotkeyGemLevelingCts?.Dispose();
            _hotkeyGemLevelingTask?.Wait();
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
                    _autoGemLevelingCts = new CancellationTokenSource();
                    _autoGemLevelingTask = Task.Run(() => BeginGemLevel(_autoGemLevelingCts.Token), _autoGemLevelingCts.Token);
                    _autoGemLevelingTask.ContinueWith((task) =>
                    {
                        _autoGemLevelingTask = null;
                        _autoGemLevelingCts?.Dispose();
                        if (Settings.ReturnMouseToStart.Value)
                        {
                            SetCursorPos(_mousePosition);
                        }
                    }, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.NotOnCanceled);
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
                _hotkeyGemLevelingCts?.Cancel();
                _hotkeyGemLevelingCts?.Dispose();

                _hotkeyGemLevelingCts = new CancellationTokenSource();
                _hotkeyGemLevelingCts.CancelAfter(1000);

                _mousePosition = Input.MousePositionNum;

                _hotkeyGemLevelingTask = Task.Run(() => BeginGemLevel(_hotkeyGemLevelingCts.Token), _hotkeyGemLevelingCts.Token);
                _hotkeyGemLevelingTask.ContinueWith((task) =>
                {
                    _hotkeyGemLevelingTask = null;
                    _hotkeyGemLevelingCts?.Dispose();
                    if (Settings.ReturnMouseToStart.Value)
                    {
                        SetCursorPos(_mousePosition);
                    }
                }, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.NotOnCanceled);

                Input.KeyUp(Settings.Run.Value);
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
                await Task.Delay(actionDelay, cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;

                Input.LeftDown();
                await Task.Delay(actionDelay, cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;
