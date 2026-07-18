using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Tossakan.Helpers;

public static class Dialogs
{
    public static async Task<bool> ConfirmAsync(
        XamlRoot xamlRoot, string title, string message, string actionText = "Delete")
    {
        var dialog = new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = title,
            Content = message,
            PrimaryButtonText = actionText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }
}
