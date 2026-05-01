namespace MindAtlas.Web;

public enum ToastLevel { Info, Success, Error }

public sealed class ToastService
{
    public event Action<string, ToastLevel>? OnShow;

    public void Show(string message, ToastLevel level = ToastLevel.Info)
        => OnShow?.Invoke(message, level);

    public void Error(string message) => Show(message, ToastLevel.Error);
    public void Success(string message) => Show(message, ToastLevel.Success);
    public void Info(string message) => Show(message, ToastLevel.Info);
}
