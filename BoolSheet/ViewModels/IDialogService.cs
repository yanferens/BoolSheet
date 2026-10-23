using BoolSheet.Core.Storage;

namespace BoolSheet.ViewModels;

/// <summary>
/// Lets view models talk to the user without depending on a concrete page.
/// Implemented by the main page; the tests supply a fake.
/// </summary>
public interface IDialogService
{
    Task ShowMessageAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>Offers several choices; returns the chosen one, or null if the user cancels.</summary>
    Task<string?> ChooseAsync(string title, string cancel, params string[] choices);

    /// <summary>Asks for a line of text; returns null if the user cancels.</summary>
    Task<string?> PromptAsync(string title, string message, string initialValue);

    /// <summary>Lets the user choose one of the cloud files; returns null if the user cancels.</summary>
    Task<CloudFile?> PickCloudFileAsync(IReadOnlyList<CloudFile> files);

    /// <summary>
    /// Shows a "Save as" dialog and writes <paramref name="content"/> to the chosen file.
    /// Returns the file path, or null if the user cancels.
    /// </summary>
    Task<string?> SaveFileAsAsync(string suggestedName, string content);

    /// <summary>Reports a bug (an exception nobody expected) without closing the program.</summary>
    Task ShowUnexpectedErrorAsync(Exception exception);
}
