namespace PKForge.App.Views;

/// <summary>Lightweight lower-screen surface used while Poképark is open.</summary>
public sealed class PokeparkJournalPage : ContentPage
{
    private readonly PokeparkJournalView _view = new();
    private bool _cleanedUp;

    public PokeparkJournalPage()
    {
        BackgroundColor = Theme.UiTokens.Housing;
        Content = _view;
    }

    public ValueTask RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_cleanedUp) MainThread.BeginInvokeOnMainThread(_view.InvalidateSurface);
        return ValueTask.CompletedTask;
    }

    public void Cleanup()
    {
        if (_cleanedUp) return;
        _cleanedUp = true;
        _view.Detach();
    }
}
